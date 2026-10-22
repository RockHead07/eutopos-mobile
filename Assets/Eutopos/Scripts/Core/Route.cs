using System.Collections.Generic;
using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>Rute sebagai titik belokan di kerangka G: [posisi pengguna, belokan..., tujuan].</summary>
    public sealed class Route
    {
        public IReadOnlyList<Vector2> Points { get; }
        public float LengthM { get; }

        public Route(List<Vector2> points)
        {
            Points = points;
            float len = 0f;
            for (int i = 1; i < points.Count; i++) len += Vector2.Distance(points[i - 1], points[i]);
            LengthM = len;
        }
    }
}
