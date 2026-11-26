using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class PoiPointerLayoutTests
    {
        [Test]
        public void ScaleForHeightMatchesTargetHeight()
        {
            float boundsHeight = 2.554105f - (-1.969749f);
            float scale = PoiPointerLayout.ScaleForHeight(boundsHeight, 0.35f);
            Assert.That(boundsHeight * scale, Is.EqualTo(0.35f).Within(1e-5f));
        }

        [TestCase(0f, 0.35f)]
        [TestCase(-1f, 0.35f)]
        [TestCase(4.5f, 0f)]
        [TestCase(4.5f, -0.35f)]
        [TestCase(float.NaN, 0.35f)]
        [TestCase(float.PositiveInfinity, 0.35f)]
        public void ScaleForHeightReturnsZeroOnInvalidInput(float boundsHeight, float targetHeight)
        {
            Assert.That(PoiPointerLayout.ScaleForHeight(boundsHeight, targetHeight), Is.EqualTo(0f));
        }

        [Test]
        public void TipLocalOffsetPlacesBoundsMinAtZero()
        {
            float minY = -1.969749f;
            float maxY = 2.554105f;
            float scale = PoiPointerLayout.ScaleForHeight(maxY - minY, 0.35f);
            float offsetY = PoiPointerLayout.TipLocalOffsetY(minY, scale);

            Assert.That(minY * scale + offsetY, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(maxY * scale + offsetY, Is.EqualTo(0.35f).Within(1e-5f));
        }

        [Test]
        public void LabelLiftAddsClearanceAboveTop()
        {
            Assert.That(PoiPointerLayout.LabelLiftForHeight(0.35f, 0.23f), Is.EqualTo(0.58f).Within(1e-5f));
            Assert.That(PoiPointerLayout.LabelLiftForHeight(0.10f, 0.18f), Is.EqualTo(0.28f).Within(1e-5f));
        }
    }
}
