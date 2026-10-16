using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class SmokeTests
    {
        [Test]
        public void SystemNumericsIsAvailable()
        {
            Assert.That(Vector3.Transform(Vector3.UnitZ, Quaternion.Identity), Is.EqualTo(Vector3.UnitZ));
        }
    }
}
