using System;
using Eutopos.Core;
using UnityEngine;
using UnityEngine.UI;
using NumV2 = System.Numerics.Vector2;

namespace Eutopos.App
{
    /// <summary>Denah 2D di kerangka G: area walkable abu, target jingga, rute biru, pengguna merah dengan garis arah.</summary>
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

        WalkableGrid boundsFor;
        float minX, minY, span;

        void FitTo(WalkableGrid nav)
        {
            // kotak pembatas sel walkable (grid punya margin kosong 3 satuan di sekelilingnya)
            int c0 = nav.Width, c1 = -1, r0 = nav.Height, r1 = -1;
            for (int r = 0; r < nav.Height; r++)
                for (int c = 0; c < nav.Width; c++)
                    if (nav.IsWalkable(c, r)) { c0 = Math.Min(c0, c); c1 = Math.Max(c1, c); r0 = Math.Min(r0, r); r1 = Math.Max(r1, r); }
            if (c1 < 0) { c0 = 0; c1 = nav.Width - 1; r0 = 0; r1 = nav.Height - 1; }
            minX = nav.Origin.X + c0 * nav.CellM;
            minY = nav.Origin.Y + r0 * nav.CellM;
            span = Math.Max(Math.Max((c1 - c0 + 1) * nav.CellM, (r1 - r0 + 1) * nav.CellM), 1f);
            boundsFor = nav;
        }

        public void Draw(WalkableGrid nav, Route route, NumV2? userG, float yawB)
        {
            tex.SetPixels32(clear);
            if (nav != null)
            {
                if (boundsFor != nav) FitTo(nav);
                float scale = (Size - 2 * Margin) / span;
                Vector2Int Px(NumV2 p) => new Vector2Int(Margin + (int)((p.X - minX) * scale), Margin + (int)((p.Y - minY) * scale));

                // latar: sel walkable per piksel
                for (int py = Margin; py < Size - Margin; py++)
                    for (int px = Margin; px < Size - Margin; px++)
                    {
                        float gx = minX + (px - Margin) / scale, gy = minY + (py - Margin) / scale;
                        int col = (int)Math.Floor((gx - nav.Origin.X) / nav.CellM), row = (int)Math.Floor((gy - nav.Origin.Y) / nav.CellM);
                        if (nav.IsWalkable(col, row)) tex.SetPixel(px, py, new Color32(110, 110, 110, 200));
                    }
                foreach (var t in nav.Targets) Dot(Px(t.Position), 3, new Color32(224, 122, 46, 255));
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
