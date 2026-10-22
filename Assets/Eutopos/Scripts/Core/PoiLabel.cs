using System.Globalization;

namespace Eutopos.Core
{
    /// <summary>Teks di atas pin POI: nama ruangan dan jarak menurut aplikasi (satu desimal, titik, seperti banner).</summary>
    public static class PoiLabel
    {
        public static string Format(string name, float distanceM) =>
            (string.IsNullOrEmpty(name) ? "?" : name) + ", " + distanceM.ToString("0.0", CultureInfo.InvariantCulture) + " m";
    }
}
