using Eutopos.Core.Capture;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public sealed class JobStatusViewTests
    {
        [TestCase("uploading", null, null, JobView.Uploading, "Mengunggah")]
        [TestCase("queued", null, null, JobView.Queued, "Antre")]
        [TestCase("running", "build", null, JobView.Processing, "Memproses (build)")]
        [TestCase("done", null, "publish", JobView.ReadyToTest, "Siap diuji")]
        [TestCase("deleting", null, null, JobView.Deleting, "Dihapus")]
        public void MapsJobStatus(string status, string stage, string gate, JobView expected, string label)
        {
            var result = JobStatusView.Map(new JobSnapshot { Status = status, Stage = stage, GateDecision = gate });
            Assert.That(result.view, Is.EqualTo(expected));
            Assert.That(result.text, Is.EqualTo(label));
        }

        [Test]
        public void HoldShowsBothReasons()
        {
            var result = JobStatusView.Map(new JobSnapshot
            {
                Status = "done", GateDecision = "hold", GateReasons = new[] { "Foto kurang", "Peta terputus" },
            });
            Assert.That(result.view, Is.EqualTo(JobView.Held));
            Assert.That(result.text, Is.EqualTo("Ditahan: Foto kurang; Peta terputus"));
        }

        [Test]
        public void FailureShowsOnlyFirstLine()
        {
            var result = JobStatusView.Map(new JobSnapshot { Status = "failed", Error = "Foto rusak\r\nbaris teknis" });
            Assert.That(result.view, Is.EqualTo(JobView.Failed));
            Assert.That(result.text, Is.EqualTo("Gagal: Foto rusak"));
        }

        [Test]
        public void StalledAddsMarker()
        {
            var result = JobStatusView.Map(new JobSnapshot { Status = "running", Stage = "build", Stalled = true });
            Assert.That(result.text, Is.EqualTo("Memproses (build) (macet)"));
        }
    }
}
