using System;
using System.Globalization;
using System.IO;
using Eutopos.App;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Eutopos.Spike
{
    public sealed class SpikeRecorder : MonoBehaviour
    {
        [SerializeField] ARSession arSession;
        [SerializeField] ARCameraManager cameraManager;

        FrameCapture capture;
        Transform cameraPose;
        StreamWriter frames;
        JObject selectedConfiguration;
        string sessionFolder = "";
        int photos, trackingLosses, notTracking, tooFast, belowThreshold, captureFailed;
        bool cameraReady, wasTracking, hasSample;
        double sampleTime;
        Vector3 samplePosition, lastPosition;
        Quaternion lastRotation;
        TrackingState captureTracking;

        Text statusText, pathText;
        Button startButton, stopButton;
        Font font;
        string message = "";

        TrackingState Tracking => arSession.subsystem?.trackingState ?? TrackingState.None;

        void Awake()
        {
            cameraPose = cameraManager.transform;
            capture = gameObject.AddComponent<FrameCapture>();

            // FrameCapture handles each request synchronously in this same frameReceived dispatch.
            cameraManager.frameReceived += OnCameraFrame;
            capture.Init(cameraManager, cameraManager.GetComponent<Camera>());
            cameraManager.frameReceived += CameraConfigured;

            CreateUi();
            UpdateUi();
        }

        void CameraConfigured(ARCameraFrameEventArgs _)
        {
            // FrameCapture's first frame has already selected the configuration.
            cameraReady = true;
            cameraManager.frameReceived -= CameraConfigured;
        }

        void Update()
        {
            if (frames != null) ObserveTracking(Tracking);
            UpdateUi();
        }

        void ObserveTracking(TrackingState tracking)
        {
            bool trackingNow = tracking == TrackingState.Tracking;
            if (wasTracking && !trackingNow) trackingLosses++;
            if (!trackingNow) hasSample = false;
            wasTracking = trackingNow;
        }

        void OnCameraFrame(ARCameraFrameEventArgs _)
        {
            if (frames == null) return;
            var tracking = Tracking;
            ObserveTracking(tracking);
            var position = cameraPose.position;
            var rotation = cameraPose.rotation;
            float speed = 0;
            if (tracking == TrackingState.Tracking)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (hasSample && now > sampleTime)
                    speed = Vector3.Distance(position, samplePosition) / (float)(now - sampleTime);
                samplePosition = position;
                sampleTime = now;
                hasSample = true;
            }

            switch (Keyframes.Decide(tracking, speed, position, rotation, photos > 0,
                        lastPosition, lastRotation))
            {
                case KeyframeDecision.NotTracking: notTracking++; return;
                case KeyframeDecision.TooFast: tooFast++; return;
                case KeyframeDecision.BelowThreshold: belowThreshold++; return;
            }

            captureTracking = tracking;
            capture.Request(OnCaptured);
        }

        void OnCaptured(CapturedFrame frame, string error)
        {
            if (frames == null) return;
            if (frame == null || frame.Jpeg == null || frame.Jpeg.Length == 0)
            {
                captureFailed++;
                message = error ?? "JPEG kamera kosong";
                return;
            }

            try
            {
                string file = (photos + 1).ToString("D6", CultureInfo.InvariantCulture) + ".jpg";
                File.WriteAllBytes(Path.Combine(sessionFolder, "images", file), frame.Jpeg);
                frames.WriteLine(Keyframes.FrameLine(file, frame, captureTracking));
                photos++;
                lastPosition = frame.CamPosU;
                lastRotation = frame.CamRotU;
                message = "";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                captureFailed++;
                StopRecording();
                message = "Penyimpanan foto gagal: " + e.Message;
                Debug.LogException(e);
            }
        }

        void StartRecording()
        {
            if (!cameraReady || frames != null) return;
            string date = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string folder = Path.Combine(Application.persistentDataPath, "spike", date);
            if (Directory.Exists(folder))
            {
                message = "Nama sesi sudah ada. Tunggu satu detik lalu coba lagi.";
                return;
            }

            try
            {
                var available = new JArray();
                using (var configs = cameraManager.GetConfigurations(Allocator.Temp))
                    foreach (var config in configs)
                        available.Add(Config(config));
                selectedConfiguration = cameraManager.currentConfiguration is { } selected
                    ? Config(selected)
                    : null;

                Directory.CreateDirectory(Path.Combine(folder, "images"));
                sessionFolder = folder;
                WriteJson("configs.json", new JObject
                {
                    ["available"] = available,
                    ["selected"] = selectedConfiguration,
                });
                photos = trackingLosses = notTracking = tooFast = belowThreshold = captureFailed = 0;
                hasSample = false;
                wasTracking = Tracking == TrackingState.Tracking;
                message = "";
                WriteMetadata();
                frames = new StreamWriter(Path.Combine(folder, "frames.jsonl")) { AutoFlush = true };
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                message = "Sesi gagal dibuat: " + e.Message;
                Debug.LogException(e);
            }
        }

        static JObject Config(XRCameraConfiguration config) => new JObject
        {
            ["width"] = config.width,
            ["height"] = config.height,
            ["fps"] = config.framerate,
        };

        void StopRecording()
        {
            if (frames == null) return;
            var writer = frames;
            frames = null;
            capture.Request(null);
            try
            {
                writer.Dispose();
                WriteMetadata();
                message = "Rekaman disimpan.";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                message = "Sesi gagal ditutup: " + e.Message;
                Debug.LogException(e);
            }
        }

        void WriteMetadata() => WriteJson("meta.json", new JObject
        {
            ["device_model"] = SystemInfo.deviceModel,
            ["app_version"] = Application.version,
            ["selected_configuration"] = selectedConfiguration?.DeepClone(),
            ["photo_count"] = photos,
            ["tracking_losses"] = trackingLosses,
            ["skipped_frames"] = new JObject
            {
                ["not_tracking"] = notTracking, ["too_fast"] = tooFast,
                ["below_threshold"] = belowThreshold, ["capture_failed"] = captureFailed,
            },
            ["coordinate_system"] = "unity_world",
            ["up_axis"] = "Y",
            ["handedness"] = "left",
        });

        void WriteJson(string file, JObject data) =>
            File.WriteAllText(Path.Combine(sessionFolder, file), data.ToString() + "\n");

        void OnApplicationPause(bool paused)
        {
            if (paused) StopRecording();
        }

        void OnDestroy()
        {
            StopRecording();
            cameraManager.frameReceived -= OnCameraFrame;
            cameraManager.frameReceived -= CameraConfigured;
        }

        void UpdateUi()
        {
            startButton.interactable = cameraReady && frames == null;
            stopButton.interactable = frames != null;
            string state = frames != null ? "Merekam" : "Siaga";
            statusText.text = $"{state}  |  Foto: {photos}  |  Hilang: {trackingLosses}\n" +
                $"Pelacakan: {Tracking} ({ARSession.state})";
            if (message.Length > 0) statusText.text += "\n" + message;
            pathText.text = sessionFolder;
        }

        void CreateUi()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            new GameObject("Spike Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var hud = new GameObject("Spike HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);

            var panel = new GameObject("Controls", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(-32, 0);
            rect.anchoredPosition = new Vector2(0, -48);
            panel.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 0.92f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 10;
            layout.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            actions.transform.SetParent(panel.transform, false);
            actions.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            startButton = AddButton(actions.transform, "Mulai", StartRecording);
            stopButton = AddButton(actions.transform, "Berhenti", StopRecording);
            statusText = AddText(panel.transform, "Status", 28);
            pathText = AddText(panel.transform, "Folder", 21);
        }

        Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<LayoutElement>().minHeight = 70;
            var text = AddText(obj.transform, "Label", 28);
            text.text = label;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.rectTransform.anchoredPosition = Vector2.zero;
            var button = obj.GetComponent<Button>();
            button.targetGraphic = obj.GetComponent<Image>();
            button.onClick.AddListener(onClick);
            return button;
        }

        Text AddText(Transform parent, string label, int size)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}
