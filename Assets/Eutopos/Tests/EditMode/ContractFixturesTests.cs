using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    /// <summary>
    /// Jawaban server yang sebenarnya, disalin byte demi byte dari eutopos-vps tests/fixtures/
    /// (localize_ok.json, localize_failed.json, navgraph_response.json; lihat docs/contract.md di sana).
    /// Jangan mengubah isi berkas dari sini: ubah di server, lalu salin ulang.
    /// </summary>
    public class ContractFixturesTests
    {
        static string Read(string name) =>
            File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Eutopos/Tests/EditMode/" + name));

        [Test]
        public void LocalizeOkFixtureParsesToThePoseInTheBuildingFrame()
        {
            var r = VpsResponses.ClassifyLocalize(200, Read("localize_ok.json"), false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Ok));
            Assert.That(r.PositionG, Is.EqualTo(new Vector3(1.5f, 2.0f, 1.3f)));
            Assert.That(r.RotationG, Is.EqualTo(Quaternion.Identity));
            Assert.That((r.Inliers, r.AreaId, r.MapVersion), Is.EqualTo((120, "floor10", (int?)4)));
            Assert.That(r.TimingsS["total"], Is.EqualTo(0.9f).Within(1e-6f));
            Assert.That(r.ContractVersion, Is.EqualTo(1));
        }

        [Test]
        public void LocalizeFailedFixtureIsAFailedAnswerNotAnError()
        {
            var r = VpsResponses.ClassifyLocalize(200, Read("localize_failed.json"), false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Failed));
            Assert.That((r.Inliers, r.ContractVersion), Is.EqualTo((7, 1)));
        }

        [Test]
        public void NavGraphFixtureKeepsTheRevisionAsTheServerSentIt()
        {
            var r = VpsResponses.ParseNavGraph(200, Read("navgraph_response.json"), false);
            Assert.That(r.Status, Is.EqualTo(VpsStatus.Ok));
            Assert.That(r.Nav, Is.Not.Null);
            // string buram: dibandingkan apa adanya, bukan diuraikan sebagai tanggal (docs/contract.md)
            Assert.That(r.Revision, Is.EqualTo("2026-10-22T06:33:00+00:00"));
            Assert.That((r.AreaId, r.MapVersion, r.ContractVersion), Is.EqualTo(("floor10", (int?)4, 1)));
        }

        [Test]
        public void AnswersFromAnOlderServerWithoutAVersionStillWorkAsVersionZero()
        {
            var loc = VpsResponses.ClassifyLocalize(200, NoVersion(Read("localize_ok.json")), false);
            Assert.That((loc.Status, loc.ContractVersion), Is.EqualTo((VpsStatus.Ok, 0)));
            var nav = VpsResponses.ParseNavGraph(200, NoVersion(Read("navgraph_response.json")), false);
            Assert.That((nav.Status, nav.ContractVersion), Is.EqualTo((VpsStatus.Ok, 0)));
        }

        static string NoVersion(string json)
        {
            var cut = json.Replace("\"contract_version\": 1,", "");
            Assert.That(cut, Is.Not.EqualTo(json), "fixture tidak memuat contract_version");
            return cut;
        }
    }
}
