using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core
{
    public sealed class NavTarget
    {
        public string Node { get; }
        public string Name { get; }
        public NavTarget(string node, string name) { Node = node; Name = name; }
    }

    public sealed class Route
    {
        public IReadOnlyList<Vector2> Points { get; }
        public IReadOnlyList<string> NodeIds { get; }
        public float LengthM { get; }

        public Route(List<Vector2> points, List<string> nodeIds)
        {
            Points = points;
            NodeIds = nodeIds;
            float len = 0f;
            for (int i = 1; i < points.Count; i++) len += Vector2.Distance(points[i - 1], points[i]);
            LengthM = len;
        }
    }

    /// <summary>Graf titik terukur dalam kerangka G (x, y meter), spesifikasi bagian 7.</summary>
    public sealed class NavGraph
    {
        readonly Dictionary<string, Vector2> nodes = new Dictionary<string, Vector2>();
        readonly Dictionary<string, List<string>> adj = new Dictionary<string, List<string>>();
        readonly List<(string a, string b)> edges = new List<(string a, string b)>();
        readonly List<NavTarget> targets = new List<NavTarget>();

        public IReadOnlyDictionary<string, Vector2> Nodes => nodes;
        public IReadOnlyList<NavTarget> Targets => targets;

        NavGraph() { }

        /// <summary>Graf dari JSON server. Bentuk apa pun yang rusak menjadi FormatException.</summary>
        public static NavGraph Parse(string json)
        {
            try { return ParseChecked(json); }
            catch (FormatException) { throw; }
            catch (Exception e) { throw new FormatException("navgraph rusak: " + e.Message, e); }
        }

        static NavGraph ParseChecked(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonReaderException e) { throw new FormatException("navgraph bukan JSON: " + e.Message); }

            if ((string)root["frame"] != "building") throw new FormatException("frame harus \"building\"");
            if ((string)root["units"] != "m") throw new FormatException("units harus \"m\"");
            var g = new NavGraph();
            if (!(root["nodes"] is JArray nodeArr) || nodeArr.Count == 0) throw new FormatException("nodes kosong");
            foreach (var n in nodeArr)
            {
                var id = (string)n["id"];
                if (string.IsNullOrEmpty(id)) throw new FormatException("simpul tanpa id");
                if (g.nodes.ContainsKey(id)) throw new FormatException("id simpul ganda: " + id);
                g.nodes[id] = new Vector2(Number(n["x"], id, "x"), Number(n["y"], id, "y"));
                g.adj[id] = new List<string>();
            }
            if (!(root["edges"] is JArray edgeArr)) throw new FormatException("edges harus daftar");
            foreach (var e in edgeArr)
            {
                if (!(e is JArray pair) || pair.Count != 2 || pair[0].Type != JTokenType.String || pair[1].Type != JTokenType.String)
                    throw new FormatException("sisi harus pasangan id: " + e.ToString(Formatting.None));
                string a = (string)pair[0], b = (string)pair[1];
                if (a == b) throw new FormatException("sisi ke dirinya sendiri: " + a);
                foreach (var id in new[] { a, b })
                    if (!g.nodes.ContainsKey(id)) throw new FormatException("sisi merujuk simpul yang tidak ada: " + id);
                g.edges.Add((a, b));
                g.adj[a].Add(b);
                g.adj[b].Add(a);
            }
            if (!(root["targets"] is JArray targetArr)) throw new FormatException("targets harus daftar");
            foreach (var t in targetArr)
            {
                var node = (string)t["node"];
                var name = (string)t["name"];
                if (node == null || !g.nodes.ContainsKey(node) || string.IsNullOrEmpty(name))
                    throw new FormatException("target tidak sah: " + t.ToString(Formatting.None));
                g.targets.Add(new NavTarget(node, name));
            }
            return g;
        }

        static float Number(JToken token, string id, string key)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new FormatException($"simpul {id}: {key} bukan angka");
            double v = (double)token;
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new FormatException($"simpul {id}: {key} bukan angka");
            return (float)v;
        }

        public bool TryProject(Vector2 p, out Vector2 point, out string a, out string b)
        {
            float best = float.PositiveInfinity;
            point = default; a = null; b = null;
            foreach (var (ea, eb) in edges)
            {
                Vector2 A = nodes[ea], B = nodes[eb], ab = B - A;
                float len2 = ab.LengthSquared();
                float s = len2 > 0 ? Math.Clamp(Vector2.Dot(p - A, ab) / len2, 0f, 1f) : 0f;
                Vector2 q = A + s * ab;
                float d = Vector2.DistanceSquared(p, q);
                if (d < best) { best = d; point = q; a = ea; b = eb; }
            }
            return a != null;
        }

        /// <summary>A* dari titik proyeksi pengguna ke simpul target. Null bila tidak terjangkau.</summary>
        public Route Plan(Vector2 from, string targetNode)
        {
            if (!nodes.ContainsKey(targetNode)) throw new ArgumentException("target tidak ada: " + targetNode);
            if (!TryProject(from, out var proj, out var ea, out var eb)) return null;

            const string Start = "\u0000start";
            Vector2 Pos(string id) => id == Start ? proj : nodes[id];
            IEnumerable<string> Neighbors(string id) => id == Start ? new[] { ea, eb } : (IEnumerable<string>)adj[id];
            Vector2 goal = nodes[targetNode];

            var cost = new Dictionary<string, float> { [Start] = 0f };
            var parent = new Dictionary<string, string>();
            var open = new List<string> { Start };
            var closed = new HashSet<string>();
            bool reached = false;
            while (open.Count > 0)
            {
                string cur = open[0];
                float bestF = float.PositiveInfinity;
                foreach (var o in open)
                {
                    float f = cost[o] + Vector2.Distance(Pos(o), goal);
                    if (f < bestF) { bestF = f; cur = o; }
                }
                if (cur == targetNode) { reached = true; break; }
                open.Remove(cur);
                closed.Add(cur);
                foreach (var nb in Neighbors(cur))
                {
                    if (closed.Contains(nb)) continue;
                    float c = cost[cur] + Vector2.Distance(Pos(cur), nodes[nb]);
                    if (!cost.TryGetValue(nb, out var old) || c < old)
                    {
                        cost[nb] = c;
                        parent[nb] = cur;
                        if (!open.Contains(nb)) open.Add(nb);
                    }
                }
            }
            if (!reached) return null;

            var ids = new List<string>();
            for (string c = targetNode; c != Start; c = parent[c]) ids.Add(c);
            ids.Reverse();
            var points = new List<Vector2> { from, proj };
            foreach (var id in ids) points.Add(nodes[id]);
            return new Route(points, ids);
        }

        public bool HasEdge(string a, string b) => adj.TryGetValue(a, out var n) && n.Contains(b);

        public static bool Arrived(Vector2 user, Vector2 target, float radiusM = 1.0f) =>
            Vector2.Distance(user, target) < radiusM;
    }
}
