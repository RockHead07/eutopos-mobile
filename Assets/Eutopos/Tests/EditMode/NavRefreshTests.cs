using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class NavRefreshTests
    {
        [TestCase("a", "b", false, true)]
        [TestCase("a", "a", false, false)]
        [TestCase("a", "b", true, false)]
        [TestCase("a", null, false, false)]
        [TestCase(null, "b", false, true)]
        public void ReplacesOnlyWhenTheRevisionChangedAndNotNavigating(string current, string incoming, bool navigating, bool expected)
        {
            Assert.That(NavRefresh.ShouldReplace(current, incoming, navigating), Is.EqualTo(expected));
        }
    }
}
