using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Eutopos.Core.Tests
{
    /// <summary>
    /// Kasus aturan rute yang sama dengan server (eutopos-vps tests/fixtures/route_cases.json, disalin ke
    /// sini). Uji rute di dashboard memakai port Python dari NavGraph; berkas ini memastikan keduanya
    /// memberi rute yang sama, jadi lolos di dashboard berarti lolos di ponsel.
    /// </summary>
    public class RouteCasesTests
    {
        static JObject Cases() =>
            JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Eutopos/Tests/EditMode/route_cases.json")));

        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        [Test]
        public void RouteCasesMatchTheServer()
        {
            foreach (JObject c in Cases()["routes"])
            {
                var name = (string)c["name"];
                var g = NavGraph.Parse(c["graph"].ToString(Formatting.None));
                var r = g.Plan(V(c["from"]), (string)c["target"]);
                if ((bool?)c["expect"]["unreachable"] == true)
                {
                    Assert.That(r, Is.Null, name);
                    continue;
                }
                Assert.That(r, Is.Not.Null, name);
                Assert.That(r.NodeIds.ToArray(), Is.EqualTo(c["expect"]["nodes"].Select(t => (string)t).ToArray()), name);
                Assert.That(r.LengthM, Is.EqualTo((float)c["expect"]["length_m"]).Within(1e-3f), name);
            }
        }

        [Test]
        public void ArrivalCasesMatchTheServer()
        {
            foreach (JObject a in Cases()["arrivals"])
                Assert.That(NavGraph.Arrived(V(a["user"]), V(a["target"])), Is.EqualTo((bool)a["expect"]), a.ToString(Formatting.None));
        }
    }
}
