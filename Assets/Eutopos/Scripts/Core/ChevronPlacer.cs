using System;
using System.Collections.Generic;
using System.Numerics;

namespace Eutopos.Core
{
    public readonly struct Chevron
    {
        public int Index { get; }
        public Vector2 Position { get; }
        public Vector2 Direction { get; }
        public float Alpha { get; }

        public Chevron(int index, Vector2 position, Vector2 direction, float alpha)
        {
            Index = index; Position = position; Direction = direction; Alpha = alpha;
        }
    }

    /// <summary>
    /// Menaruh panah chevron sepanjang rute. Jarak dihitung dari TUJUAN (panah k berada k * spacing - phase dari
    /// tujuan menurut panjang busur), jadi panah diam di dunia saat pengguna berjalan dan mengalir ke tujuan
    /// saat phase naik. Hanya panah dalam [minAhead, maxAhead] dari pengguna yang dikembalikan; alfa memudar di
    /// fade terakhir sebelum maxAhead. Murni, tanpa Unity, supaya bisa diuji.
    /// </summary>
    public static class ChevronPlacer
    {
        public static List<Chevron> Place(IReadOnlyList<Vector2> points, float spacingM, float phaseM, float maxAheadM, float fadeM, float minAheadM = 0.5f)
        {
            if (phaseM < 0f || phaseM >= spacingM) throw new ArgumentOutOfRangeException(nameof(phaseM));
            var result = new List<Chevron>();
            if (points == null || points.Count < 2) return result;

            var segLen = new float[points.Count - 1];
            float total = 0f;
            for (int i = 0; i < segLen.Length; i++) { segLen[i] = Vector2.Distance(points[i], points[i + 1]); total += segLen[i]; }
            if (total < minAheadM) return result;

            for (int k = 0; ; k++)
            {
                float fromGoal = k * spacingM - phaseM;
                if (fromGoal < 0f) continue;
                float s = total - fromGoal; // jarak dari pengguna
                if (s < minAheadM) break;
                if (s > maxAheadM) continue;

                // segmen yang memuat s (dilewati bila panjangnya nol)
                float left = s;
                int seg = 0;
                while (seg < segLen.Length - 1 && left > segLen[seg]) { left -= segLen[seg]; seg++; }
                if (segLen[seg] < 1e-6f) continue;
                var dir = (points[seg + 1] - points[seg]) / segLen[seg];
                var pos = points[seg] + dir * Math.Min(left, segLen[seg]);
                float alpha = fadeM > 0f ? Math.Clamp((maxAheadM - s) / fadeM, 0f, 1f) : 1f;
                result.Add(new Chevron(k, pos, dir, alpha));
            }
            return result;
        }
    }
}
