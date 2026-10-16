using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class FrameConvertTests
    {
        const float Eps = 1e-4f;

        static void Near(Vector3 a, Vector3 b) =>
            Assert.That(Vector3.Distance(a, b), Is.LessThan(Eps), $"{a} != {b}");

        [Test]
        public void GToBSwapsYAndZ()
        {
            Near(FrameConvert.GToB(new Vector3(1, 2, 3)), new Vector3(1, 3, 2));
            Near(FrameConvert.BToG(FrameConvert.GToB(new Vector3(1, 2, 3))), new Vector3(1, 2, 3));
        }

        [Test]
        public void CameraLookingAlongPlusXInGHasYaw90InB()
        {
            // kamera COLMAP: z depan. Putar 90 derajat di sekitar y_G supaya z_kamera = +x_G
            var r = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathFPi() / 2);
            var f = FrameConvert.ForwardB(r);
            Near(f, new Vector3(1, 0, 0));
            Assert.That(FrameConvert.YawDeg(f).Value, Is.EqualTo(90f).Within(Eps));
        }

        [Test]
        public void CameraLookingAlongPlusYInGHasYaw0InB()
        {
            // z_kamera = +y_G: putar -90 derajat di sekitar x_G
            var r = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathFPi() / 2);
            var f = FrameConvert.ForwardB(r);
            Near(f, new Vector3(0, 0, 1));
            Assert.That(FrameConvert.YawDeg(f).Value, Is.EqualTo(0f).Within(Eps));
        }

        [Test]
        public void SteepCameraHasNoYaw()
        {
            Assert.That(FrameConvert.YawDeg(new Vector3(0.1f, -1f, 0.1f)), Is.Null);
            Assert.That(FrameConvert.YawDeg(Vector3.Zero), Is.Null);
            Assert.That(FrameConvert.YawDeg(new Vector3(0.5f, -0.5f, 0.5f)), Is.Not.Null);
        }

        [TestCase(190f, -170f)]
        [TestCase(-190f, 170f)]
        [TestCase(540f, -180f)]
        [TestCase(0f, 0f)]
        public void WrapDegStaysInRange(float input, float expected)
        {
            Assert.That(FrameConvert.WrapDeg(input), Is.EqualTo(expected).Within(Eps));
        }

        [Test]
        public void YawDiffCrossesTheSeam()
        {
            Assert.That(FrameConvert.YawDiffDeg(170f, -170f), Is.EqualTo(20f).Within(Eps));
        }

        [Test]
        public void RotatedIntrinsicsMatchARotatedImage()
        {
            var k = new Intrinsics(1000f, 990f, 960f, 540f, 1920, 1080);
            var p = new Vector3(0.3f, -0.2f, 2.0f); // titik di depan kamera sensor
            var uv = k.Project(p);
            // citra diputar 90 derajat searah jarum jam: u' = H - v, v' = u; kamera ikut: X' = -Y, Y' = X
            var kr = k.RotatedClockwise();
            var uvr = kr.Project(new Vector3(-p.Y, p.X, p.Z));
            Assert.That(uvr.X, Is.EqualTo(k.Height - uv.Y).Within(1e-3f));
            Assert.That(uvr.Y, Is.EqualTo(uv.X).Within(1e-3f));
            Assert.That((kr.Width, kr.Height), Is.EqualTo((1080, 1920)));
        }

        [Test]
        public void ScaledIntrinsicsFollowTheImageSize()
        {
            var k = new Intrinsics(1000f, 1000f, 960f, 540f, 1920, 1080).ScaledTo(960, 540);
            Assert.That((k.Fx, k.Cx, k.Cy, k.Width), Is.EqualTo((500f, 480f, 270f, 960)));
        }

        [Test]
        public void ImageRotationIsClockwiseAndBottomUp()
        {
            // citra 3x2 atas-ke-bawah: [A B C] / [D E F], satu kanal bermakna per piksel
            byte[] src = Px('A', 'B', 'C', 'D', 'E', 'F');
            // putar CW -> 2x3: [D A] / [E B] / [F C]; disimpan bawah-ke-atas untuk Texture2D
            byte[] dst = ImageOps.RotateClockwiseBottomUp(src, 3, 2);
            Assert.That(Letters(dst), Is.EqualTo("FCEBDA"));
        }

        static byte[] Px(params char[] letters)
        {
            var b = new byte[letters.Length * 4];
            for (int i = 0; i < letters.Length; i++) b[i * 4] = (byte)letters[i];
            return b;
        }

        static string Letters(byte[] rgba)
        {
            var s = new char[rgba.Length / 4];
            for (int i = 0; i < s.Length; i++) s[i] = (char)rgba[i * 4];
            return new string(s);
        }

        static float MathFPi() => (float)System.Math.PI;
    }
}
