using System.IO;

namespace Eutopos.Core
{
    /// <summary>Ekspor log sesi dari panel uji (spesifikasi bagian 10 dan 11).</summary>
    public static class LogFiles
    {
        /// <summary>eutopos-[area-]yyyyMMdd-HHmmss.jsonl; area dibersihkan supaya tidak bisa membuat folder.</summary>
        public static string ExportName(string areaId, string sessionFile)
        {
            var stamp = Path.GetFileName(sessionFile.Replace('\\', '/'));
            if (string.IsNullOrEmpty(areaId)) return "eutopos-" + stamp;
            foreach (var c in new[] { '/', '\\' }) areaId = areaId.Replace(c, '_');
            foreach (var c in Path.GetInvalidFileNameChars()) areaId = areaId.Replace(c, '_');
            return $"eutopos-{areaId}-{stamp}";
        }

        /// <summary>Baca berkas yang masih ditulis SessionLog (FileShare.ReadWrite; ReadAllBytes menolaknya).</summary>
        public static byte[] ReadShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
