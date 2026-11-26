using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core.Capture
{
    public static class FramePackage
    {
        public static string PrepareForUpload(CaptureSession s, string role, string metaJson, string configsJson)
        {
            if (s.Count(role) == 0) throw new InvalidOperationException("Tidak ada foto untuk dikemas.");
            string path = Path.Combine(s.Dir, role + ".zip");
            return File.Exists(path) ? path : Write(s, role, metaJson, configsJson);
        }

        public static string Write(CaptureSession s, string role, string metaJson, string configsJson)
        {
            var records = s.Records(role);
            if (records.Count == 0) throw new InvalidOperationException("Tidak ada foto untuk dikemas.");

            var meta = JObject.Parse(metaJson);
            if (meta["resolution"] == null)
            {
                var first = records[0].K;
                meta["resolution"] = new JObject { ["width"] = first.Width, ["height"] = first.Height };
            }

            string path = Path.Combine(s.Dir, role + ".zip");
            string source = Path.Combine(s.Dir, role);
            CaptureDisk.Write(path, stream =>
            {
                using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var record in records)
                    Copy(zip, "images/" + record.File, Path.Combine(source, "images", record.File),
                        CompressionLevel.NoCompression);
                Copy(zip, "frames.jsonl", Path.Combine(source, "frames.jsonl"), CompressionLevel.Optimal);
                WriteText(zip, "meta.json", meta.ToString(Formatting.None));
                WriteText(zip, "configs.json", configsJson);
            });
            return path;
        }

        static void Copy(ZipArchive zip, string name, string source, CompressionLevel compression)
        {
            using var input = File.OpenRead(source);
            using var entry = zip.CreateEntry(name, compression).Open();
            input.CopyTo(entry);
        }

        static void WriteText(ZipArchive zip, string name, string value)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(),
                new UTF8Encoding(false));
            writer.Write(value);
        }
    }
}
