using System;
using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>
    /// Kerangka G (server: tangan kanan, z atas) ke B (Unity: tangan kiri, Y atas), spesifikasi bagian 5.
    /// </summary>
    public static class FrameConvert
    {
        public const float MinHorizontalFraction = 0.3f;

        public static Vector3 GToB(Vector3 g) => new Vector3(g.X, g.Z, g.Y);

        public static Vector3 BToG(Vector3 b) => new Vector3(b.X, b.Z, b.Y);

        /// <summary>Sumbu optik kamera COLMAP (z depan) dari rotasi gedung-dari-kamera di G, dinyatakan di B.</summary>
        public static Vector3 ForwardB(Quaternion buildingFromCamG) =>
            GToB(Vector3.Transform(Vector3.UnitZ, buildingFromCamG));

        /// <summary>Yaw dari +Z ke +X (derajat), null bila kamera nyaris tegak lurus lantai.</summary>
        public static float? YawDeg(Vector3 f)
        {
            float len = f.Length();
            float horizontal = MathF.Sqrt(f.X * f.X + f.Z * f.Z);
            if (len < 1e-6f || horizontal < MinHorizontalFraction * len) return null;
            return MathF.Atan2(f.X, f.Z) * 180f / MathF.PI;
        }

        public static float WrapDeg(float deg)
        {
            float d = (deg + 180f) % 360f;
            if (d < 0) d += 360f;
            return d - 180f;
        }

        public static float YawDiffDeg(float a, float b) => MathF.Abs(WrapDeg(a - b));
    }
}
