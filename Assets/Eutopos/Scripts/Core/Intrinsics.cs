using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>Intrinsik pinhole dalam piksel untuk citra Width x Height.</summary>
    public readonly struct Intrinsics
    {
        public readonly float Fx, Fy, Cx, Cy;
        public readonly int Width, Height;

        public Intrinsics(float fx, float fy, float cx, float cy, int width, int height)
        {
            Fx = fx; Fy = fy; Cx = cx; Cy = cy; Width = width; Height = height;
        }

        /// <summary>Intrinsik setelah citra diputar 90 derajat searah jarum jam (W x H menjadi H x W).</summary>
        public Intrinsics RotatedClockwise() => new Intrinsics(Fy, Fx, Height - Cy, Cx, Height, Width);

        public Intrinsics ScaledTo(int width, int height)
        {
            float sx = (float)width / Width, sy = (float)height / Height;
            return new Intrinsics(Fx * sx, Fy * sy, Cx * sx, Cy * sy, width, height);
        }

        public Vector2 Project(Vector3 p) => new Vector2(Fx * p.X / p.Z + Cx, Fy * p.Y / p.Z + Cy);
    }
}
