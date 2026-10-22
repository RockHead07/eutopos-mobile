using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    /// <summary>
    /// Kasus yang sama dengan server (eutopos-vps tests/fixtures/walkable_cases.json, disalin ke sini).
    /// Dashboard menguji rute dengan perencana Python; berkas ini memastikan perencana C# memberi rute yang
    /// sama, jadi lolos di dashboard berarti lolos di ponsel. Jangan mengubah nilai di berkas itu dari sini.
    /// </summary>
    public class WalkableCasesTests
    {
        static JObject Load() =>
            JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Eutopos/Tests/EditMode/walkable_cases.json")));

        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        static string[] Rows(JToken t) => t.Select(r => (string)r).ToArray();

        static IEnumerable<TestCaseData> RouteCases() =>
            Load()["cases"].Select(c => new TestCaseData(c).SetName("Route_" + (string)c["name"]));

        static IEnumerable<TestCaseData> BitCases() =>
            Load()["bits"].Select(c => new TestCaseData(c).SetName("Bits_" + (string)c["b64"]));

        [TestCaseSource(nameof(RouteCases))]
        public void RouteMatchesTheServer(JToken c)
        {
            var g = WalkableGrid.FromRows(Rows(c["rows"]), (double)c["cell_m"], (double)c["origin"][0], (double)c["origin"][1]);
            var r = g.Plan(V(c["from"]), V(c["to"]));
            if ((bool?)c["expect"]["unreachable"] == true)
            {
                Assert.That(r, Is.Null);
                return;
            }
            Assert.That(r, Is.Not.Null);
            var want = c["expect"]["points"].Select(V).ToArray();
            Assert.That(r.Points.Count, Is.EqualTo(want.Length));
            for (int i = 0; i < want.Length; i++)
            {
                Assert.That(r.Points[i].X, Is.EqualTo(want[i].X).Within(1e-3f), "x" + i);
                Assert.That(r.Points[i].Y, Is.EqualTo(want[i].Y).Within(1e-3f), "y" + i);
            }
            Assert.That(r.LengthM, Is.EqualTo((float)c["expect"]["length_m"]).Within(1e-3f));
        }

        [TestCaseSource(nameof(BitCases))]
        public void BitsMatchTheServer(JToken c)
        {
            var rows = Rows(c["rows"]);
            var json = new JObject
            {
                ["frame"] = "building", ["units"] = "m",
                ["cell_m"] = (double)c["cell_m"], ["origin"] = c["origin"],
                ["width"] = rows[0].Length, ["height"] = rows.Length, ["bits"] = (string)c["b64"],
                ["targets"] = new JArray(),
            };
            var g = WalkableGrid.Parse(json.ToString());
            for (int top = 0; top < rows.Length; top++)
                for (int col = 0; col < rows[top].Length; col++)
                    Assert.That(g.IsWalkable(col, rows.Length - 1 - top), Is.EqualTo(rows[top][col] == '.'), $"col {col} row {rows.Length - 1 - top}");
        }

        [Test]
        public void ParseReadsTargetsAndRejectsBadShapes()
        {
            string Make(string bits, int w, string targets = "[]") =>
                $@"{{""frame"":""building"",""units"":""m"",""cell_m"":1.0,""origin"":[0,0],""width"":{w},""height"":1,""bits"":""{bits}"",""targets"":{targets}}}";
            var g = WalkableGrid.Parse(Make("Bw==", 3, @"[{""id"":""R2"",""name"":""Lab"",""x"":1.5,""y"":0.5}]"));
            Assert.That(g.Targets.Count, Is.EqualTo(1));
            Assert.That((g.Targets[0].Id, g.Targets[0].Name, g.Targets[0].Position), Is.EqualTo(("R2", "Lab", new Vector2(1.5f, 0.5f))));
            Assert.Throws<FormatException>(() => WalkableGrid.Parse(Make("Bw==", 40)));            // bits terlalu pendek
            Assert.Throws<FormatException>(() => WalkableGrid.Parse(Make("###", 3)));              // bukan base64
            Assert.Throws<FormatException>(() => WalkableGrid.Parse(Make("Bw==", 3, @"[{""id"":""R2""}]")));
            Assert.Throws<FormatException>(() => WalkableGrid.Parse("{}"));
        }

        [Test]
        public void ArrivedUsesTheRadius()
        {
            Assert.That(WalkableGrid.Arrived(new Vector2(0, 0), new Vector2(0.9f, 0)), Is.True);
            Assert.That(WalkableGrid.Arrived(new Vector2(0, 0), new Vector2(1.0f, 0)), Is.False);
            Assert.That(WalkableGrid.Arrived(new Vector2(0, 0), new Vector2(2.9f, 0), 3f), Is.True);
        }
    }
}
