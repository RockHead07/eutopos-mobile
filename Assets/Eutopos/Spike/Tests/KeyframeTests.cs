using Eutopos.App;
using Eutopos.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Eutopos.Spike.Tests
{
    public sealed class KeyframeTests
    {
        [TestCase(TrackingState.None, 0f, false, 1f, 90f, KeyframeDecision.NotTracking)]
        [TestCase(TrackingState.Limited, 0f, true, 1f, 90f, KeyframeDecision.NotTracking)]
        [TestCase(TrackingState.Tracking, 0.71f, false, 1f, 90f, KeyframeDecision.TooFast)]
        [TestCase(TrackingState.Tracking, 0.7f, false, 0f, 0f, KeyframeDecision.Capture)]
        [TestCase(TrackingState.Tracking, 0.7f, true, 0.4f, 0f, KeyframeDecision.Capture)]
        [TestCase(TrackingState.Tracking, 0f, true, 0f, 10f, KeyframeDecision.Capture)]
        [TestCase(TrackingState.Tracking, 0f, true, 0.39f, 9.9f, KeyframeDecision.BelowThreshold)]
        public void KeyframesUseTrackingSpeedAndEitherMovementThreshold(TrackingState tracking,
            float speed, bool hasPrevious, float distance, float angle, KeyframeDecision expected)
        {
            Assert.That(Keyframes.Decide(tracking, speed, Vector3.right * distance,
                Quaternion.AngleAxis(angle, Vector3.up), hasPrevious, Vector3.zero,
                Quaternion.identity), Is.EqualTo(expected));
        }

        [Test]
        public void QuaternionSignAloneIsNotMovement()
        {
            Assert.That(Keyframes.Decide(TrackingState.Tracking, 0, Vector3.zero,
                new Quaternion(0, 0, 0, -1), true, Vector3.zero, Quaternion.identity),
                Is.EqualTo(KeyframeDecision.BelowThreshold));
        }

        [Test]
        public void FrameLineKeepsNanosecondsPoseAndPinholeFields()
        {
            const long stamp = 12_345_678_901L;
            var frame = new CapturedFrame
            {
                TimestampNs = stamp,
                CamPosU = new Vector3(1, 2, -3),
                CamRotU = new Quaternion(0.5f, -0.5f, 0.5f, 0.5f),
                K = new Intrinsics(600, 610, 299.5f, 401.25f, 600, 800),
            };

            string line = Keyframes.FrameLine("000001.jpg", frame, TrackingState.Tracking);
            StringAssert.DoesNotContain("\n", line);
            var data = JObject.Parse(line);
            Assert.That(data["file"].Value<string>(), Is.EqualTo("000001.jpg"));
            Assert.That(data["t_ns"].Value<long>(), Is.EqualTo(stamp));
            Assert.That(data["tracking"].Value<string>(), Is.EqualTo("Tracking"));
            Assert.That(data["position"].ToObject<float[]>(), Is.EqualTo(new[] { 1f, 2f, -3f }));
            Assert.That(data["rotation_xyzw"].ToObject<float[]>(),
                Is.EqualTo(new[] { 0.5f, -0.5f, 0.5f, 0.5f }));
            var k = data["K"];
            Assert.That(k["fx"].Value<float>(), Is.EqualTo(600f));
            Assert.That(k["fy"].Value<float>(), Is.EqualTo(610f));
            Assert.That(k["cx"].Value<float>(), Is.EqualTo(299.5f));
            Assert.That(k["cy"].Value<float>(), Is.EqualTo(401.25f));
            Assert.That(k["width"].Value<int>(), Is.EqualTo(600));
            Assert.That(k["height"].Value<int>(), Is.EqualTo(800));
        }
    }
}
