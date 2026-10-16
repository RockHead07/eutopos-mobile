using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Eutopos.App
{
    /// <summary>Satu JSONL per sesi di persistentDataPath/logs (spesifikasi bagian 11).</summary>
    public sealed class SessionLog : IDisposable
    {
        readonly StreamWriter writer;
        readonly float t0;
        public string FilePath { get; }

        public SessionLog(string dir)
        {
            var logs = Path.Combine(dir, "logs");
            Directory.CreateDirectory(logs);
            FilePath = Path.Combine(logs, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
            t0 = Time.realtimeSinceStartup;
        }

        public void Write(string type, JObject data)
        {
            data["t"] = Math.Round(Time.realtimeSinceStartup - t0, 3);
            data["type"] = type;
            writer.WriteLine(data.ToString(Formatting.None));
        }

        public void Dispose() => writer.Dispose();
    }
}
