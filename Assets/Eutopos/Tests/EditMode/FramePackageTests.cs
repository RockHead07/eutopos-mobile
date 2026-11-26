using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using Eutopos.Core.Capture;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public sealed class FramePackageTests
    {
        static readonly byte[] Jpeg = { 0xff, 0xd8, 0xff, 0xd9 };
        string root;
        CaptureSession session;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "eutopos-zip-" + Guid.NewGuid().ToString("N"));
            session = CaptureSession.Create(root, "Lorong", "lorong", new DateTime(2026, 11, 23));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void PackageContainsOnlyExpectedEntries()
        {
            for (int i = 0; i < 3; i++)
                session.Add("peta", Jpeg, new FrameRecord
                {
                    Position = Vector3.UnitX * i, Rotation = Quaternion.Identity, Tracking = "Tracking",
                    K = new Intrinsics(600, 610, 300, 400, 600, 800),
                });
            string path = FramePackage.Write(session, "peta",
                CaptureMeta.Json("1.0", "Ponsel uji", DateTime.UtcNow, 3, 0, "peta"), "{}");

            Assert.That(path, Is.EqualTo(Path.Combine(session.Dir, "peta.zip")));
            using (var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
            {
                var imageNames = new[] { "images/000001.jpg", "images/000002.jpg", "images/000003.jpg" };
                Assert.That(archive.Entries.Select(e => e.FullName),
                    Is.EquivalentTo(imageNames.Concat(new[] { "frames.jsonl", "meta.json", "configs.json" })));
                Assert.That(archive.Entries.All(e => !e.FullName.EndsWith("/") && !e.FullName.Contains("\\")),
                    Is.True);
                using var frames = new StreamReader(archive.GetEntry("frames.jsonl").Open());
                var lines = frames.ReadToEnd().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                Assert.That(lines.Select(x => "images/" + FrameRecord.FromLine(x).File),
                    Is.EquivalentTo(imageNames));
                using var meta = new StreamReader(archive.GetEntry("meta.json").Open());
                var resolution = JObject.Parse(meta.ReadToEnd())["resolution"];
                Assert.That(resolution["width"].Value<int>(), Is.EqualTo(600));
                Assert.That(resolution["height"].Value<int>(), Is.EqualTo(800));
            }

            if (Environment.GetEnvironmentVariable("EUTOPOS_KEEP_PACKAGE") == "1")
                File.Copy(path, Path.Combine(Path.GetTempPath(), "eutopos-paket-uji.zip"), true);
        }

        [Test]
        public void EmptyRoleCannotBePackaged()
        {
            Assert.Throws<InvalidOperationException>(() => FramePackage.Write(session, "uji", "{}", "{}"));
            Assert.That(File.Exists(Path.Combine(session.Dir, "uji.zip")), Is.False);
        }

        string WriteSample()
        {
            var record = new FrameRecord
            {
                Rotation = Quaternion.Identity, Tracking = "Tracking",
                K = new Intrinsics(600, 610, 300, 400, 600, 800),
            };
            session.Add("peta", Jpeg, record);
            return FramePackage.Write(session, "peta", "{}", "{}");
        }

        [Test]
        public void ZipEntriesAreStoredOrDeflated()
        {
            string path = WriteSample();
            using var reader = new BinaryReader(File.OpenRead(path));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(reader.ReadUInt32(), Is.EqualTo(0x04034b50u));
                reader.ReadUInt16(); // Versi ekstraksi
                ushort flags = reader.ReadUInt16();
                Assert.That(reader.ReadUInt16(), Is.EqualTo(0).Or.EqualTo(8));
                Assert.That(flags & 8, Is.Zero, "Ukuran ada pada header lokal di stream seekable.");
                reader.BaseStream.Seek(8, SeekOrigin.Current); // Tanggal, waktu, CRC
                uint size = reader.ReadUInt32();
                reader.ReadUInt32(); // Ukuran tanpa kompresi
                int nameLength = reader.ReadUInt16();
                int extraLength = reader.ReadUInt16();
                reader.BaseStream.Seek(size + nameLength + extraLength, SeekOrigin.Current);
            }
            Assert.That(reader.ReadUInt32(), Is.EqualTo(0x02014b50u));
        }

        [Test]
        public void EntriesRoundTripByteForByte()
        {
            string path = WriteSample();
            using var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read);
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("images/")))
            {
                using var source = entry.Open();
                using var contents = new MemoryStream();
                source.CopyTo(contents);
                Assert.That(contents.ToArray(),
                    Is.EqualTo(File.ReadAllBytes(Path.Combine(session.Dir, "peta", entry.FullName))));
            }
        }

        [Test]
        public void PrepareForUploadReusesExistingPackageBytes()
        {
            string path = WriteSample();
            byte[] original = File.ReadAllBytes(path);
            string reused = FramePackage.PrepareForUpload(session, "peta", "{\"changed\":true}", "{}");
            Assert.That(reused, Is.EqualTo(path));
            Assert.That(File.ReadAllBytes(reused), Is.EqualTo(original));
        }
    }
}
