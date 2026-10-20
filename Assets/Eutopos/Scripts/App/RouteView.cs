using System.Collections.Generic;
using Eutopos.Core;
using UnityEngine;

namespace Eutopos.App
{
    /// <summary>Garis rute di lantai (Y_B = 0,02) dan panah ke arah 1,5 m di depan, dalam koordinat lokal BuildingRoot (B).</summary>
    public sealed class RouteView : MonoBehaviour
    {
        const float FloorY = 0.02f, ArrowAheadM = 1.5f, ArrowSizeM = 0.4f;
        LineRenderer line, arrow;

        void Awake()
        {
            line = NewLine("RouteLine", 0.15f);
            arrow = NewLine("RouteArrow", 0.12f);
        }

        LineRenderer NewLine(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.18f, 0.44f, 0.87f, 0.9f);
            lr.positionCount = 0;
            return lr;
        }

        static Vector3 OnFloor(System.Numerics.Vector2 g) => new Vector3(g.X, FloorY, g.Y);

        public void Show(Route route)
        {
            var pts = new List<Vector3>();
            foreach (var p in route.Points) pts.Add(OnFloor(p));
            line.positionCount = pts.Count;
            line.SetPositions(pts.ToArray());

            // titik dan arah pada jarak ArrowAheadM sepanjang rute
            float left = ArrowAheadM;
            for (int i = 1; i < pts.Count; i++)
            {
                var seg = pts[i] - pts[i - 1];
                float len = seg.magnitude;
                if (len < 1e-4f) continue;
                if (left <= len || i == pts.Count - 1)
                {
                    var dir = seg / len;
                    var tip = pts[i - 1] + dir * Mathf.Min(left, len);
                    var side = Vector3.Cross(Vector3.up, dir) * ArrowSizeM;
                    arrow.positionCount = 3;
                    arrow.SetPositions(new[] { tip - dir * ArrowSizeM + side, tip, tip - dir * ArrowSizeM - side });
                    return;
                }
                left -= len;
            }
            arrow.positionCount = 0;
        }

        public void Clear()
        {
            line.positionCount = 0;
            arrow.positionCount = 0;
        }
    }
}
