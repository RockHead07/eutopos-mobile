using System;
using System.Numerics;
using Eutopos.Core.Capture;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public sealed class CapturePolicyTests
    {
        [TestCase(false, 0f, false, 1f, 90f, ShotDecision.NotTracking)]
        [TestCase(false, 0f, true, 1f, 90f, ShotDecision.NotTracking)]
        [TestCase(true, 0.71f, false, 1f, 90f, ShotDecision.TooFast)]
        [TestCase(true, 0.7f, false, 0f, 0f, ShotDecision.Capture)]
        [TestCase(true, 0.7f, true, 0.4f, 0f, ShotDecision.Capture)]
        [TestCase(true, 0f, true, 0f, 10f, ShotDecision.Capture)]
        [TestCase(true, 0f, true, 0.39f, 9.9f, ShotDecision.BelowThreshold)]
        public void AutoUsesTrackingSpeedAndEitherMovementThreshold(bool tracking, float speed,
            bool hasPrevious, float distance, float angle, ShotDecision expected)
        {
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, angle * (float)Math.PI / 180f);
            Assert.That(CapturePolicy.Auto(tracking, speed, Vector3.UnitX * distance, rotation,
                hasPrevious, Vector3.Zero, Quaternion.Identity, 0, CapturePolicy.MinFreeBytes),
                Is.EqualTo(expected));
        }

        [Test]
        public void QuaternionSignAloneIsNotMovement()
        {
            Assert.That(CapturePolicy.Auto(true, 0, Vector3.Zero, new Quaternion(0, 0, 0, -1),
                true, Vector3.Zero, Quaternion.Identity, 0, CapturePolicy.MinFreeBytes),
                Is.EqualTo(ShotDecision.BelowThreshold));
        }

        [TestCase(false, 0.8f, 1500, 0, ShotDecision.NotTracking)]
        [TestCase(true, 0.8f, 1500, 0, ShotDecision.LimitReached)]
        [TestCase(true, 0.8f, 1499, (1L << 30) - 1, ShotDecision.LowStorage)]
        public void AutoChecksLimitsBeforeMovement(bool tracking, float speed, int count, long free,
            ShotDecision expected)
        {
            Assert.That(CapturePolicy.Auto(tracking, speed, Vector3.Zero, Quaternion.Identity,
                false, Vector3.Zero, Quaternion.Identity, count, free), Is.EqualTo(expected));
        }

        [TestCase(true, 0f, ShotDecision.Capture)]
        [TestCase(false, 0f, ShotDecision.NotTracking)]
        [TestCase(true, 0.71f, ShotDecision.TooFast)]
        public void ManualRequiresTrackingAndSafeSpeed(bool tracking, float speed, ShotDecision expected)
        {
            Assert.That(CapturePolicy.Manual(tracking, speed, 0, CapturePolicy.MinFreeBytes),
                Is.EqualTo(expected));
        }

        [TestCase(0.99f, ShotDecision.TooCloseToTest)]
        [TestCase(1f, ShotDecision.Capture)]
        public void TestShotChecksAllPreviousTestPositions(float distance, ShotDecision expected)
        {
            Assert.That(CapturePolicy.TestShot(true, 0, Vector3.UnitX * distance,
                new[] { -Vector3.UnitX, Vector3.Zero }), Is.EqualTo(expected));
        }

        [Test]
        public void FirstTestShotHasNoSpacingRestriction()
        {
            Assert.That(CapturePolicy.TestShot(true, 0, Vector3.Zero, Array.Empty<Vector3>()),
                Is.EqualTo(ShotDecision.Capture));
        }

        [Test]
        public void TrackingLossStartsOneNewSegment()
        {
            var continuity = new PoseContinuity();
            Assert.That(continuity.Observe(Vector3.Zero, 0, true), Is.True);
            Assert.That(continuity.Observe(Vector3.Zero, 0.2, false), Is.False);
            Assert.That(continuity.Segment, Is.EqualTo(1));
            Assert.That(continuity.Event, Is.EqualTo("tracking_lost"));
            Assert.That(continuity.Observe(Vector3.Zero, 0.4, false), Is.False);
            Assert.That(continuity.Segment, Is.EqualTo(1));
        }

        [Test]
        public void FastPositionJumpStartsNewSegment()
        {
            var continuity = new PoseContinuity();
            continuity.Observe(Vector3.Zero, 0, true);
            Assert.That(continuity.Observe(new Vector3(0.6f, 0, 0), 0.2, true), Is.False);
            Assert.That(continuity.Segment, Is.EqualTo(1));
            Assert.That(continuity.Event, Is.EqualTo("position_jump"));
        }

        [Test]
        public void NormalWalkingKeepsSegment()
        {
            var continuity = new PoseContinuity();
            continuity.Observe(Vector3.Zero, 0, true);
            Assert.That(continuity.Observe(new Vector3(0.1f, 0, 0), 0.2, true), Is.True);
            Assert.That(continuity.Speed, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(continuity.Segment, Is.Zero);
            Assert.That(continuity.Event, Is.Null);
        }

        [Test]
        public void DistanceAddsEachConsecutivePhotoPosition()
        {
            var frames = new[]
            {
                new FrameRecord { Position = Vector3.Zero },
                new FrameRecord { Position = 3 * Vector3.UnitX },
                new FrameRecord { Position = 3 * Vector3.UnitX + 4 * Vector3.UnitY },
            };
            Assert.That(CapturePolicy.DistanceM(frames), Is.EqualTo(7f));
            Assert.That(CapturePolicy.DistanceM(Array.Empty<FrameRecord>()), Is.Zero);
        }

        [Test]
        public void AutoWaitsForOneStableSecondAfterTrackingLossOrJump()
        {
            var gate = new CaptureTrackingGate();
            Assert.That(gate.Observe(0, false, false), Is.False);
            Assert.That(gate.Observe(1, true, false), Is.False);
            Assert.That(gate.Observe(1.99, true, false), Is.False);
            Assert.That(gate.Observe(2, true, false), Is.True);
            Assert.That(gate.Observe(3, true, true), Is.False);
            Assert.That(gate.Observe(3.1, true, false), Is.False);
            Assert.That(gate.Observe(4.1, true, false), Is.True);
        }

        [Test]
        public void NewAreaChecksAllBuildings()
        {
            var buildings = new[]
            {
                new BuildingInfo { Id = 3, AreaIds = new[] { "lorong" } },
                new BuildingInfo { Id = 4, AreaIds = new[] { "ruang" } },
            };
            Assert.That(CapturePolicy.AreaExists("ruang", buildings), Is.True);
            Assert.That(CapturePolicy.AreaExists("ruang-baru", buildings), Is.False);
        }
    }
}
