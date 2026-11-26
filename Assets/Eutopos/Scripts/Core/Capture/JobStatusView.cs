using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core.Capture
{
    public sealed class JobSnapshot
    {
        public int Id;
        public string Status;
        public string Stage;
        public string Error;
        public string GateDecision;
        public IReadOnlyList<string> GateReasons = Array.Empty<string>();
        public int? MapVersionId;
        public bool Stalled;

        internal static JobSnapshot FromJson(JObject json)
        {
            var summary = json["summary"] as JObject;
            var gate = summary?["gate"] as JObject;
            var reasons = gate?["reasons"] as JArray;
            return new JobSnapshot
            {
                Id = (int)json["id"],
                Status = (string)json["status"],
                Stage = (string)json["stage"],
                Error = (string)json["error"],
                GateDecision = (string)gate?["decision"],
                GateReasons = reasons?.OfType<JObject>().Select(r => (string)r["message"])
                    .Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? Array.Empty<string>(),
                MapVersionId = (int?)json["map_version_id"],
                Stalled = (bool?)json["stalled"] ?? false,
            };
        }
    }

    public enum JobView { Uploading, Queued, Processing, ReadyToTest, Held, Failed, Deleting }

    public static class JobStatusView
    {
        public static (JobView view, string text) Map(JobSnapshot j)
        {
            (JobView view, string text) result = j.Status switch
            {
                "uploading" => (JobView.Uploading, "Mengunggah"),
                "queued" => (JobView.Queued, "Antre"),
                "running" => (JobView.Processing,
                    string.IsNullOrEmpty(j.Stage) ? "Memproses" : $"Memproses ({j.Stage})"),
                "done" when j.GateDecision == "publish" => (JobView.ReadyToTest, "Siap diuji"),
                "done" => (JobView.Held, Detail("Ditahan", string.Join("; ", j.GateReasons ?? Array.Empty<string>()))),
                "failed" => (JobView.Failed, Detail("Gagal", FirstLine(j.Error))),
                "deleting" => (JobView.Deleting, "Dihapus"),
                _ => (JobView.Failed, "Gagal: status pekerjaan tidak dikenal"),
            };
            return j.Stalled ? (result.view, result.text + " (macet)") : result;
        }

        static string FirstLine(string value) => value?.Split(new[] { '\r', '\n' }, 2)[0];

        static string Detail(string label, string detail) =>
            string.IsNullOrWhiteSpace(detail) ? label : label + ": " + detail;
    }
}
