using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eutopos.App;
using Eutopos.Core.Capture;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using NVector = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace Eutopos.Capture
{
    public sealed class CaptureController : MonoBehaviour
    {
        const string TokenKey = "eutopos.capture.app_token";
        const string MapRole = "peta", TestRole = "uji";
        const string TrackingWarning = "Pelacakan putus. Kembali ke area bingkai terakhir.";

        enum ScreenView { Settings, Home, Name, Record, Test, Upload, Status }

        [SerializeField] ARSession arSession;
        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] ARPointCloudManager pointCloudManager;
        [SerializeField] FrameCapture frameCapture;
        [SerializeField] Canvas canvas;
        [SerializeField] Material geometryMaterial;

        readonly UnityHttp http = new UnityHttp();
        CaptureUi ui;
        CaptureUi.CameraControls cameraUi;
        Camera arCamera;
        FrustumLayer frustums;
        PointLayer points;
        PoseContinuity continuity;
        CaptureTrackingGate trackingGate;
        CaptureSession session;
        string workspace, configError, appToken;
        VpsConfig config;
        ScreenView screen;
        int revision, baseSegment;
        bool paused, taking, auto = true, pointDisplay, tracking, stable;
        double shotTimeout, noticeUntil, nextStorage, nextHud;
        long freeBytes;
        string notice;

        System.Collections.Generic.IReadOnlyList<BuildingInfo> buildings;
        BuildingInfo building;
        string areaName, existingArea;
        DateTime nameTime;
        bool newArea;
        CancellationTokenSource transfer;

        bool IsCamera => screen == ScreenView.Record || screen == ScreenView.Test;
        string CurrentRole => screen == ScreenView.Test ? TestRole : MapRole;

        void Start()
        {
            workspace = Path.Combine(Application.persistentDataPath, "capture");
            config = VpsConfig.Load(out configError);
            appToken = PlayerPrefs.GetString(TokenKey, "");
            arCamera = cameraManager.GetComponent<Camera>();
            frameCapture.Init(cameraManager, arCamera);
            ui = new CaptureUi(canvas);
            frustums = Layer<FrustumLayer>("Bingkai foto");
            frustums.Initialize(geometryMaterial);
            points = Layer<PointLayer>("Titik ARCore");
            points.Initialize(pointCloudManager, geometryMaterial);
            if (string.IsNullOrWhiteSpace(appToken)) ShowSettings(ShowHome);
            else ShowHome();
        }

        T Layer<T>(string label) where T : Component
        {
            var layer = new GameObject(label);
            layer.transform.SetParent(transform, false);
            return layer.AddComponent<T>();
        }

        void Switch(ScreenView target)
        {
            revision++;
            StopShot();
            screen = target;
            frustums.gameObject.SetActive(IsCamera);
            points.SetVisible(IsCamera && pointDisplay);
        }

        void ShowSettings(Action done)
        {
            Switch(ScreenView.Settings);
            var page = ui.Page("Pengaturan", "Akses server untuk aplikasi capture.",
                string.IsNullOrWhiteSpace(appToken) ? null : done);
            var input = ui.Input(page, "Token aplikasi", password: true, limit: 256);
            var save = ui.Button(page, "Simpan token", () =>
            {
                appToken = input.text.Trim();
                if (appToken.Length == 0) return;
                PlayerPrefs.SetString(TokenKey, appToken);
                PlayerPrefs.Save();
                done();
            }, primary: true, interactable: false);
            input.onValueChanged.AddListener(value => save.interactable = !string.IsNullOrWhiteSpace(value));
            if (config == null) ui.Label(page, configError, muted: true);
        }

        void ShowHome() => ShowHome(null);

        void ShowHome(string message)
        {
            Switch(ScreenView.Home);
            var page = ui.Page("Eutopos Capture", "Rekam dan unggah foto peta indoor.");
            ui.Button(page, "Rekam peta baru", ShowName, primary: true);
            ui.Button(page, "Ganti token", () => ShowSettings(ShowHome));
            if (message != null) ui.Label(page, message);
            try
            {
                ShowSessionLinks(page, "Sesi belum selesai", CaptureSession.Unfinished(workspace));
                ShowSessionLinks(page, "Riwayat unggahan", CaptureSession.Submitted(workspace));
            }
            catch (IOException)
            {
                ui.Label(page, "Daftar sesi tidak bisa dibaca.");
            }
        }

        void ShowSessionLinks(Transform page, string label, System.Collections.Generic.IReadOnlyList<string> paths)
        {
            if (paths.Count == 0) return;
            ui.Label(page, label, bold: true);
            foreach (string path in paths)
                ui.Button(page, Path.GetFileName(path), () => Resume(path));
        }

        void Resume(string path)
        {
            try
            {
                UseSession(CaptureSession.Open(path), reopening: true);
                switch (session.Stage)
                {
                    case SessionStage.Recording: ShowCamera(false); break;
                    case SessionStage.Testing: ShowCamera(true); break;
                    case SessionStage.Packaged:
                    case SessionStage.Uploading: ShowUpload(); break;
                    case SessionStage.Submitted: ShowStatus(); break;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                ShowHome("Sesi tidak dapat dibuka.");
            }
        }

        void ShowName()
        {
            Switch(ScreenView.Name);
            building = null;
            buildings = null;
            areaName = "";
            existingArea = null;
            newArea = true;
            nameTime = DateTime.Now;
            _ = LoadBuildings();
        }

        async Task LoadBuildings()
        {
            if (screen != ScreenView.Name) Switch(ScreenView.Name);
            int current = revision;
            var page = ui.Page("Nama peta", "Pilih gedung dan area pemetaan.", ShowHome);
            ui.Label(page, "Memuat daftar gedung…", muted: true);
            try
            {
                var loaded = await Api().ListBuildings();
                if (revision != current || this == null) return;
                buildings = loaded;
                RenderName();
            }
            catch (Exception e)
            {
                if (revision == current && this != null)
                    ShowFailure("Nama peta", e, () => _ = LoadBuildings(), ShowHome);
            }
        }

        void RenderName()
        {
            var page = ui.Page("Nama peta", "Pilih gedung dan area pemetaan.", ShowHome);
            ui.Label(page, "Gedung", bold: true);
            if (buildings.Count == 0) ui.Label(page, "Belum ada gedung di server.", muted: true);
            foreach (var item in buildings)
                ui.Button(page, item.Name, () =>
                {
                    building = item;
                    existingArea = null;
                    RenderName();
                }, primary: item == building, height: 64);
            if (building == null) return;

            ui.Label(page, "Area", bold: true);
            ui.Button(page, "Area baru", () => { newArea = true; RenderName(); }, primary: newArea, height: 64);
            ui.Button(page, "Rekam ulang area", () => { newArea = false; RenderName(); },
                primary: !newArea, height: 64);
            if (newArea) RenderNewArea(page);
            else RenderExistingAreas(page);
        }

        void RenderNewArea(Transform page)
        {
            var input = ui.Input(page, "Nama area");
            input.SetTextWithoutNotify(areaName);
            var hint = ui.Label(page, "", muted: true);
            var start = ui.Button(page, "Mulai rekam", () =>
                StartSession(areaName.Trim(), AreaId.FromName(areaName, nameTime), building.Id), primary: true);
            void Validate(string value)
            {
                areaName = value;
                string id = AreaId.FromName(value, nameTime);
                bool exists = CapturePolicy.AreaExists(id, buildings);
                hint.text = exists ? "Area sudah ada, pilih dari daftar untuk rekam ulang" : "ID area · " + id;
                start.interactable = !string.IsNullOrWhiteSpace(value) && !exists;
            }
            input.onValueChanged.AddListener(Validate);
            Validate(areaName);
        }

        void RenderExistingAreas(Transform page)
        {
            if (building.AreaIds.Count == 0) ui.Label(page, "Gedung ini belum punya area.", muted: true);
            foreach (string id in building.AreaIds)
                ui.Button(page, id, () => { existingArea = id; RenderName(); },
                    primary: existingArea == id, height: 64);
            ui.Button(page, "Mulai rekam", () => StartSession(existingArea, existingArea, null),
                primary: true, interactable: existingArea != null);
        }

        void StartSession(string name, string id, int? buildingId)
        {
            try
            {
                UseSession(CaptureSession.Create(workspace, name, id, buildingId, DateTime.UtcNow), reopening: false);
                ShowCamera(false);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                ShowHome("Sesi baru tidak dapat dibuat. Periksa ruang penyimpanan dan coba lagi.");
            }
        }

        void UseSession(CaptureSession value, bool reopening)
        {
            bool sameSession = reopening && session?.Dir == value.Dir;
            session = value;
            if (sameSession)
            {
                if (session.RecoveryNote != null) SetNotice(session.RecoveryNote, 8);
                return;
            }
            continuity = new PoseContinuity();
            trackingGate = new CaptureTrackingGate();
            baseSegment = reopening ? session.Records(MapRole).Concat(session.Records(TestRole))
                .Select(photo => photo.Segment + 1).DefaultIfEmpty(0).Max() : 0;
            auto = true;
            pointDisplay = false;
            points.Clear();
            notice = null;
            freeBytes = AvailableStorage();
            nextStorage = nextHud = 0;
            if (session.RecoveryNote != null) SetNotice(session.RecoveryNote, 8);
        }

        void ShowCamera(bool test)
        {
            Switch(test ? ScreenView.Test : ScreenView.Record);
            stable = tracking = false;
            trackingGate = new CaptureTrackingGate();
            cameraUi = ui.CameraPage(test, ShowHome, Undo, () => RequestShot(manual: true), FinishCamera,
                () => { auto = !auto; UpdateHud(); },
                () => { pointDisplay = !pointDisplay; points.SetVisible(pointDisplay); UpdateHud(); });
            RebuildFrustums();
            UpdateHud();
        }

        void Update()
        {
            if (ui == null) return;
            ui.FitSafeArea();
            if (!IsCamera || paused) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextStorage)
            {
                freeBytes = AvailableStorage();
                nextStorage = now + 1;
            }
            tracking = arSession.subsystem?.trackingState == TrackingState.Tracking;
            continuity.Observe(ToNum(arCamera.transform.position), now, tracking);
            bool eventDetected = continuity.Event != null;
            if (continuity.Event == "tracking_lost")
            {
                try { session.RecordTrackingLoss(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    SetNotice("Gangguan pelacakan tidak bisa disimpan.");
                }
            }
            stable = trackingGate.Observe(now, tracking, eventDetected);
            if (taking && now >= shotTimeout)
            {
                StopShot();
                SetNotice("Kamera belum mengirim foto.");
            }
            if (screen == ScreenView.Record && auto && stable && !taking) RequestShot(manual: false);
            if (now >= nextHud)
            {
                UpdateHud();
                nextHud = now + 0.15;
            }
        }

        void RequestShot(bool manual)
        {
            if (!IsCamera || taking || paused) return;
            bool test = screen == ScreenView.Test;
            string role = CurrentRole;
            var photos = session.Records(role);
            var last = photos.Count > 0 ? photos[photos.Count - 1] : null;
            var pose = arCamera.transform;
            NVector pos = ToNum(pose.position);
            ShotDecision decision = manual
                ? CapturePolicy.Manual(tracking, continuity.Speed, photos.Count, freeBytes)
                : CapturePolicy.Auto(tracking, continuity.Speed, pos, ToNum(pose.rotation), last != null,
                    last?.Position ?? default, last?.Rotation ?? NQuaternion.Identity, photos.Count, freeBytes);
            if (test && decision == ShotDecision.Capture)
                decision = CapturePolicy.TestShot(tracking, continuity.Speed, pos, photos.Select(p => p.Position).ToArray());
            if (decision != ShotDecision.Capture)
            {
                if (manual) SetNotice(CapturePolicy.Reason(decision));
                return;
            }
            int current = revision;
            taking = true;
            shotTimeout = Time.realtimeSinceStartupAsDouble + 2;
            frameCapture.Request((frame, error) => ReceivePhoto(current, role, frame, error, manual));
            UpdateHud();
        }

        void ReceivePhoto(int current, string role, CapturedFrame frame, string error, bool manual)
        {
            taking = false;
            if (revision != current || !IsCamera || paused) return;
            if (frame == null)
            {
                if (manual) SetNotice(error ?? "Kamera belum siap.");
                return;
            }
            if (arSession.subsystem?.trackingState != TrackingState.Tracking)
            {
                if (manual) SetNotice(CapturePolicy.Reason(ShotDecision.NotTracking));
                return;
            }
            var position = ToNum(frame.CamPosU);
            if (role == TestRole)
            {
                var decision = CapturePolicy.TestShot(true, continuity.Speed, position,
                    session.Records(TestRole).Select(p => p.Position).ToArray());
                if (decision != ShotDecision.Capture)
                {
                    SetNotice(CapturePolicy.Reason(decision));
                    return;
                }
            }

            try
            {
                session.Add(role, frame.Jpeg, new FrameRecord
                {
                    TimestampNs = frame.TimestampNs, Position = position, Rotation = ToNum(frame.CamRotU),
                    Tracking = "Tracking", K = frame.K, Segment = baseSegment + continuity.Segment,
                });
                freeBytes = Math.Max(0, freeBytes - frame.Jpeg.LongLength);
                RebuildFrustums();
                UpdateHud();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                auto = false;
                SetNotice("Foto tidak dapat disimpan. Periksa ruang penyimpanan.");
            }
        }

        void StopShot()
        {
            if (!taking) return;
            taking = false;
            frameCapture.Request(null);
        }

        void Undo()
        {
            if (taking) return;
            try
            {
                if (session.UndoLast(CurrentRole)) RebuildFrustums();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                SetNotice("Berkas foto terakhir tidak dapat dihapus.");
            }
            UpdateHud();
        }

        void FinishCamera()
        {
            if (taking) return;
            try
            {
                if (screen == ScreenView.Record && session.Count(MapRole) > 0)
                {
                    session.SetStage(SessionStage.Testing);
                    ShowCamera(true);
                }
                else if (screen == ScreenView.Test && session.Count(TestRole) >= CapturePolicy.MinTestPhotos)
                {
                    session.SetStage(SessionStage.Packaged);
                    ShowUpload();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                SetNotice("Perubahan sesi tidak dapat disimpan.");
            }
        }

        void RebuildFrustums() => frustums.Rebuild(session.Records(MapRole), session.Records(TestRole));

        void SetNotice(string message, double seconds = 2)
        {
            notice = message;
            noticeUntil = Time.realtimeSinceStartupAsDouble + seconds;
        }

        void UpdateHud()
        {
            if (!IsCamera || cameraUi == null) return;
            int count = session.Count(CurrentRole);
            double gib = freeBytes / (double)CapturePolicy.MinFreeBytes;
            var elapsed = DateTime.UtcNow - session.StartedUtc;
            cameraUi.Stats.text = screen == ScreenView.Test
                ? $"{count} dari minimal {CapturePolicy.MinTestPhotos} foto uji"
                : $"{count} foto   {CapturePolicy.DistanceM(session.Records(MapRole)):0.0} m   {elapsed:hh\\:mm\\:ss}\n{gib:0.00} GiB kosong";
            CaptureUi.Show(cameraUi.Tracking, !stable ? TrackingWarning : null);
            CaptureUi.Show(cameraUi.Notice, Time.realtimeSinceStartupAsDouble < noticeUntil ? notice :
                freeBytes < CapturePolicy.MinFreeBytes ? CapturePolicy.Reason(ShotDecision.LowStorage) : null);
            cameraUi.Undo.interactable = count > 0 && !taking;
            cameraUi.Shutter.interactable = !taking;
            cameraUi.Next.interactable = !taking && count >= (screen == ScreenView.Test ? CapturePolicy.MinTestPhotos : 1);
            CaptureUi.SetButtonText(cameraUi.Auto, auto ? "Otomatis aktif" : "Otomatis mati");
            CaptureUi.SetButtonText(cameraUi.Points, pointDisplay ? "Titik aktif" : "Titik mati");
        }

        long AvailableStorage()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var stat = new AndroidJavaObject("android.os.StatFs", Application.persistentDataPath);
                return stat.Call<long>("getAvailableBytes");
#else
                return new DriveInfo(Path.GetPathRoot(workspace)).AvailableFreeSpace;
#endif
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is AndroidJavaException)
            {
                return 0;
            }
        }

        void ShowUpload()
        {
            Switch(ScreenView.Upload);
            var page = ui.Page("Unggah", session.AreaName);
            var status = ui.Label(page, "Menyiapkan paket foto…");
            ui.Button(page, "Jeda", PauseTransfer);
            _ = Upload(status, revision);
        }

        async Task Upload(Text status, int current)
        {
            var run = new CancellationTokenSource();
            transfer = run;
            var ct = run.Token;
            int sleepBefore = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            try
            {
                var active = session;
                string version = Application.version;
                string model = SystemInfo.deviceModel;
                var files = await Task.Run(() => new[] { Package(active, MapRole, version, model),
                    Package(active, TestRole, version, model) }, ct);
                ct.ThrowIfCancellationRequested();
                var auth = Auth();
                if (active.JobId == null)
                {
                    int id = await new CaptureApi(http, auth).CreateJob(active.AreaId, active.AreaName,
                        files.Select(f => (f.name, f.role, f.size)).ToArray(), active.BuildingId);
                    active.SetJobId(id);
                }
                ct.ThrowIfCancellationRequested();
                active.SetStage(SessionStage.Uploading);
                var uploader = new TusUploader(http, auth);
                long done = 0, total = files.Sum(f => f.size);
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    string stateFile = active.UploadStatePath(file.role);
                    var state = UploadState.LoadOrCreate(stateFile, active.JobId.Value, file.name, file.path, file.size);
                    long baseBytes = done;
                    var progress = new Progress<double>(fraction =>
                    {
                        if (this != null && current == revision)
                            status.text = "Mengunggah " + ((baseBytes + fraction * file.size) / total).ToString("P0");
                    });
                    await uploader.Upload(state, stateFile, progress, ct);
                    done += file.size;
                }
                active.SetStage(SessionStage.Submitted);
                if (this != null && current == revision) ShowStatus();
            }
            catch (OperationCanceledException)
            {
                if (this != null && current == revision)
                    ShowUploadFailure("Unggahan dijeda.", null);
            }
            catch (Exception e)
            {
                if (this != null && current == revision) ShowUploadFailure(FailureMessage(e), e);
            }
            finally
            {
                if (transfer == run) transfer = null;
                run.Dispose();
                Screen.sleepTimeout = sleepBefore;
            }
        }

        static (string name, string role, string path, long size) Package(CaptureSession active, string role,
            string version, string model)
        {
            string path = FramePackage.PrepareForUpload(active, role,
                CaptureMeta.Json(version, model, active.StartedUtc, active.Count(role), active.TrackingLosses, role), "{}");
            return (Path.GetFileName(path), role, path, new FileInfo(path).Length);
        }

        void ShowUploadFailure(string message, Exception error)
        {
            var page = ui.Page("Unggah", session.AreaName, ShowHome);
            ui.Label(page, message);
            if (error is CaptureHttpException { Kind: FailureKind.JobClosed })
            {
                ui.Button(page, "Buat pekerjaan baru", () =>
                {
                    try { session.ResetUpload(); ShowUpload(); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        ShowUploadFailure("Status unggahan tidak dapat direset.", error);
                    }
                }, primary: true);
                return;
            }
            ui.Button(page, "Coba lagi", ShowUpload, primary: true);
            if (error is CaptureHttpException { Kind: FailureKind.TokenRejected })
                ui.Button(page, "Ganti token", () => ShowSettings(ShowUpload));
        }

        void ShowStatus()
        {
            Switch(ScreenView.Status);
            var page = ui.Page("Status", session.AreaName, ShowHome);
            if (session.JobId.HasValue) ui.Label(page, $"Pekerjaan #{session.JobId.Value}", muted: true);
            var label = ui.Label(page, "Memuat status pekerjaan…", bold: true);
            _ = Poll(label, revision, session.JobId);
        }

        async Task Poll(Text label, int current, int? job)
        {
            try
            {
                if (!job.HasValue) throw new InvalidOperationException("Sesi belum memiliki pekerjaan.");
                var api = Api();
                while (this != null && revision == current)
                {
                    if (!paused)
                    {
                        var snapshot = await api.GetJob(job.Value);
                        if (this == null || revision != current) return;
                        var display = JobStatusView.Map(snapshot);
                        label.text = display.text;
                        if (display.view == JobView.Held || display.view == JobView.Failed)
                        {
                            ui.Button(label.transform.parent, "Rekam ulang",
                                () => StartSession(session.AreaName, session.AreaId, null), primary: true);
                            return;
                        }
                        if (display.view == JobView.ReadyToTest || display.view == JobView.Deleting) return;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception e)
            {
                if (this != null && revision == current) ShowFailure("Status", e, ShowStatus, ShowHome);
            }
        }

        void ShowFailure(string title, Exception error, Action retry, Action back)
        {
            var page = ui.Page(title, null, back);
            ui.Label(page, FailureMessage(error));
            ui.Button(page, "Coba lagi", retry, primary: true);
            if (error is CaptureHttpException { Kind: FailureKind.TokenRejected })
                ui.Button(page, "Ganti token", () => ShowSettings(retry));
        }

        static string FailureMessage(Exception error) => error switch
        {
            CaptureHttpException { Kind: FailureKind.TokenRejected } =>
                "Token aplikasi ditolak. Periksa atau ganti token di Pengaturan.",
            CaptureHttpException { Kind: FailureKind.AccessRedirect } => "Akses Cloudflare menolak permintaan.",
            CaptureHttpException { Kind: FailureKind.JobClosed } => "Pekerjaan ini sudah tidak menerima unggahan.",
            CaptureHttpException { Kind: FailureKind.Retryable } => "Koneksi bermasalah.",
            CaptureHttpException httpError => httpError.Detail,
            IOException => "Berkas sesi tidak dapat dibaca atau ditulis.",
            _ => error.Message,
        };

        CaptureApi Api() => new CaptureApi(http, Auth());

        CaptureAuth Auth()
        {
            if (config == null) throw new InvalidOperationException("Konfigurasi server belum tersedia pada APK.");
            return new CaptureAuth { BaseUrl = config.BaseUrl, ClientId = config.ClientId,
                ClientSecret = config.ClientSecret, AppToken = appToken };
        }

        void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (!isPaused) return;
            StopShot();
            PauseTransfer();
        }

        void PauseTransfer()
        {
            transfer?.Cancel();
            http.AbortUploads();
        }

        void OnDestroy()
        {
            StopShot();
            PauseTransfer();
            ui?.Dispose();
        }

        // Konverter assembly aplikasi navigasi bersifat internal.
        static NVector ToNum(Vector3 value) => new NVector(value.x, value.y, value.z);
        static NQuaternion ToNum(Quaternion value) => new NQuaternion(value.x, value.y, value.z, value.w);
    }
}
