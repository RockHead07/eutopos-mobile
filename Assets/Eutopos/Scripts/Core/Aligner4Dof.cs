using System;
using System.Numerics;

namespace Eutopos.Core
{
    /// <summary>Kerangka B di U: p_U = RotY(YawDeg) * p_B + Translation (spesifikasi bagian 5).</summary>
    public readonly struct Alignment
    {
        public readonly float YawDeg;
        public readonly Vector3 Translation;

        public Alignment(float yawDeg, Vector3 translation)
        {
            YawDeg = FrameConvert.WrapDeg(yawDeg);
            Translation = translation;
        }

        public Vector3 BToU(Vector3 pB) => Aligner4Dof.RotY(YawDeg, pB) + Translation;

        public Vector3 UToB(Vector3 pU) => Aligner4Dof.RotY(-YawDeg, pU - Translation);
    }

    public static class Aligner4Dof
    {
        /// <summary>Putar di sekitar +Y, sama dengan Quaternion.Euler(0, deg, 0) di Unity (+Z ke +X).</summary>
        public static Vector3 RotY(float deg, Vector3 v)
        {
            float r = deg * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
            return new Vector3(c * v.X + s * v.Z, v.Y, -s * v.X + c * v.Z);
        }

        public static Alignment? Solve(Vector3 camPosU, Vector3 camFwdU, Vector3 camPosB, Vector3 camFwdB)
        {
            float? yawU = FrameConvert.YawDeg(camFwdU), yawB = FrameConvert.YawDeg(camFwdB);
            if (yawU == null || yawB == null) return null;
            float theta = FrameConvert.WrapDeg(yawU.Value - yawB.Value);
            return new Alignment(theta, camPosU - RotY(theta, camPosB));
        }
    }
}
