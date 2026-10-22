using System;
using System.Collections.Generic;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core
{
    public sealed class WalkTarget
    {
        public string Id { get; }
        public string Name { get; }
        public Vector2 Position { get; }
        public WalkTarget(string id, string name, Vector2 position) { Id = id; Name = name; Position = position; }
    }

    /// <summary>
    /// Grid walkable siap pakai dari server (kerangka G, 2D) dan perencana rutenya. Aturannya sama persis dengan
    /// eutopos-vps server/walkable.py (spesifikasi 2026-10-21 area walkable): A* delapan arah dengan biaya
    /// bilangan bulat, lalu diluruskan. Diuji dengan walkable_cases.json yang sama dengan server.
    /// Sel [row, col], indeks row * Width + col, baris naik searah y.
    /// </summary>
    public sealed class WalkableGrid
    {
        // dx, dy; urutan ini bagian dari aturan (tie-break), jangan diubah
        static readonly int[] Dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        static readonly int[] Dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        const int MaxCells = 1_000_000, MaxSide = 20_000;
        const long MaxF = 1L << 21;

        readonly bool[] cells;
        readonly double cellM, ox, oy;
        readonly List<WalkTarget> targets = new List<WalkTarget>();
        // dipakai ulang antar Plan supaya tidak membebani GC di ponsel
        int[] best, parent;
        bool[] closed;

        public int Width { get; }
        public int Height { get; }
        public float CellM => (float)cellM;
        public Vector2 Origin => new Vector2((float)ox, (float)oy);
        public IReadOnlyList<WalkTarget> Targets => targets;

        WalkableGrid(bool[] cells, int width, int height, double cellM, double ox, double oy)
        {
            this.cells = cells; Width = width; Height = height; this.cellM = cellM; this.ox = ox; this.oy = oy;
        }

        public bool IsWalkable(int col, int row) => col >= 0 && col < Width && row >= 0 && row < Height && cells[row * Width + col];

        /// <summary>Grid dari teks untuk tes: '#' tembok, '.' walkable; baris pertama = y tertinggi.</summary>
        public static WalkableGrid FromRows(string[] rows, double cellM, double ox, double oy)
        {
            int h = rows.Length, w = rows[0].Length;
            var cells = new bool[w * h];
            for (int top = 0; top < h; top++)
                for (int col = 0; col < w; col++)
                    cells[(h - 1 - top) * w + col] = rows[top][col] == '.';
            return new WalkableGrid(cells, w, h, cellM, ox, oy);
        }

        /// <summary>Objek "walkable" dari /navgraph. Bentuk apa pun yang rusak menjadi FormatException.</summary>
        public static WalkableGrid Parse(string json)
        {
            try { return ParseChecked(json); }
            catch (FormatException) { throw; }
            catch (Exception e) { throw new FormatException("walkable rusak: " + e.Message, e); }
        }

        static WalkableGrid ParseChecked(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonReaderException e) { throw new FormatException("walkable bukan JSON: " + e.Message); }
            if ((string)root["frame"] != "building") throw new FormatException("frame harus \"building\"");
            if ((string)root["units"] != "m") throw new FormatException("units harus \"m\"");
            double cell = Num(root["cell_m"], "cell_m");
            if (cell <= 0) throw new FormatException("cell_m harus positif");
            if (!(root["origin"] is JArray org) || org.Count != 2) throw new FormatException("origin harus [x, y]");
            double ox = Num(org[0], "origin"), oy = Num(org[1], "origin");
            int w = Int(root["width"], "width"), h = Int(root["height"], "height");
            if (w <= 0 || h <= 0 || w > MaxSide || h > MaxSide || (long)w * h > MaxCells) throw new FormatException("ukuran grid tidak sah");
            var bitsText = (string)root["bits"];
            if (bitsText == null) throw new FormatException("bits kosong");
            byte[] raw;
            try { raw = Convert.FromBase64String(bitsText); }
            catch (FormatException) { throw new FormatException("bits bukan base64"); }
            int n = w * h;
            if (raw.Length != (n + 7) / 8) throw new FormatException("bits tidak cocok dengan width * height");
            var cells = new bool[n];
            for (int i = 0; i < n; i++) cells[i] = ((raw[i >> 3] >> (i & 7)) & 1) == 1;
            var g = new WalkableGrid(cells, w, h, cell, ox, oy);
            if (!(root["targets"] is JArray arr)) throw new FormatException("targets harus daftar");
            foreach (var t in arr)
            {
                var id = (string)t["id"];
                var name = (string)t["name"];
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                    throw new FormatException("target tidak sah: " + t.ToString(Formatting.None));
                g.targets.Add(new WalkTarget(id, name, new Vector2((float)Num(t["x"], id), (float)Num(t["y"], id))));
            }
            return g;
        }

        static double Num(JToken token, string key)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new FormatException(key + " bukan angka");
            double v = (double)token;
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new FormatException(key + " bukan angka");
            return v;
        }

        static int Int(JToken token, string key)
        {
            if (token == null || token.Type != JTokenType.Integer) throw new FormatException(key + " bukan bilangan bulat");
            return (int)token;
        }

        public static bool Arrived(Vector2 user, Vector2 target, float radiusM = 1.0f) =>
            Vector2.Distance(user, target) < radiusM;

        bool OwnWalkable(Vector2 p) =>
            IsWalkable((int)Math.Floor((p.X - ox) / cellM), (int)Math.Floor((p.Y - oy) / cellM));

        /// <summary>Sel sendiri bila walkable; selain itu sel walkable terdekat (seri: indeks terkecil). -1 bila tidak ada.</summary>
        int Anchor(Vector2 p)
        {
            double px = p.X, py = p.Y;
            int col = (int)Math.Floor((px - ox) / cellM), row = (int)Math.Floor((py - oy) / cellM);
            if (IsWalkable(col, row)) return row * Width + col;
            int bestIdx = -1;
            double bestD2 = double.PositiveInfinity;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i]) continue;
                double cx = ox + (i % Width + 0.5) * cellM - px, cy = oy + (i / Width + 0.5) * cellM - py;
                double d2 = cx * cx + cy * cy;
                if (d2 < bestD2) { bestD2 = d2; bestIdx = i; }
            }
            return bestIdx;
        }

        /// <summary>Sel tetangga walkable, dan diagonal tidak memotong sudut tembok.</summary>
        bool Passable(int col, int row, int dx, int dy)
        {
            int nc = col + dx, nr = row + dy;
            if (!IsWalkable(nc, nr)) return false;
            return dx == 0 || dy == 0 || (cells[row * Width + nc] && cells[nr * Width + col]);
        }

        int Heuristic(int idx, int goal)
        {
            int dx = Math.Abs(idx % Width - goal % Width), dy = Math.Abs(idx / Width - goal / Width);
            return 10 * Math.Max(dx, dy) + 4 * Math.Min(dx, dy);
        }

        /// <summary>
        /// A* delapan arah, biaya 10 dan 14, prioritas (f, h, indeks) lewat kunci 63 bit. Parent hanya diganti bila
        /// biaya LEBIH KECIL; goal diperiksa saat di-pop. Harus sama dengan walkable._astar di server.
        /// ponytail: kunci membatasi f kurang dari 2^21 (rute di atas sekitar 150 ribu langkah ditolak); pakai dua kunci bila perlu.
        /// </summary>
        List<int> AStar(int start, int goal)
        {
            int n = cells.Length;
            if (best == null || best.Length != n) { best = new int[n]; parent = new int[n]; closed = new bool[n]; }
            Array.Fill(best, int.MaxValue);
            Array.Fill(closed, false);
            var heap = new List<long>();
            best[start] = 0;
            int h0 = Heuristic(start, goal);
            HeapPush(heap, ((long)h0 << 42) | ((long)h0 << 22) | (long)start);
            while (heap.Count > 0)
            {
                int idx = (int)(HeapPop(heap) & 0x3FFFFF);
                if (closed[idx]) continue;
                if (idx == goal)
                {
                    var path = new List<int> { idx };
                    while (path[path.Count - 1] != start) path.Add(parent[path[path.Count - 1]]);
                    path.Reverse();
                    return path;
                }
                closed[idx] = true;
                int row = idx / Width, col = idx % Width;
                for (int k = 0; k < 8; k++)
                {
                    int dx = Dx[k], dy = Dy[k];
                    if (!Passable(col, row, dx, dy)) continue;
                    int nb = (row + dy) * Width + col + dx;
                    if (closed[nb]) continue;
                    int cost = best[idx] + (dx == 0 || dy == 0 ? 10 : 14);
                    if (cost >= best[nb]) continue;
                    best[nb] = cost; parent[nb] = idx;
                    int hn = Heuristic(nb, goal);
                    long f = (long)cost + hn;
                    if (f >= MaxF) continue;
                    HeapPush(heap, (f << 42) | ((long)hn << 22) | (long)nb);
                }
            }
            return null;
        }

        static void HeapPush(List<long> h, long v)
        {
            h.Add(v);
            int i = h.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (h[p] <= h[i]) break;
                (h[p], h[i]) = (h[i], h[p]);
                i = p;
            }
        }

        static long HeapPop(List<long> h)
        {
            long top = h[0];
            int last = h.Count - 1;
            h[0] = h[last];
            h.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < h.Count && h[l] < h[m]) m = l;
                if (r < h.Count && h[r] < h[m]) m = r;
                if (m == i) break;
                (h[m], h[i]) = (h[i], h[m]);
                i = m;
            }
            return top;
        }

        /// <summary>Garis pandang Bresenham bilangan bulat (semua oktan): semua sel yang dilalui walkable.</summary>
        bool Los(int a, int b)
        {
            int x0 = a % Width, y0 = a / Width, x1 = b % Width, y1 = b / Width;
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (!cells[y0 * Width + x0]) return false;
                if (x0 == x1 && y0 == y1) return true;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Rute dari from ke to di atas grid; null bila tidak ada sel walkable atau tujuan tidak terjangkau.</summary>
        public Route Plan(Vector2 from, Vector2 to)
        {
            int a = Anchor(from), b = Anchor(to);
            if (a < 0 || b < 0) return null;
            var path = AStar(a, b);
            if (path == null) return null;

            var kept = new List<int> { path[0] };
            int i = 0;
            while (i < path.Count - 1)
            {
                int j = path.Count - 1;
                while (j > i + 1 && !Los(path[i], path[j])) j--;
                kept.Add(path[j]);
                i = j;
            }

            bool startOk = OwnWalkable(from), goalOk = OwnWalkable(to);
            var mid = new List<int>(kept);
            if (kept.Count == 1)
            {
                if (startOk && goalOk) mid.Clear();
            }
            else
            {
                if (goalOk) mid.RemoveAt(mid.Count - 1);
                if (startOk) mid.RemoveAt(0);
            }

            var points = new List<Vector2> { from };
            foreach (var cell in mid)
                points.Add(new Vector2((float)(ox + (cell % Width + 0.5) * cellM), (float)(oy + (cell / Width + 0.5) * cellM)));
            points.Add(to);
            return new Route(points);
        }
    }
}
