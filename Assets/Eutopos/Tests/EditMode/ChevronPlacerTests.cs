using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class ChevronPlacerTests
    {
        const float Spacing = 0.9f, MaxAhead = 8f, Fade = 2f;

        static Vector2[] Path(params float[] xy) =>
            Enumerable.Range(0, xy.Length / 2).Select(i => new Vector2(xy[2 * i], xy[2 * i + 1])).ToArray();

        static List<Chevron> Place(Vector2[] pts, float phase = 0f) => ChevronPlacer.Place(pts, Spacing, phase, MaxAhead, Fade);

        [Test]
        public void StraightRouteGivesEightArrowsFacingTheGoal()
        {
            var r = Place(Path(0, 0, 10, 0));
            Assert.That(r.Select(c => c.Index).ToArray(), Is.EqualTo(new[] { 3, 4, 5, 6, 7, 8, 9, 10 }));
            Assert.That(r.All(c => c.Direction == new Vector2(1, 0)), Is.True);
            for (int i = 0; i < r.Count; i++)
                Assert.That(r[i].Position.X, Is.EqualTo(10f - Spacing * (3 + i)).Within(1e-4f), "x" + i);
            Assert.That(r[0].Alpha, Is.EqualTo(0.35f).Within(1e-3f)); // terjauh memudar
            Assert.That(r[1].Alpha, Is.EqualTo(0.8f).Within(1e-3f));
            Assert.That(r[2].Alpha, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void ArrowsStayPutInTheWorldWhenTheUserMoves()
        {
            var a = Place(Path(0, 0, 10, 0));
            var b = Place(Path(0.4f, 0, 10, 0));
            var common = b.Where(cb => a.Any(ca => ca.Index == cb.Index)).ToList();
            Assert.That(common.Count, Is.GreaterThanOrEqualTo(7));
            foreach (var cb in common)
                Assert.That(cb.Position.X, Is.EqualTo(a.First(ca => ca.Index == cb.Index).Position.X).Within(1e-4f));
        }

        [Test]
        public void FollowsABendWithTheLocalDirection()
        {
            var r = Place(Path(0, 0, 5, 0, 5, 5));
            var third = r.First(c => c.Index == 3);
            Assert.That(third.Position.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(third.Position.Y, Is.EqualTo(2.3f).Within(1e-4f));
            Assert.That(third.Direction, Is.EqualTo(new Vector2(0, 1)));
            var sixth = r.First(c => c.Index == 6);
            Assert.That(sixth.Position.X, Is.EqualTo(4.6f).Within(1e-4f));
            Assert.That(sixth.Direction, Is.EqualTo(new Vector2(1, 0)));
        }

        [Test]
        public void PhaseMovesArrowsTowardTheGoal()
        {
            var r = Place(Path(0, 0, 10, 0), 0.45f);
            Assert.That(r.First(c => c.Index == 3).Position.X, Is.EqualTo(7.75f).Within(1e-4f));
        }

        [TestCase(0.9f)]
        [TestCase(-0.1f)]
        public void PhaseOutsideOneSpacingThrows(float phase)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Place(Path(0, 0, 10, 0), phase));
        }

        [Test]
        public void ShortOrDegenerateRoutesGiveNothing()
        {
            Assert.That(Place(Path(1, 1)), Is.Empty);
            Assert.That(Place(Path(0, 0, 0.3f, 0)), Is.Empty);
            Assert.That(Place(Path(2, 2, 2, 2)), Is.Empty);
            Assert.That(ChevronPlacer.Place(null, Spacing, 0f, MaxAhead, Fade), Is.Empty);
        }
    }
}
