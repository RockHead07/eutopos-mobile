using System;
using System.Numerics;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    public class NavGraphTests
    {
        // Lorong L: P01 (0,0) - P02 (4,0) - P03 (4,3); P04 terpisah di (10,10)
        const string Good = @"{
          ""frame"": ""building"", ""units"": ""m"",
          ""nodes"": [{""id"":""P01"",""x"":0,""y"":0},{""id"":""P02"",""x"":4,""y"":0},
                      {""id"":""P03"",""x"":4,""y"":3},{""id"":""P04"",""x"":10,""y"":10}],
          ""edges"": [[""P01"",""P02""],[""P02"",""P03""]],
          ""targets"": [{""node"":""P03"",""name"":""Pintu Lab""},{""node"":""P04"",""name"":""Pulau""}]
        }";

        [Test]
        public void ParsesNodesAndTargets()
        {
            var g = NavGraph.Parse(Good);
            Assert.That(g.Nodes.Count, Is.EqualTo(4));
            Assert.That(g.Nodes["P02"], Is.EqualTo(new Vector2(4, 0)));
            Assert.That(g.Targets[0].Name, Is.EqualTo("Pintu Lab"));
        }

        [TestCase(@"{""frame"":""model"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[]}", "frame")]
        [TestCase(@"{""frame"":""building"",""units"":""cm"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[]}", "units")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[],""edges"":[],""targets"":[]}", "nodes")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0},{""id"":""A"",""x"":1,""y"":0}],""edges"":[],""targets"":[]}", "ganda")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":""0"",""y"":0}],""edges"":[],""targets"":[]}", "bukan angka")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[[""A"",""Z""]],""targets"":[]}", "Z")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[[""A"",""A""]],""targets"":[]}", "dirinya")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[{""node"":""Z"",""name"":""x""}]}", "target")]
        [TestCase(@"{bukan json", "JSON")]
        public void RejectsBrokenGraphsWithTheReason(string json, string words)
        {
            var e = Assert.Throws<FormatException>(() => NavGraph.Parse(json));
            StringAssert.Contains(words, e.Message);
        }

        [TestCase(@"{""frame"":{},""units"":""m"",""nodes"":[],""edges"":[],""targets"":[]}")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[1],""edges"":[],""targets"":[]}")]
        [TestCase(@"{""frame"":""building"",""units"":""m"",""nodes"":[{""id"":""A"",""x"":0,""y"":0}],""edges"":[],""targets"":[""A""]}")]
        [TestCase(@"[]")]
        [TestCase(null)]
        public void AnyMalformedShapeIsAFormatException(string json)
        {
            Assert.Throws<FormatException>(() => NavGraph.Parse(json));
        }

        [Test]
        public void ProjectsOntoTheNearestEdge()
        {
            var g = NavGraph.Parse(Good);
            Assert.That(g.TryProject(new Vector2(2, 0.7f), out var p, out var a, out var b), Is.True);
            Assert.That(Vector2.Distance(p, new Vector2(2, 0)), Is.LessThan(1e-4f));
            Assert.That((a, b), Is.EqualTo(("P01", "P02")));
        }

        [Test]
        public void PlansAlongTheCorridor()
        {
            var r = NavGraph.Parse(Good).Plan(new Vector2(1, 0.5f), "P03");
            Assert.That(r, Is.Not.Null);
            Assert.That(r.NodeIds, Is.EqualTo(new[] { "P02", "P03" }));
            Assert.That(r.Points[0], Is.EqualTo(new Vector2(1, 0.5f)));
            Assert.That(Vector2.Distance(r.Points[1], new Vector2(1, 0)), Is.LessThan(1e-4f));
            Assert.That(r.LengthM, Is.EqualTo(0.5f + 3f + 3f).Within(1e-3f));
        }

        [Test]
        public void UnreachableTargetHasNoRoute()
        {
            Assert.That(NavGraph.Parse(Good).Plan(new Vector2(1, 0), "P04"), Is.Null);
        }

        [Test]
        public void UnknownTargetIsAnError()
        {
            Assert.Throws<ArgumentException>(() => NavGraph.Parse(Good).Plan(Vector2.Zero, "P99"));
        }

        [Test]
        public void HasEdgeIsSymmetric()
        {
            var g = NavGraph.Parse(Good);
            Assert.That(g.HasEdge("P01", "P02") && g.HasEdge("P02", "P01"), Is.True);
            Assert.That(g.HasEdge("P01", "P03"), Is.False);
            Assert.That(g.HasEdge("P01", "P99"), Is.False);
        }

        [Test]
        public void ArrivalUsesAStrictRadius()
        {
            Assert.That(NavGraph.Arrived(new Vector2(0.99f, 0), Vector2.Zero), Is.True);
            Assert.That(NavGraph.Arrived(new Vector2(1.0f, 0), Vector2.Zero), Is.False);
        }
    }
}
