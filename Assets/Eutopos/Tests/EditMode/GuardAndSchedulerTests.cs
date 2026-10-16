using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class GuardAndSchedulerTests
    {
        static readonly Alignment Here = new Alignment(0f, Vector3.Zero);
        static Alignment Shifted(float metersX) => new Alignment(0f, new Vector3(-metersX, 0, 0));
        static readonly Vector3 Cam = new Vector3(1, 1.3f, 1);

        [Test]
        public void FirstCorrectionIsAlwaysAccepted()
        {
            var g = new JumpGuard();
            Assert.That(g.Evaluate(0f, null, Shifted(50f), Cam).Decision, Is.EqualTo(CorrectionDecision.Accepted));
        }

        [Test]
        public void SmallJumpIsAccepted()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            var r = g.Evaluate(5f, Here, Shifted(0.5f), Cam);
            Assert.That(r.Decision, Is.EqualTo(CorrectionDecision.Accepted));
            Assert.That(r.JumpM, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void BigRecentJumpBecomesACandidateThenIsConfirmed()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            Assert.That(g.Evaluate(5f, Here, Shifted(6f), Cam).Decision, Is.EqualTo(CorrectionDecision.Candidate));
            Assert.That(g.HasCandidate, Is.True);
            // koreksi berikutnya setuju dengan kandidat (selisih 0,4 m < 1 m)
            var r = g.Evaluate(10f, Here, Shifted(6.4f), Cam);
            Assert.That(r.Decision, Is.EqualTo(CorrectionDecision.Confirmed));
            Assert.That(g.HasCandidate, Is.False);
        }

        [Test]
        public void CandidateIsDroppedWhenTheNextCorrectionDisagrees()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            g.Evaluate(5f, Here, Shifted(6f), Cam);
            var r = g.Evaluate(10f, Here, Shifted(0.2f), Cam);
            Assert.That(r.Decision, Is.EqualTo(CorrectionDecision.Accepted));
            Assert.That(r.DroppedCandidate, Is.True);
            Assert.That(g.HasCandidate, Is.False);
        }

        [Test]
        public void BigJumpAfterALongGapIsAccepted()
        {
            var g = new JumpGuard();
            g.Evaluate(0f, null, Here, Cam);
            Assert.That(g.Evaluate(31f, Here, Shifted(6f), Cam).Decision, Is.EqualTo(CorrectionDecision.Accepted));
        }

        [Test]
        public void TimerNeverFiresWhileARequestIsInFlightOrTrackingIsLost()
        {
            var s = new CorrectionScheduler { Navigating = true, Tracking = true };
            Assert.That(s.ShouldFireTimer(0f), Is.True);
            Assert.That(s.TryStart(0f), Is.True);
            Assert.That(s.ShouldFireTimer(100f), Is.False);   // masih berjalan
            Assert.That(s.TryStart(100f), Is.False);          // tombol juga ditolak
            s.Finish();
            Assert.That(s.ShouldFireTimer(3f), Is.False);     // belum 5 detik sejak mulai
            Assert.That(s.ShouldFireTimer(5f), Is.True);
            s.Tracking = false;
            Assert.That(s.ShouldFireTimer(50f), Is.False);
            Assert.That(s.TryStart(50f), Is.False);
        }

        [Test]
        public void TimerIsSilentWhenNotNavigatingButTheButtonWorks()
        {
            var s = new CorrectionScheduler { Navigating = false, Tracking = true };
            Assert.That(s.ShouldFireTimer(100f), Is.False);
            Assert.That(s.TryStart(100f), Is.True);
        }
    }
}
