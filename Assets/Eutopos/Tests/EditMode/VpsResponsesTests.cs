using System.Globalization;
using System.Numerics;
using System.Threading;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class VpsResponsesTests
    {
        const string Ok = @"{""status"":""ok"",""inliers"":120,""correspondences"":300,""intrinsics_source"":""client"",
          ""pose_model"":{""position"":[0,0,0],""rotation_xyzw"":[0,0,0,1]},
          ""pose_building"":{""position"":[1.5,2.0,1.3],""rotation_xyzw"":[0,0,0,1]},
          ""area_id"":""floor10"",""map_version"":4,""t_s"":{""total"":0.9}}";

        [Test]
        public void OkAnswerCarriesThePoseInTheBuildingFrame()
        {
            var r = VpsResponses.ClassifyLocalize(200, Ok, false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Ok));
            Assert.That(r.PositionG, Is.EqualTo(new Vector3(1.5f, 2.0f, 1.3f)));
            Assert.That(r.RotationG, Is.EqualTo(Quaternion.Identity));
            Assert.That((r.Inliers, r.AreaId, r.MapVersion), Is.EqualTo((120, "floor10", (int?)4)));
            Assert.That(r.TimingsS["total"], Is.EqualTo(0.9f).Within(1e-6f));
        }

        [Test]
        public void OnlyAnswersThatRetryingCannotFixStopTheTimer()
        {
            // 401/403 dan 503 tidak pulih dengan mengulang tiap 5 detik (spesifikasi bagian 9).
            Assert.That(VpsResponses.StopsTimer(VpsStatus.Unauthorized), Is.True);
            Assert.That(VpsResponses.StopsTimer(VpsStatus.NoMap), Is.True);
            foreach (var s in new[] { VpsStatus.Ok, VpsStatus.Failed, VpsStatus.NotAligned, VpsStatus.NetworkError, VpsStatus.ServerError })
                Assert.That(VpsResponses.StopsTimer(s), Is.False, s.ToString());
        }

        [Test]
        public void FewInliersOrFailedStatusIsFailed()
        {
            Assert.That(VpsResponses.ClassifyLocalize(200, Ok.Replace("120", "49"), false).Status, Is.EqualTo(VpsStatus.Failed));
            Assert.That(VpsResponses.ClassifyLocalize(200, Ok.Replace(@"""ok""", @"""failed"""), false).Status, Is.EqualTo(VpsStatus.Failed));
        }

        [Test]
        public void MissingBuildingPoseMeansNotAligned()
        {
            var body = Ok.Replace(@"{""position"":[1.5,2.0,1.3],""rotation_xyzw"":[0,0,0,1]}", "null");
            Assert.That(VpsResponses.ClassifyLocalize(200, body, false).Status, Is.EqualTo(VpsStatus.NotAligned));
        }

        [TestCase(302)]
        [TestCase(401)]
        [TestCase(403)]
        public void AccessRedirectsAndRefusalsAreUnauthorized(long code)
        {
            var r = VpsResponses.ClassifyLocalize(code, "<html>login</html>", false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Unauthorized));
        }

        [Test]
        public void OtherOutcomes()
        {
            Assert.That(VpsResponses.ClassifyLocalize(503, "{}", false).Status, Is.EqualTo(VpsStatus.NoMap));
            Assert.That(VpsResponses.ClassifyLocalize(0, null, true).Status, Is.EqualTo(VpsStatus.NetworkError));
            Assert.That(VpsResponses.ClassifyLocalize(500, "boom", false).Status, Is.EqualTo(VpsStatus.ServerError));
            Assert.That(VpsResponses.ClassifyLocalize(200, "<html>", false).Status, Is.EqualTo(VpsStatus.ServerError));
        }

        // Cloudflare menjawab 5xx dengan halaman HTML saat server di belakang terowongan mati (530 = Error 1033)
        [TestCase(502)]
        [TestCase(504)]
        [TestCase(522)]
        [TestCase(530)]
        public void UnreachableServerBehindCloudflareIsANetworkError(long code)
        {
            const string page = "<!doctype html>\n<html class=\"no-js\"><title>Cloudflare Tunnel error</title></html>";
            var l = VpsResponses.ClassifyLocalize(code, page, false);
            var g = VpsResponses.ParseNavGraph(code, page, false);
            Assert.That((l.Status, g.Status), Is.EqualTo((VpsStatus.NetworkError, VpsStatus.NetworkError)));
            StringAssert.DoesNotContain("<", l.Detail);
            StringAssert.DoesNotContain("<", g.Detail);
        }

        [Test]
        public void HtmlBodiesNeverReachTheDetailShownOnScreen()
        {
            var r = VpsResponses.ClassifyLocalize(500, "<html><body>Internal Server Error</body></html>", false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.ServerError));
            StringAssert.DoesNotContain("<", r.Detail);
            StringAssert.Contains("500", r.Detail);
        }

        // jawaban 200 yang rusak tidak boleh melempar: pelempar mematikan coroutine dan scheduler macet
        [TestCase(@"{""status"":""ok"",""inliers"":120,""t_s"":{""total"":null},""pose_building"":null}")]
        [TestCase(@"{""status"":""ok"",""inliers"":120,""t_s"":{""total"":{}},""pose_building"":null}")]
        [TestCase(@"{""status"":""ok"",""inliers"":120,""pose_building"":{""rotation_xyzw"":[0,0,0,1]}}")]
        [TestCase(@"{""status"":""ok"",""inliers"":120,""pose_building"":{""position"":[1,2],""rotation_xyzw"":[0,0,0,1]}}")]
        [TestCase(@"{""status"":""ok"",""inliers"":""many""}")]
        [TestCase(@"{""status"":""ok"",""inliers"":120,""area_id"":{}}")]
        [TestCase(@"[]")]
        [TestCase(null)]
        public void MalformedOkBodyIsAServerError(string body)
        {
            Assert.That(VpsResponses.ClassifyLocalize(200, body, false).Status, Is.EqualTo(VpsStatus.ServerError));
        }

        [TestCase(@"{""area_id"":""a"",""graph"":{""frame"":""building"",""units"":""m"",""nodes"":[1],""edges"":[],""targets"":[]}}")]
        [TestCase(@"{""area_id"":""a"",""graph"":{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[""A""]}}")]
        [TestCase(@"{""area_id"":""a"",""graph"":{""frame"":{}}}")]
        [TestCase(@"{""area_id"":""a""}")]
        [TestCase(null)]
        public void MalformedNavGraphBodyIsAServerError(string body)
        {
            Assert.That(VpsResponses.ParseNavGraph(200, body, false).Status, Is.EqualTo(VpsStatus.ServerError));
        }

        [Test]
        public void NavGraphOutcomes()
        {
            const string graph = @"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0},{""id"":""B"",""x"":1,""y"":0}],""edges"":[[""A"",""B""]],""targets"":[{""node"":""B"",""name"":""Lab""}]}";
            var ok = VpsResponses.ParseNavGraph(200, @"{""area_id"":""floor10"",""map_version"":4,""graph"":" + graph + "}", false);
            Assert.That(ok.Status, Is.EqualTo(VpsStatus.Ok));
            Assert.That(ok.Graph.Targets[0].Name, Is.EqualTo("Lab"));
            Assert.That(VpsResponses.ParseNavGraph(404, "{}", false).Status, Is.EqualTo(VpsStatus.NotFound));
            var broken = VpsResponses.ParseNavGraph(200, @"{""area_id"":""a"",""map_version"":1,""graph"":{""frame"":""x""}}", false);
            Assert.That(broken.Status, Is.EqualTo(VpsStatus.ServerError));
            StringAssert.Contains("frame", broken.Detail);
        }

        [Test]
        public void NumbersUseADotEvenOnAnIndonesianPhone()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("id-ID");
                Assert.That(VpsResponses.Number(1234.5f), Is.EqualTo("1234.5"));
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }
    }
}
