using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class Aligner4DofTests
    {
        const float Eps = 1e-3f;

        static void Near(Vector3 a, Vector3 b) =>
            Assert.That(Vector3.Distance(a, b), Is.LessThan(Eps), $"{a} != {b}");

        [Test]
        public void RotYMatchesUnityConvention()
        {
            Near(Aligner4Dof.RotY(90f, new Vector3(0, 0, 1)), new Vector3(1, 0, 0));
            Near(Aligner4Dof.RotY(90f, new Vector3(1, 0, 0)), new Vector3(0, 0, -1));
            Near(Aligner4Dof.RotY(45f, new Vector3(0, 2, 0)), new Vector3(0, 2, 0));
        }

        [TestCase(30f, 1f, 0.2f, -4f)]
        [TestCase(-120f, -7.5f, 1.4f, 2f)]
        [TestCase(179f, 0f, 0f, 0f)]
        public void SolveRecoversTheTrueAlignment(float yaw, float tx, float ty, float tz)
        {
            var truth = new Alignment(yaw, new Vector3(tx, ty, tz));
            var camPosB = new Vector3(4.5f, 1.3f, 1.6f);
            var camFwdB = Vector3.Normalize(new Vector3(0.6f, -0.2f, 0.8f));
            var camPosU = truth.BToU(camPosB);
            var camFwdU = Aligner4Dof.RotY(yaw, camFwdB);

            var got = Aligner4Dof.Solve(camPosU, camFwdU, camPosB, camFwdB);

            Assert.That(got, Is.Not.Null);
            Assert.That(FrameConvert.YawDiffDeg(got.Value.YawDeg, yaw), Is.LessThan(Eps));
            Near(got.Value.Translation, truth.Translation);
        }

        [Test]
        public void YawAcrossTheSeamIsTheShortWay()
        {
            var got = Aligner4Dof.Solve(
                Vector3.Zero, Aligner4Dof.RotY(170f, Vector3.UnitZ),
                Vector3.Zero, Aligner4Dof.RotY(-170f, Vector3.UnitZ));
            Assert.That(got.Value.YawDeg, Is.EqualTo(-20f).Within(Eps));
        }

        [Test]
        public void RoundTripBetweenFrames()
        {
            var a = new Alignment(63f, new Vector3(2, -1, 5));
            var p = new Vector3(3, 0.4f, -2);
            Near(a.UToB(a.BToU(p)), p);
        }

        [Test]
        public void SteepCameraCannotBeAligned()
        {
            Assert.That(Aligner4Dof.Solve(Vector3.Zero, -Vector3.UnitY, Vector3.Zero, Vector3.UnitZ), Is.Null);
        }
    }
}
