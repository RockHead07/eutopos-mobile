using System;
using Eutopos.Core;
using UnityEngine;
using UnityEngine.UI;
using NumV2 = System.Numerics.Vector2;

namespace Eutopos.App
{
    /// <summary>Denah 2D di kerangka G: sisi abu, rute biru, pengguna merah dengan garis arah.</summary>
    public sealed class MiniMap
    {
        const int Size = 256, Margin = 16;
        readonly Texture2D tex;
        readonly Color32[] clear;

        public MiniMap(RawImage target)
        {
            tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            clear = new Color32[Size * Size];
            for (int i = 0; i < clear.Length; i++) clear[i] = new Color32(0, 0, 0, 140);
            target.texture = tex;
        }

        public void Draw(NavGraph graph, Route route, NumV2? userG, float yawB)
        {
            tex.SetPixels32(clear);
            if (graph != null)
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var p in graph.Nodes.Values)
                {
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
                float span = Math.Max(Math.Max(maxX - minX, maxY - minY), 1f);
                float scale = (Size - 2 * Margin) / span;
                Vector2Int Px(NumV2 p) => new Vector2Int(Margin + (int)((p.X - minX) * scale), Margin + (int)((p.Y - minY) * scale));

                foreach (var a in graph.Nodes)
                    foreach (var b in graph.Nodes)
                        if (string.CompareOrdinal(a.Key, b.Key) < 0 && Connected(graph, a.Key, b.Key))
                            Line(Px(a.Value), Px(b.Value), new Color32(160, 160, 160, 255));
                if (route != null)
                    for (int i = 1; i < route.Points.Count; i++)
                        Line(Px(route.Points[i - 1]), Px(route.Points[i]), new Color32(60, 120, 230, 255));
                if (userG.HasValue)
                {
                    var u = Px(userG.Value);
                    Dot(u, 4, new Color32(230, 60, 60, 255));
                    if (!float.IsNaN(yawB))
                    {
                        // yaw B dari +Z_B (= +y_G) ke +X_B (= +x_G)
                        float r = yawB * Mathf.Deg2Rad;
                        var head = new Vector2Int(u.x + (int)(Mathf.Sin(r) * 18), u.y + (int)(Mathf.Cos(r) * 18));
                        Line(u, head, new Color32(230, 60, 60, 255));
                    }
                }
            }
            tex.Apply();
        }

        static bool Connected(NavGraph g, string a, string b) => g.HasEdge(a, b);

        void Line(Vector2Int a, Vector2Int b, Color32 c)
        {
            int dx = Math.Abs(b.x - a.x), dy = -Math.Abs(b.y - a.y), sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, err = dx + dy;
            int x = a.x, y = a.y;
            while (true)
            {
                Set(x, y, c);
                if (x == b.x && y == b.y) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        void Dot(Vector2Int c, int r, Color32 col)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) Set(c.x + x, c.y + y, col);
        }

        void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < Size && y < Size) tex.SetPixel(x, y, c);
        }
    }
}
