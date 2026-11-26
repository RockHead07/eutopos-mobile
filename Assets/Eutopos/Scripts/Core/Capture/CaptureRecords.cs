using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eutopos.Core.Capture
{
    public static class AreaId
    {
        static readonly Regex InvalidChars = new Regex("[^a-z0-9]+");
        static readonly Regex ValidId = new Regex(@"\A[a-z0-9][a-z0-9-]{0,39}\z");

        public static string FromName(string name, DateTime now)
        {
            string id = InvalidChars.Replace((name ?? "").ToLowerInvariant(), "-").Trim('-');
            if (id.Length > 40) id = id.Substring(0, 40).TrimEnd('-');
            return id.Length != 0 ? id : "area-" + now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        }

        public static bool IsValid(string id) => id != null && ValidId.IsMatch(id);
    }

    public sealed class FrameRecord
    {
        public string File;
        public long TimestampNs;
        public Vector3 Position;
        public Quaternion Rotation;
        public string Tracking;
        public Intrinsics K;
        public int Segment;

        public static string ToLine(FrameRecord r)
        {
            var p = r.Position;
            var q = r.Rotation;
            var k = r.K;
            return new JObject
            {
                ["file"] = r.File,
                ["t_ns"] = r.TimestampNs,
                ["position"] = new JArray(p.X, p.Y, p.Z),
                ["rotation_xyzw"] = new JArray(q.X, q.Y, q.Z, q.W),
                ["tracking"] = r.Tracking,
                ["K"] = new JObject
                {
                    ["fx"] = k.Fx, ["fy"] = k.Fy, ["cx"] = k.Cx, ["cy"] = k.Cy,
                    ["width"] = k.Width, ["height"] = k.Height,
                },
                ["segment"] = r.Segment,
            }.ToString(Formatting.None);
        }

        public static FrameRecord FromLine(string line)
        {
            var data = JObject.Parse(line);
            if (data["file"]?.Type != JTokenType.String || data["t_ns"]?.Type != JTokenType.Integer ||
                data["tracking"]?.Type != JTokenType.String || !(data["K"] is JObject k) ||
                !(data["position"] is JArray p) || p.Count != 3 ||
                !(data["rotation_xyzw"] is JArray q) || q.Count != 4 ||
                k["width"]?.Type != JTokenType.Integer || k["height"]?.Type != JTokenType.Integer)
                throw new JsonSerializationException("Bidang foto tidak lengkap atau tidak sah.");

            return new FrameRecord
            {
                File = (string)data["file"],
                TimestampNs = (long)data["t_ns"],
                Position = new Vector3(Number(p[0]), Number(p[1]), Number(p[2])),
                Rotation = new Quaternion(Number(q[0]), Number(q[1]), Number(q[2]), Number(q[3])),
                Tracking = (string)data["tracking"],
                K = new Intrinsics(Number(k["fx"]), Number(k["fy"]), Number(k["cx"]),
                    Number(k["cy"]), (int)k["width"], (int)k["height"]),
                Segment = (int?)data["segment"] ?? 0,
            };
        }

        static float Number(JToken value)
        {
            if (value?.Type != JTokenType.Float && value?.Type != JTokenType.Integer)
                throw new JsonSerializationException("Koordinat atau intrinsik bukan angka.");
            float number = (float)value;
            if (float.IsNaN(number) || float.IsInfinity(number))
                throw new JsonSerializationException("Koordinat atau intrinsik tidak berhingga.");
            return number;
        }
    }

    public static class CaptureMeta
    {
        public static string Json(string appVersion, string deviceModel, DateTime startedUtc,
            int photos, int trackingLosses, string role)
        {
            return new JObject
            {
                ["app_version"] = appVersion,
                ["device_model"] = deviceModel,
                ["started_utc"] = startedUtc.ToUniversalTime(),
                ["photo_count"] = photos,
                ["tracking_losses"] = trackingLosses,
                ["role"] = role,
                ["coordinate_system"] = "unity_world",
                ["up_axis"] = "Y",
                ["handedness"] = "left",
            }.ToString(Formatting.None);
        }
    }
}
