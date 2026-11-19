using Eutopos.App;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Eutopos.Spike
{
    public enum KeyframeDecision { Capture, NotTracking, TooFast, BelowThreshold }

    public static class Keyframes
    {
        public static KeyframeDecision Decide(TrackingState tracking, float speed,
            Vector3 position, Quaternion rotation, bool hasPrevious, Vector3 previousPosition,
            Quaternion previousRotation)
        {
            if (tracking != TrackingState.Tracking) return KeyframeDecision.NotTracking;
            if (speed > 0.7f) return KeyframeDecision.TooFast;

            // The absolute quaternion dot handles q and -q as the same orientation.
            float dotAtTenDegrees = Mathf.Cos(5f * Mathf.Deg2Rad);
            if (!hasPrevious || (position - previousPosition).sqrMagnitude >= 0.4f * 0.4f ||
                Mathf.Abs(Quaternion.Dot(rotation, previousRotation)) <= dotAtTenDegrees)
                return KeyframeDecision.Capture;

            return KeyframeDecision.BelowThreshold;
        }

        public static string FrameLine(string file, CapturedFrame frame, TrackingState tracking)
        {
            var p = frame.CamPosU;
            var q = frame.CamRotU;
            var k = frame.K;
            return new JObject
            {
                ["file"] = file,
                ["t_ns"] = frame.TimestampNs,
                ["position"] = new JArray(p.x, p.y, p.z),
                ["rotation_xyzw"] = new JArray(q.x, q.y, q.z, q.w),
                ["tracking"] = tracking.ToString(),
                ["K"] = new JObject
                {
                    ["fx"] = k.Fx, ["fy"] = k.Fy, ["cx"] = k.Cx, ["cy"] = k.Cy,
                    ["width"] = k.Width, ["height"] = k.Height,
                },
            }.ToString(Formatting.None);
        }
    }
}
