using System;
using System.Linq;
using System.Numerics;
using Eutopos.Core.Capture;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public sealed class CaptureRecordsTests
    {
        static readonly DateTime Now = new DateTime(2026, 11, 23, 10, 11, 12, DateTimeKind.Utc);

        [TestCase("Lorong Lantai 10 / Sayap B", "lorong-lantai-10-sayap-b")]
        [TestCase("  ---  ", "area-20261123-1011")]
        [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA - lanjutan", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        public void AreaNameProducesServerSlug(string name, string expected)
        {
            string id = AreaId.FromName(name, Now);
            Assert.That(id, Is.EqualTo(expected));
            Assert.That(AreaId.IsValid(id), Is.True);
        }

        [Test]
        public void LongAndRandomNamesAlwaysProduceValidIds()
        {
            var random = new Random(16391);
            string id = AreaId.FromName(new string('A', 60), Now);
            Assert.That(id.Length, Is.EqualTo(40));
            Assert.That(AreaId.IsValid(id), Is.True);
            const string alphabet = "abXY09 ,-_/\n/Éç日本😀";
            for (int i = 0; i < 200; i++)
            {
                string name = new string(Enumerable.Range(0, random.Next(90))
                    .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
                Assert.That(AreaId.IsValid(AreaId.FromName(name, Now)), Is.True);
            }
        }

        [Test]
        public void FrameRoundTripKeepsNanosecondsAndPose()
        {
            var record = new FrameRecord
            {
                File = "000001.jpg", TimestampNs = 12_345_678_901L,
                Position = new Vector3(1, 2, -3), Rotation = new Quaternion(0.5f, -0.5f, 0.5f, 0.5f),
                Tracking = "Tracking", K = new Intrinsics(600, 610, 299.5f, 401.25f, 600, 800), Segment = 3,
            };
            string line = FrameRecord.ToLine(record);
            StringAssert.DoesNotContain("\n", line);
            var json = JObject.Parse(line);
            Assert.That(json["t_ns"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(json["t_ns"].Value<long>(), Is.EqualTo(12_345_678_901L));
            Assert.That(json["position"].ToObject<float[]>(), Is.EqualTo(new[] { 1f, 2f, -3f }));
            Assert.That(json["rotation_xyzw"].ToObject<float[]>(), Is.EqualTo(new[] { 0.5f, -0.5f, 0.5f, 0.5f }));
            Assert.That(json["K"]["height"].Value<int>(), Is.EqualTo(800));
            var decoded = FrameRecord.FromLine(line);
            Assert.That(decoded.File, Is.EqualTo(record.File));
            Assert.That(decoded.TimestampNs, Is.EqualTo(record.TimestampNs));
            Assert.That(decoded.Position, Is.EqualTo(record.Position));
            Assert.That(decoded.Rotation, Is.EqualTo(record.Rotation));
            Assert.That(decoded.Tracking, Is.EqualTo(record.Tracking));
            Assert.That(decoded.K, Is.EqualTo(record.K));
            Assert.That(decoded.Segment, Is.EqualTo(record.Segment));
        }

        [Test]
        public void MetaDeclaresCoordinateConventionAndPhotoCount()
        {
            var meta = JObject.Parse(CaptureMeta.Json("1.0", "Ponsel uji", Now, 7, 2, "peta"));
            Assert.That(meta["coordinate_system"].Value<string>(), Is.EqualTo("unity_world"));
            Assert.That(meta["up_axis"].Value<string>(), Is.EqualTo("Y"));
            Assert.That(meta["handedness"].Value<string>(), Is.EqualTo("left"));
            Assert.That(meta["photo_count"].Value<int>(), Is.EqualTo(7));
        }
    }
}
