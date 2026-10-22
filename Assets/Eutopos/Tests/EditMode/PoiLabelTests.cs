using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class PoiLabelTests
    {
        [TestCase("Kamar Tidur", 3.24f, "Kamar Tidur, 3.2 m")]
        [TestCase("Lab", 0f, "Lab, 0.0 m")]
        [TestCase("Lab", 12.96f, "Lab, 13.0 m")]
        [TestCase("", 3.24f, "?, 3.2 m")]
        [TestCase(null, 3.24f, "?, 3.2 m")]
        public void FormatsNameAndDistance(string name, float distance, string expected)
        {
            Assert.That(PoiLabel.Format(name, distance), Is.EqualTo(expected));
        }
    }
}
