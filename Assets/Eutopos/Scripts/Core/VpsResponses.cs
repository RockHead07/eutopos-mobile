using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core
{
    public enum VpsStatus { Ok, Failed, NotAligned, NoMap, NotFound, Unauthorized, NetworkError, ServerError }

    public sealed class LocalizeResult
    {
        public VpsStatus Status;
        public int Inliers;
        public Vector3 PositionG;
        public Quaternion RotationG;
        public string AreaId;
        public int? MapVersion;
        public IReadOnlyDictionary<string, float> TimingsS = new Dictionary<string, float>();
        public string Detail;
    }

    public sealed class NavGraphResult
    {
        public VpsStatus Status;
        public NavGraph Graph;
        public string AreaId;
        public int? MapVersion;
        public string Detail;
    }

    public static class VpsResponses
    {
        public const int MinInliers = 50;

        public static string Number(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Jawaban yang tidak pulih dengan mengulang: token salah, atau layanan belum punya peta.</summary>
        public static bool StopsTimer(VpsStatus s) => s == VpsStatus.Unauthorized || s == VpsStatus.NoMap;

        /// <summary>Status yang sama untuk semua endpoint. Cloudflare Access mengalihkan ke halaman login (3xx).</summary>
        static VpsStatus? Common(long code, bool connectionError)
        {
            if (code >= 300 && code < 400 || code == 401 || code == 403) return VpsStatus.Unauthorized;
            if (connectionError && code == 0) return VpsStatus.NetworkError;
            // Cloudflare tidak menjangkau server di belakang terowongan (530 = Error 1033): sama dengan jaringan putus
            if (code == 502 || code == 504 || (code >= 520 && code <= 530)) return VpsStatus.NetworkError;
            if (code == 503) return VpsStatus.NoMap;
            return null;
        }

        /// <summary>Teks untuk layar dan log: halaman HTML (Cloudflare, proxy) tidak pernah ditampilkan mentah.</summary>
        static string Short(string s)
        {
            if (s == null) return "";
            if (s.TrimStart().StartsWith("<")) return "(halaman HTML)";
            return s.Length > 200 ? s.Substring(0, 200) : s;
        }

        public static LocalizeResult ClassifyLocalize(long httpCode, string body, bool connectionError)
        {
            var common = Common(httpCode, connectionError);
            if (common != null) return new LocalizeResult { Status = common.Value, Detail = Short(body) };
            if (httpCode != 200) return new LocalizeResult { Status = VpsStatus.ServerError, Detail = $"HTTP {httpCode}: {Short(body)}" };
            // batas kepercayaan: jawaban apa pun tidak boleh melempar ke pemanggil (coroutine mati, scheduler macet)
            try { return ParseLocalize(JObject.Parse(body)); }
            catch (JsonReaderException) { return new LocalizeResult { Status = VpsStatus.ServerError, Detail = "jawaban bukan JSON" }; }
            catch (Exception e) { return new LocalizeResult { Status = VpsStatus.ServerError, Detail = "jawaban rusak: " + e.Message }; }
        }

        static LocalizeResult ParseLocalize(JObject j)
        {
            var r = new LocalizeResult
            {
                Inliers = (int?)j["inliers"] ?? 0,
                AreaId = (string)j["area_id"],
                MapVersion = (int?)j["map_version"],
            };
            var timings = new Dictionary<string, float>();
            if (j["t_s"] is JObject ts)
                foreach (var p in ts.Properties()) timings[p.Name] = (float)p.Value;
            r.TimingsS = timings;

            if ((string)j["status"] != "ok" || r.Inliers < MinInliers) { r.Status = VpsStatus.Failed; return r; }
            if (!(j["pose_building"] is JObject pose)) { r.Status = VpsStatus.NotAligned; return r; }
            var pos = (JArray)pose["position"];
            var q = (JArray)pose["rotation_xyzw"];
            r.PositionG = new Vector3((float)pos[0], (float)pos[1], (float)pos[2]);
            r.RotationG = new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]);
            r.Status = VpsStatus.Ok;
            return r;
        }

        public static NavGraphResult ParseNavGraph(long httpCode, string body, bool connectionError)
        {
            var common = Common(httpCode, connectionError);
            if (common != null) return new NavGraphResult { Status = common.Value, Detail = Short(body) };
            if (httpCode == 404) return new NavGraphResult { Status = VpsStatus.NotFound, Detail = Short(body) };
            if (httpCode != 200) return new NavGraphResult { Status = VpsStatus.ServerError, Detail = $"HTTP {httpCode}: {Short(body)}" };
            try
            {
                var j = JObject.Parse(body);
                return new NavGraphResult
                {
                    Status = VpsStatus.Ok,
                    Graph = NavGraph.Parse(j["graph"].ToString(Formatting.None)),
                    AreaId = (string)j["area_id"],
                    MapVersion = (int?)j["map_version"],
                };
            }
            catch (Exception e)
            {
                return new NavGraphResult { Status = VpsStatus.ServerError, Detail = "graf rusak: " + e.Message };
            }
        }
    }
}
