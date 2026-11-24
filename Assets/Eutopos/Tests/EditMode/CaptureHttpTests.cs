using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eutopos.Core.Capture;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public sealed class CaptureHttpTests
    {
        sealed class FakeHttp : ICaptureHttp
        {
            public readonly List<HttpCall> Calls = new List<HttpCall>();
            readonly Queue<HttpReply> replies;

            public FakeHttp(params HttpReply[] replies) => this.replies = new Queue<HttpReply>(replies);

            public Task<HttpReply> Send(HttpCall call)
            {
                Calls.Add(call);
                Assert.That(replies.Count, Is.GreaterThan(0), $"{call.Method} tanpa balasan uji");
                return Task.FromResult(replies.Dequeue());
            }
        }

        static readonly CaptureAuth Auth = new CaptureAuth
        {
            BaseUrl = "https://capture.example.test/",
            ClientId = "fixture-id",
            ClientSecret = "fixture-secret",
            AppToken = "fixture-app",
        };

        string root, stateFile;
        UploadState state;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "eutopos-tus-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            stateFile = Path.Combine(root, "upload-peta.zip.json");
            state = new UploadState
            {
                JobId = 23, Name = "peta.zip", Path = Path.Combine(root, "peta.zip"), Size = 120,
            };
            File.WriteAllBytes(state.Path, Enumerable.Range(0, 120).Select(i => (byte)i).ToArray());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static HttpReply Reply(long status, string header = null, string value = null, string text = null) =>
            new HttpReply
            {
                Status = status, Text = text,
                Headers = header == null ? null : new Dictionary<string, string> { [header] = value },
            };

        static HttpReply Created(string location = "upload-1") => Reply(201, "location", location);
        static HttpReply Offset(long status, int n) => Reply(status, "upload-offset", n.ToString());

        Task Upload(ICaptureHttp http, Func<TimeSpan, Task> delay = null) =>
            new TusUploader(http, Auth, 50, delay ?? (_ => Task.CompletedTask))
                .Upload(state, stateFile, null, CancellationToken.None);

        static void AssertAuth(HttpCall call)
        {
            Assert.That(call.Headers["CF-Access-Client-Id"], Is.EqualTo(Auth.ClientId));
            Assert.That(call.Headers["CF-Access-Client-Secret"], Is.EqualTo(Auth.ClientSecret));
            Assert.That(call.Headers["Authorization"], Is.EqualTo("Bearer " + Auth.AppToken));
        }

        [Test]
        public async Task CreateJobSendsFramesKindAndAllAuthHeaders()
        {
            var http = new FakeHttp(Reply(201, text: "{\"id\":23}"));
            int job = await new CaptureApi(http, Auth).CreateJob("lorong", "Lorong",
                new[] { ("peta.zip", "peta", 120L), ("uji.zip", "uji", 100L) });

            Assert.That(job, Is.EqualTo(23));
            var call = http.Calls.Single();
            Assert.That(call.Method, Is.EqualTo("POST"));
            Assert.That(call.Url, Is.EqualTo("https://capture.example.test/api/jobs"));
            AssertAuth(call);
            var body = JObject.Parse(Encoding.UTF8.GetString(call.Body));
            Assert.That((string)body["area_id"], Is.EqualTo("lorong"));
            Assert.That((string)body["area_name"], Is.EqualTo("Lorong"));
            Assert.That(body["videos"].Select(v => (string)v["kind"]),
                Is.EqualTo(new[] { "frames", "frames" }));
            Assert.That(body["videos"].Select(v => (string)v["role"]), Is.EqualTo(new[] { "peta", "uji" }));
            Assert.That(body["videos"].Select(v => (long)v["size"]), Is.EqualTo(new[] { 120L, 100L }));
        }

        [Test]
        public async Task ListBuildingsParsesAreaIds()
        {
            const string body = @"[{""id"":3,""slug"":""hcm"",""name"":""HCM"",""area_ids"":[""lantai-10""]},
                {""id"":4,""slug"":""lab"",""name"":""Lab"",""area_ids"":[]}]";
            var http = new FakeHttp(Reply(200, text: body));
            var buildings = await new CaptureApi(http, Auth).ListBuildings();

            Assert.That(http.Calls.Single().Method, Is.EqualTo("GET"));
            Assert.That(http.Calls.Single().Url, Is.EqualTo("https://capture.example.test/api/buildings"));
            AssertAuth(http.Calls.Single());
            Assert.That(buildings.Select(x => x.Id), Is.EqualTo(new[] { 3, 4 }));
            Assert.That(buildings[0].Slug, Is.EqualTo("hcm"));
            Assert.That(buildings[0].Name, Is.EqualTo("HCM"));
            Assert.That(buildings[0].AreaIds, Is.EqualTo(new[] { "lantai-10" }));
            Assert.That(buildings[1].AreaIds, Is.Empty);
        }

        [TestCase(3)]
        [TestCase(null)]
        public async Task CreateJobSendsBuildingIdOnlyWhenGiven(int? buildingId)
        {
            var http = new FakeHttp(Reply(201, text: "{\"id\":23}"));
            await new CaptureApi(http, Auth).CreateJob("lorong", "Lorong",
                new[] { ("peta.zip", "peta", 120L) }, buildingId);
            var body = JObject.Parse(Encoding.UTF8.GetString(http.Calls.Single().Body));
            if (buildingId.HasValue)
                Assert.That((int)body["building_id"], Is.EqualTo(buildingId.Value));
            else
                Assert.That(body.Property("building_id"), Is.Null);
        }

        [Test]
        public async Task UploadSendsChunksFromOffset()
        {
            var http = new FakeHttp(Created(), Offset(204, 50), Offset(204, 100), Offset(204, 120));
            await Upload(http);
            Assert.That(http.Calls[0].Url, Is.EqualTo("https://capture.example.test/files/"));
            Assert.That(http.Calls[0].Headers["Upload-Length"], Is.EqualTo("120"));
            Assert.That(http.Calls[0].Headers["Upload-Metadata"], Is.EqualTo("job_id MjM=,name cGV0YS56aXA="));
            foreach (var call in http.Calls)
            {
                AssertAuth(call);
                Assert.That(call.Headers["Tus-Resumable"], Is.EqualTo("1.0.0"));
            }
            var patches = http.Calls.Skip(1).ToArray();
            Assert.That(patches.All(c => c.Method == "PATCH" &&
                c.Url == "https://capture.example.test/files/upload-1" &&
                c.Headers["Content-Type"] == "application/offset+octet-stream"), Is.True);
            Assert.That(patches.Select(c => c.Headers["Upload-Offset"]), Is.EqualTo(new[] { "0", "50", "100" }));
            Assert.That(patches.Select(c => c.Body.Length), Is.EqualTo(new[] { 50, 50, 20 }));
            Assert.That(patches.SelectMany(c => c.Body), Is.EqualTo(File.ReadAllBytes(state.Path)));
        }

        [Test]
        public async Task ResumeUsesHeadOffset()
        {
            state.Url = "https://capture.example.test/files/old";
            state.Save(stateFile);
            state = UploadState.Load(stateFile);
            var http = new FakeHttp(Offset(200, 50), Offset(204, 100), Offset(204, 120));
            await Upload(http);
            Assert.That(http.Calls[0].Method, Is.EqualTo("HEAD"));
            Assert.That(http.Calls[1].Headers["Upload-Offset"], Is.EqualTo("50"));
            Assert.That(http.Calls[1].Body[0], Is.EqualTo(50));
        }

        [Test]
        public async Task ResumeDoesNotRecreateAConfirmedCompleteUpload()
        {
            state.Url = "https://capture.example.test/files/old";
            state.Offset = state.Size;
            state.Save(stateFile);
            state = UploadState.Load(stateFile);
            var http = new FakeHttp(Reply(404), Reply(409));

            await Upload(http);

            Assert.That(http.Calls, Is.Empty);
            Assert.That(state.Offset, Is.EqualTo(state.Size));
        }

        [TestCase(404)]
        [TestCase(410)]
        public async Task ResumeRecreatesWhenUploadGone(int status)
        {
            state.Url = "https://capture.example.test/files/old";
            state.Offset = 50;
            var http = new FakeHttp(Reply(status), Created("/files/new"),
                Offset(204, 50), Offset(204, 100), Offset(204, 120));
            await Upload(http);
            Assert.That(http.Calls.Take(3).Select(c => c.Method), Is.EqualTo(new[] { "HEAD", "POST", "PATCH" }));
            Assert.That(http.Calls[2].Url, Is.EqualTo("https://capture.example.test/files/new"));
            Assert.That(http.Calls[2].Headers["Upload-Offset"], Is.EqualTo("0"));
        }

        [Test]
        public async Task RetryableErrorsAreRetriedThenGiveUp()
        {
            state.Url = "https://capture.example.test/files/old";
            var delays = new List<TimeSpan>();
            var http = new FakeHttp(new[] { Offset(200, 100) }.Concat(Enumerable.Repeat(Reply(503), 5))
                .Append(Offset(204, 120)).ToArray());
            await Upload(http, duration => { delays.Add(duration); return Task.CompletedTask; });
            Assert.That(delays.Select(x => x.TotalSeconds), Is.EqualTo(new[] { 1, 2, 4, 8, 16 }));
            Assert.That(http.Calls.Count, Is.EqualTo(7));

            // Kasus kedua mensimulasikan unggahan yang belum selesai.
            state.Offset = 100;
            var unavailable = new FakeHttp(Enumerable.Repeat(Reply(503), 9).ToArray());
            var error = Assert.ThrowsAsync<CaptureHttpException>(async () => await Upload(unavailable));
            Assert.That(error.Kind, Is.EqualTo(FailureKind.Retryable));
            Assert.That(unavailable.Calls.Count, Is.EqualTo(8));
        }

        [TestCase(401, FailureKind.TokenRejected)]
        [TestCase(403, FailureKind.TokenRejected)]
        [TestCase(302, FailureKind.AccessRedirect)]
        public void TokenRejectedIsNotRetried(int status, FailureKind kind)
        {
            var http = new FakeHttp(Reply(status));
            var error = Assert.ThrowsAsync<CaptureHttpException>(async () => await Upload(http));
            Assert.That(error.Kind, Is.EqualTo(kind));
            Assert.That(http.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public void JobClosedOnCreateUpload()
        {
            var http = new FakeHttp(Reply(409, text: "{\"detail\":\"job is not uploading\"}"));
            var error = Assert.ThrowsAsync<CaptureHttpException>(async () => await Upload(http));
            Assert.That(error.Kind, Is.EqualTo(FailureKind.JobClosed));
            Assert.That(error.Status, Is.EqualTo(409));
            Assert.That(error.Detail, Is.EqualTo("job is not uploading"));
            Assert.That(http.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public void StateIsSavedAfterEachChunk()
        {
            var http = new FakeHttp(Created(), Offset(204, 50), Reply(400));
            Assert.ThrowsAsync<CaptureHttpException>(async () => await Upload(http));
            var kept = UploadState.Load(stateFile);
            Assert.That(kept.Offset, Is.EqualTo(50));
            Assert.That(kept.JobId, Is.EqualTo(23));
            Assert.That(kept.Url, Is.EqualTo("https://capture.example.test/files/upload-1"));
        }

        [Test]
        public async Task RetryResumesWhenServerAcceptedChunkButReplyWasLost()
        {
            var http = new FakeHttp(Created(), new HttpReply { NetworkError = true }, Reply(409),
                Offset(200, 50), Offset(204, 100), Offset(204, 120));
            await Upload(http);

            Assert.That(http.Calls.Select(c => c.Method),
                Is.EqualTo(new[] { "POST", "PATCH", "PATCH", "HEAD", "PATCH", "PATCH" }));
            Assert.That(http.Calls[4].Headers["Upload-Offset"], Is.EqualTo("50"));
            Assert.That(UploadState.Load(stateFile).Offset, Is.EqualTo(120));
        }

        [Test]
        public async Task GetJobReadsGateReasonsAndMapVersionFromServerContract()
        {
            const string body = @"{""id"":23,""status"":""done"",""stage"":""gate"",
                ""summary"":{""gate"":{""decision"":""hold"",""reasons"":[{""message"":""Foto uji kurang""}]}},
                ""map_version_id"":47,""stalled"":true}";
            var http = new FakeHttp(Reply(200, text: body));
            var job = await new CaptureApi(http, Auth).GetJob(23);
            Assert.That(http.Calls.Single().Url, Is.EqualTo("https://capture.example.test/api/jobs/23"));
            Assert.That(job.Id, Is.EqualTo(23));
            Assert.That(job.Status, Is.EqualTo("done"));
            Assert.That(job.Stage, Is.EqualTo("gate"));
            Assert.That(job.GateDecision, Is.EqualTo("hold"));
            Assert.That(job.GateReasons, Is.EqualTo(new[] { "Foto uji kurang" }));
            Assert.That(job.MapVersionId, Is.EqualTo(47));
            Assert.That(job.Stalled, Is.True);
        }

        [Test]
        public void UploadRejectsLocationOutsideServerOrigin()
        {
            var http = new FakeHttp(Created("https://other.example.test/files/foreign"));
            var error = Assert.ThrowsAsync<CaptureHttpException>(async () => await Upload(http));
            Assert.That(error.Kind, Is.EqualTo(FailureKind.Fatal));
            Assert.That(http.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResumeStateBelongsOnlyToMatchingJobAndPackage()
        {
            state.Url = "https://capture.example.test/files/old";
            state.Offset = 50;
            state.Save(stateFile);

            var kept = UploadState.LoadOrCreate(stateFile, 23, "peta.zip", state.Path, 120);
            Assert.That(kept.Offset, Is.EqualTo(50));
            Assert.That(kept.Url, Is.EqualTo(state.Url));
            var fresh = UploadState.LoadOrCreate(stateFile, 24, "peta.zip", state.Path, 120);
            Assert.That(fresh.JobId, Is.EqualTo(24));
            Assert.That(fresh.Url, Is.Null);
            Assert.That(fresh.Offset, Is.Zero);
        }
    }
}
