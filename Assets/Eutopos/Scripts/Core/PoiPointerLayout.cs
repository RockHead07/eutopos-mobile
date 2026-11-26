namespace Eutopos.Core
{
    /// <summary>Perhitungan skala, offset ujung ke titik asal, dan tinggi label pin POI 3D.</summary>
    public static class PoiPointerLayout
    {
        public static float ScaleForHeight(float boundsHeight, float targetHeightM)
        {
            if (float.IsNaN(boundsHeight) || float.IsInfinity(boundsHeight) || boundsHeight <= 1e-6f) return 0f;
            if (float.IsNaN(targetHeightM) || float.IsInfinity(targetHeightM) || targetHeightM <= 0f) return 0f;
            return targetHeightM / boundsHeight;
        }

        public static float TipLocalOffsetY(float boundsMinY, float scale) => -boundsMinY * scale;

        public static float LabelLiftForHeight(float topHeightM, float clearanceM) => topHeightM + clearanceM;
    }
}
