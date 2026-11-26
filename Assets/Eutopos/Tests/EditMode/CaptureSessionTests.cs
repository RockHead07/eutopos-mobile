using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Eutopos.Core.Capture;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public sealed class CaptureSessionTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 23, 10, 11, 12, DateTimeKind.Utc);
        static readonly byte[] Jpeg = { 0xff, 0xd8, 0xff, 0xd9 };
        string root;
        CaptureSession session;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "eutopos-capture-" + Guid.NewGuid().ToString("N"));
            session = CaptureSession.Create(root, "Lorong", "lorong", Now);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        string Frames => Path.Combine(session.Dir, "peta", "frames.jsonl");
        string Image(string file) => Path.Combine(session.Dir, "peta", "images", file);

        void Add(int count)
        {
            for (int i = 0; i < count; i++)
                session.Add("peta", Jpeg, Record());
        }

        static FrameRecord Record() => new FrameRecord
        {
            Position = Vector3.Zero, Rotation = Quaternion.Identity, Tracking = "Tracking",
            K = new Intrinsics(600, 610, 300, 400, 600, 800),
        };

        [Test]
        public void AddThenUndoKeepsNumberingGapless()
        {
            Add(3);
            Assert.That(session.UndoLast("peta"), Is.True);
            Assert.That(session.Add("peta", Jpeg, Record()), Is.EqualTo("000003.jpg"));
            string[] files = Directory.GetFiles(Path.GetDirectoryName(Image("000001.jpg")))
                .Select(Path.GetFileName).OrderBy(x => x).ToArray();
            Assert.That(files, Is.EqualTo(new[] { "000001.jpg", "000002.jpg", "000003.jpg" }));
            string[] lines = File.ReadAllLines(Frames);
            Assert.That(lines.Length, Is.EqualTo(3));
            Assert.That(FrameRecord.FromLine(lines[2]).File, Is.EqualTo("000003.jpg"));
        }

        [Test]
        public void UndoOnEmptyReturnsFalse()
        {
            Assert.That(session.UndoLast("peta"), Is.False);
            Assert.That(session.Count("peta"), Is.Zero);
            Assert.That(Directory.Exists(Path.GetDirectoryName(Image("000001.jpg"))), Is.True);
            Assert.That(File.ReadAllText(Frames), Is.Empty);
        }

        [Test]
        public void OpenDropsJpegWithoutLine()
        {
            Add(3);
            File.WriteAllBytes(Image("000004.jpg"), Jpeg);
            var reopened = CaptureSession.Open(session.Dir);
            Assert.That(reopened.Count("peta"), Is.EqualTo(3));
            Assert.That(File.Exists(Image("000004.jpg")), Is.False);
            Assert.That(reopened.RecoveryNote, Is.Not.Null);
        }

        [Test]
        public void OpenDropsTruncatedLastLine()
        {
            Add(3);
            File.AppendAllText(Frames, "{\"file\":");
            var reopened = CaptureSession.Open(session.Dir);
            Assert.That(reopened.Count("peta"), Is.EqualTo(3));
            Assert.That(File.ReadAllLines(Frames).Length, Is.EqualTo(3));
            Assert.That(reopened.RecoveryNote, Is.Not.Null);
        }

        [Test]
        public void OpenDropsLineWhoseJpegIsEmpty()
        {
            Add(3);
            File.WriteAllBytes(Image("000003.jpg"), Array.Empty<byte>());
            Assert.That(CaptureSession.Open(session.Dir).Count("peta"), Is.EqualTo(2));
            Assert.That(File.Exists(Image("000003.jpg")), Is.False);
        }

        [Test]
        public void OpenTrimsAtFirstTruncatedJpegAndRemovesTemporaryFiles()
        {
            Add(4);
            File.WriteAllBytes(Image("000003.jpg"), new byte[] { 0xff, 0xd8, 0xff, 0xe0 });
            File.WriteAllBytes(Image("000005.jpg.tmp"), Jpeg);
            var reopened = CaptureSession.Open(session.Dir);
            Assert.That(reopened.Count("peta"), Is.EqualTo(2));
            Assert.That(File.Exists(Image("000004.jpg")), Is.False);
            Assert.That(File.Exists(Image("000005.jpg.tmp")), Is.False);
            Assert.That(reopened.Add("peta", Jpeg, Record()), Is.EqualTo("000003.jpg"));
        }

        [Test]
        public void UndoAfterReopenWorks()
        {
            Add(3);
            var reopened = CaptureSession.Open(session.Dir);
            Assert.That(reopened.UndoLast("peta"), Is.True);
            Assert.That(reopened.Count("peta"), Is.EqualTo(2));
            Assert.That(CaptureSession.Open(session.Dir).Add("peta", Jpeg, Record()),
                Is.EqualTo("000003.jpg"));
        }

        [Test]
        public void UnfinishedListsNewestFirstAndSkipsSubmitted()
        {
            var newer = CaptureSession.Create(root, "Ruang", "ruang", Now.AddSeconds(1));
            newer.SetStage(SessionStage.Uploading);
            newer.SetJobId(12);
            var submitted = CaptureSession.Create(root, "Beres", "beres", Now.AddSeconds(2));
            submitted.SetStage(SessionStage.Submitted);

            Assert.That(CaptureSession.Unfinished(root), Is.EqualTo(new[] { newer.Dir, session.Dir }));
            var open = CaptureSession.Open(newer.Dir);
            Assert.That(open.Stage, Is.EqualTo(SessionStage.Uploading));
            Assert.That(open.JobId, Is.EqualTo(12));
            Assert.That(open.AreaName, Is.EqualTo("Ruang"));
            Assert.That(open.AreaId, Is.EqualTo("ruang"));
        }

        [Test]
        public void RolesAreIndependent()
        {
            Add(3);
            Assert.That(session.Add("uji", Jpeg, Record()), Is.EqualTo("000001.jpg"));
            Assert.That(session.Count("peta"), Is.EqualTo(3));
            Assert.That(CaptureSession.Open(session.Dir).Count("uji"), Is.EqualTo(1));
        }

        [Test]
        public void BuildingIdRoundTripsAndDefaultsToNull()
        {
            var created = CaptureSession.Create(root, "Ruang", "ruang", 3, Now.AddSeconds(3));
            Assert.That(CaptureSession.Open(created.Dir).BuildingId, Is.EqualTo(3));

            var manifest = Path.Combine(session.Dir, "session.json");
            File.WriteAllText(manifest, "{\"area_id\":\"lorong\",\"area_name\":\"Lorong\",\"stage\":0}");
            Assert.That(CaptureSession.Open(session.Dir).BuildingId, Is.Null);
        }

        [Test]
        public void ResetUploadClearsJobAndOffsetsButKeepsPackages()
        {
            session.SetJobId(23);
            session.SetStage(SessionStage.Uploading);
            string state = Path.Combine(session.Dir, "upload-peta.zip.json");
            string package = Path.Combine(session.Dir, "peta.zip");
            File.WriteAllText(state, "{}");
            File.WriteAllBytes(package, Jpeg);
            session.ResetUpload();

            var reopened = CaptureSession.Open(session.Dir);
            Assert.That(reopened.JobId, Is.Null);
            Assert.That(reopened.Stage, Is.EqualTo(SessionStage.Packaged));
            Assert.That(File.Exists(state), Is.False);
            Assert.That(File.ReadAllBytes(package), Is.EqualTo(Jpeg));
        }

        [Test]
        public void SubmittedSessionsCanBeListedSeparately()
        {
            var submitted = CaptureSession.Create(root, "Beres", "beres", Now.AddSeconds(5));
            submitted.SetJobId(23);
            submitted.SetStage(SessionStage.Submitted);
            Assert.That(CaptureSession.Submitted(root), Is.EqualTo(new[] { submitted.Dir }));
            Assert.That(CaptureSession.Unfinished(root), Is.EqualTo(new[] { session.Dir }));
        }

        [Test]
        public void SessionMetadataKeepsStartTimeAndTrackingLosses()
        {
            session.RecordTrackingLoss();
            var reopened = CaptureSession.Open(session.Dir);
            Assert.That(reopened.StartedUtc, Is.EqualTo(Now));
            Assert.That(reopened.TrackingLosses, Is.EqualTo(1));
        }
    }
}
