using System.IO;
using Eutopos.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Eutopos.App
{
    public sealed partial class AppController : MonoBehaviour
    {
        ARCameraManager cameraManager;
        Camera arCamera;
        FrameCapture capture;
        VpsClient vps;
        BuildingRootController rootCtl;
        SessionLog log;
        AppUi ui;

        readonly CorrectionScheduler scheduler = new CorrectionScheduler();
        readonly JumpGuard guard = new JumpGuard();
        int consecutiveFailures, acceptedCount;
        bool needsRelocalize = true, wasTracking;
        float lastPoseLogT;
        string configError, lastMessage = "Tekan Lokalisasi";
        LocalizeResult lastResult;
        float lastCorrectionT = float.NegativeInfinity;
        float lastTrackingLostT = float.NegativeInfinity;

        void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            cameraManager = FindFirstObjectByType<ARCameraManager>();
            arCamera = cameraManager.GetComponent<Camera>();
            var root = new GameObject("BuildingRoot").transform;

            capture = gameObject.AddComponent<FrameCapture>();
            capture.Init(cameraManager, arCamera);
            vps = gameObject.AddComponent<VpsClient>();
            vps.Config = VpsConfig.Load(out configError);
            rootCtl = gameObject.AddComponent<BuildingRootController>();
            rootCtl.AnchorManager = FindFirstObjectByType<ARAnchorManager>();
            rootCtl.BuildingRoot = root;
            log = new SessionLog(Application.persistentDataPath);
            ui = AppUi.Build(this);
            AwakeNavigation(root); // Task 9
        }

        void Start()
        {
            log.Write("session_start", new JObject
            {
                ["device"] = SystemInfo.deviceModel,
                ["app_version"] = Application.version,
                ["interval_s"] = scheduler.IntervalS,
                ["jump_m"] = guard.JumpThresholdM,
                ["config_error"] = configError,
            });
            EnsureNavGraph();
        }

        void OnDestroy() => log?.Dispose();

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool tracking = ARSession.state == ARSessionState.SessionTracking;
            if (tracking != wasTracking)
            {
                log.Write("tracking", new JObject { ["tracking"] = tracking, ["state"] = ARSession.state.ToString() });
                if (!tracking) { needsRelocalize = true; lastTrackingLostT = now; lastMessage = "Gerakkan ponsel perlahan"; }
                wasTracking = tracking;
            }
            scheduler.Tracking = tracking;
            if (scheduler.ShouldFireTimer(now)) StartCorrection("timer");
            if (now - lastPoseLogT >= 1f) { lastPoseLogT = now; LogPose(); }
            UpdateNavigation(now); // Task 9
            UpdateStatus(now);
        }

        public void OnLocalizePressed()
        {
            EnsureNavGraph(refresh: true); // server mungkin baru hidup lagi, atau dashboard baru menerbitkan rute
            StartCorrection("button");
        }

        void StartCorrection(string trigger)
        {
            if (vps.Config == null) { lastMessage = configError; return; }
            if (!scheduler.TryStart(Time.realtimeSinceStartup)) return;
            capture.Request((frame, error) => OnCaptured(frame, error, trigger));
        }

        void OnCaptured(CapturedFrame frame, string error, string trigger)
        {
            if (frame == null)
            {
                scheduler.Finish();
                lastMessage = error;
                log.Write("correction", new JObject { ["trigger"] = trigger, ["decision"] = "rejected", ["reason"] = error });
                return;
            }
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, "last-frame.jpg"), frame.Jpeg);
            StartCoroutine(vps.Localize(frame.Jpeg, frame.K, (r, latency) => OnLocalized(frame, r, latency, trigger)));
        }

        void OnLocalized(CapturedFrame frame, LocalizeResult r, float latency, string trigger)
        {
            scheduler.Finish();
            lastResult = r;
            lastCorrectionT = Time.realtimeSinceStartup;
            var entry = new JObject
            {
                ["trigger"] = trigger,
                ["status"] = r.Status.ToString(),
                ["inliers"] = r.Inliers,
                ["latency_s"] = latency,
                ["server_t_s"] = JObject.FromObject(r.TimingsS),
                ["map_version"] = r.MapVersion,
                ["k"] = new JArray(frame.K.Fx, frame.K.Fy, frame.K.Cx, frame.K.Cy, frame.K.Width, frame.K.Height),
            };
            switch (r.Status)
            {
                case VpsStatus.Ok:
                    HandleOk(frame, r, entry);
                    break;
                case VpsStatus.Failed:
                    consecutiveFailures++;
                    lastMessage = consecutiveFailures >= 3
                        ? "Arahkan kamera ke area bertekstur, bukan dinding polos"
                        : $"Koreksi gagal ({r.Inliers} inlier)";
                    entry["decision"] = "rejected";
                    entry["reason"] = "few_inliers";
                    break;
                case VpsStatus.NotAligned:
                    lastMessage = "Peta belum diselaraskan";
                    entry["decision"] = "rejected";
                    entry["reason"] = "not_aligned";
                    break;
                case VpsStatus.NoMap:
                    lastMessage = "Layanan belum punya peta";
                    entry["decision"] = "rejected";
                    entry["reason"] = "no_map";
                    break;
                case VpsStatus.Unauthorized:
                    lastMessage = "Token akses tidak ada atau salah";
                    entry["decision"] = "rejected";
                    entry["reason"] = "unauthorized";
                    break;
                case VpsStatus.NetworkError:
                    lastMessage = "Koreksi gagal (jaringan)";
                    entry["decision"] = "rejected";
                    entry["reason"] = "network";
                    break;
                default:
                    lastMessage = "Galat server: " + r.Detail;
                    entry["decision"] = "rejected";
                    entry["reason"] = "server_error";
                    break;
            }
            // Timer diam; tombol Lokalisasi tetap aktif sebagai jalan pulih (peta diterbitkan, token diganti), lalu HandleOk menyalakannya lagi.
            if (VpsResponses.StopsTimer(r.Status)) scheduler.Halt();
            log.Write("correction", entry);
        }

        void HandleOk(CapturedFrame frame, LocalizeResult r, JObject entry)
        {
            scheduler.Resume(); // server menjawab ok: token benar dan peta ada, apa pun keputusan atas koreksi ini
            // Spesifikasi bagian 9: setelah pelacakan hilang, tunggu koreksi dari frame BARU.
            if (frame.CapturedAtS < lastTrackingLostT)
            {
                lastMessage = "Pelacakan sempat hilang, koreksi diulang";
                entry["decision"] = "rejected";
                entry["reason"] = "stale_frame";
                return;
            }
            var posB = FrameConvert.GToB(r.PositionG);
            var fwdB = FrameConvert.ForwardB(r.RotationG);
            var camPosU = Conv.ToNum(frame.CamPosU);
            entry["pose_b"] = new JArray(posB.X, posB.Y, posB.Z);
            var proposed = Aligner4Dof.Solve(camPosU, Conv.ToNum(frame.CamFwdU), posB, fwdB);
            if (proposed == null)
            {
                lastMessage = "Tegakkan ponsel";
                entry["decision"] = "rejected";
                entry["reason"] = "camera_too_steep";
                return;
            }
            var g = guard.Evaluate(Time.realtimeSinceStartup, rootCtl.CurrentAlignment(), proposed.Value, camPosU);
            entry["jump_m"] = g.JumpM;
            entry["dropped_candidate"] = g.DroppedCandidate;
            entry["decision"] = g.Decision.ToString().ToLowerInvariant();
            if (g.Decision == CorrectionDecision.Candidate)
            {
                lastMessage = $"Lompatan {g.JumpM:0.0} m ditahan, menunggu konfirmasi";
                return;
            }
            rootCtl.Apply(proposed.Value);
            consecutiveFailures = 0;
            acceptedCount++;
            needsRelocalize = false;
            lastMessage = $"Posisi diperbarui ({r.Inliers} inlier)";
            OnCorrectionAccepted(); // Task 9
        }

        void LogPose()
        {
            var t = arCamera.transform;
            var e = new JObject
            {
                ["u_pos"] = new JArray(t.position.x, t.position.y, t.position.z),
                ["u_rot"] = new JArray(t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w),
            };
            if (rootCtl.TryUserPoseB(t, out var posB, out var yawB))
            {
                e["b_pos"] = new JArray(posB.X, posB.Y, posB.Z);
                e["b_yaw"] = float.IsNaN(yawB) ? null : (JToken)yawB;
            }
            log.Write("pose", e);
        }

        void UpdateStatus(float now)
        {
            string age = float.IsNegativeInfinity(lastCorrectionT) ? "-" : $"{now - lastCorrectionT:0}s";
            string inl = lastResult == null ? "-" : lastResult.Inliers.ToString();
            string ver = lastResult?.MapVersion?.ToString() ?? "-";
            string track = wasTracking ? "lacak" : "hilang";
            ui.Status.text = $"peta v{ver} | {inl} inlier | {age} | {track}\n{lastMessage}";
        }
    }
}
