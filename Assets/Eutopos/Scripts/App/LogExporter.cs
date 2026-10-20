using System.IO;
using Eutopos.Core;
using UnityEngine;

namespace Eutopos.App
{
    /// <summary>Salin log sesi ke Download/eutopos di ponsel (MediaStore, Android 10+) supaya bisa dibagikan tanpa adb.</summary>
    public static class LogExporter
    {
        /// <returns>Lokasi hasil salinan, untuk ditampilkan di panel uji.</returns>
        public static string Export(string sessionFile, string name)
        {
            var bytes = LogFiles.ReadShared(sessionFile);
#if UNITY_ANDROID && !UNITY_EDITOR
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") < 29)
                throw new System.NotSupportedException("butuh Android 10+; ambil lewat adb: " + sessionFile);
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var values = new AndroidJavaObject("android.content.ContentValues");
            values.Call("put", "_display_name", name);
            values.Call("put", "mime_type", "application/octet-stream"); // jenis teks membuat Android menambah .txt
            values.Call("put", "relative_path", "Download/eutopos");
            using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
            using var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
            using var item = resolver.Call<AndroidJavaObject>("insert", collection, values);
            if (item == null) throw new IOException("MediaStore menolak berkas");
            using var stream = resolver.Call<AndroidJavaObject>("openOutputStream", item);
            stream.Call("write", (sbyte[])(System.Array)bytes);
            stream.Call("close");
            return "Download/eutopos/" + name;
#else
            var dir = Path.Combine(Application.persistentDataPath, "export");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name);
            File.WriteAllBytes(path, bytes);
            return path;
#endif
        }
    }
}
