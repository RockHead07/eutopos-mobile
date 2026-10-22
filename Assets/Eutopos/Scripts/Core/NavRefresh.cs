namespace Eutopos.Core
{
    /// <summary>Aturan mengganti graf yang sudah dimuat dengan terbitan baru dari server.</summary>
    public static class NavRefresh
    {
        /// <summary>Ganti hanya bila revision berubah dan tidak sedang navigasi (rute berjalan tidak boleh berubah di tengah jalan).</summary>
        public static bool ShouldReplace(string current, string incoming, bool navigating) =>
            !navigating && incoming != null && incoming != current;
    }
}
