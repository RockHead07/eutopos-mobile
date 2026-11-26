# Aplikasi capture, irisan A (merekam sampai status) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** APK terpisah "Eutopos Capture" yang merekam foto berpose ARCore (otomatis dan manual, bingkai kamera melayang,
batal foto), mengambil minimal 5 foto uji, mengunggah dua paket ZIP lewat tus dengan token aplikasi, dan menampilkan
status pekerjaan sampai "Siap diuji", "Ditahan", atau "Gagal".

**Architecture:** Semua logika murni (aturan foto, sesi di disk, paket ZIP, klien tus dan API pekerjaan lewat antarmuka
HTTP, pemetaan status) masuk `Eutopos.Core` (tanpa UnityEngine, `System.Numerics`, dites EditMode). Bagian yang
menyentuh AR dan UI masuk assembly baru `Eutopos.Capture` yang hanya dikompilasi di Editor dan di build capture, sehingga
APK pengunjung tidak memuatnya. Build capture dibuat oleh menu Editor dengan `extraScriptingDefines`, seperti pola
`BuildSpike`.

**Tech Stack:** Unity 6000.3.14f1 (Built-in RP), AR Foundation/ARCore, uGUI dibangun lewat kode, Newtonsoft.Json,
`System.IO.Compression`, NUnit EditMode.

**Spec:** `docs/specs/2026-11-23-aplikasi-capture-matang-design.md` bagian 7 (layar rekam) di atas
`docs/specs/2026-11-05-mode-rekam-peta-irisan-1-design.md` bagian 3 sampai 6 dan 8 (alur, aturan foto, paket, tus,
pemisahan aplikasi). Server pasangannya sudah di production: eutopos-vps PR #112 (token aplikasi, pekerjaan aplikasi
berhenti sebagai kandidat).

## Global Constraints

- **Jangan commit, push, PR, atau merge.** Bagus yang memutuskan. Langkah "Commit" diganti mencatat berkas di laporan.
- Kode di `Assets/Eutopos/Scripts/Core/` tetap `noEngineReferences: true`: tanpa `UnityEngine`, vektor memakai
  `System.Numerics`. Konversi ke tipe Unity lewat `Conv.cs` yang ada.
- Tanpa paket atau dependensi baru. Hanya yang sudah ada di proyek (AR Foundation, Newtonsoft.Json, uGUI).
- Jangan mengubah perilaku aplikasi navigasi (`Assets/Eutopos/Scripts/App/`, `Main.unity`, `BuildAndroid.cs`) kecuali
  yang disebut tugas. Kode navigasi tidak boleh merujuk `Eutopos.Capture`.
- Jangan mengubah `Assets/Eutopos/Spike/` (spike tetap apa adanya); logika yang dipakai ulang disalin ke Core lalu
  dites di sana.
- Nilai yang dikunci spesifikasi: foto otomatis bila bergeser minimal **0,4 m** atau berputar minimal **10 derajat**,
  hanya saat **Tracking**, dilewati bila kecepatan di atas **0,7 m/s**; batas **1500** foto per sesi; berhenti bila ruang
  kosong kurang dari **1 GB**; minimal **5** foto uji, saling berjarak minimal **1 m**; potongan tus **50 MiB**; poll
  status tiap **5 detik**; `area_id` cocok `^[a-z0-9][a-z0-9-]{0,39}$`; nama foto `000001.jpg` berurutan tanpa celah.
- Paket ZIP harus lolos validator server `server/frame_package.py` (eutopos-vps): isi hanya `images/NNNNNN.jpg`,
  `frames.jsonl`, `meta.json`, `configs.json`; tiap baris `frames.jsonl` merujuk satu foto yang ada, tanpa ulang.
- Header setiap permintaan ke server: `CF-Access-Client-Id`, `CF-Access-Client-Secret` (dari `VpsConfig` yang ada),
  `Authorization: Bearer <token aplikasi>`, dan `redirectLimit = 0`.
- Teks UI bahasa Indonesia. Komentar bahasa Indonesia, istilah teknis Inggris, tanpa em dash.
- Perintah tes EditMode (dipakai di semua tugas, sebut `TES` di bawah):
  `& "D:\Dev\Tools\Unity Editors\6000.3.14f1\Editor\Unity.exe" -batchmode -projectPath "D:\wt\capture-design" -runTests -testPlatform EditMode -testResults "D:\wt\capture-design\TestResults-EditMode.xml" -logFile -`
  Tambah `-testFilter <NamaKelasTes>` untuk menjalankan satu kelas. Worktree ini belum punya folder `Library`, jadi
  jalankan pertama memakan waktu impor; jangan membuka proyek yang sama di Editor bersamaan.

## Review Focus

1. Aplikasi ditutup paksa di tengah menulis foto: ada JPEG tanpa baris `frames.jsonl`, atau baris yang JPEG-nya
   terpotong. Saat sesi dibuka lagi, keduanya harus dibuang sampai pasangan terakhir yang utuh, dan penomoran tetap
   tanpa celah. Dikunci di Tugas 3.
2. Batal foto pada sesi kosong, atau setelah sesi dibuka ulang, tidak boleh merusak berkas atau penomoran. Dikunci di
   Tugas 3.
3. Unggahan dilanjutkan setelah aplikasi dibuka ulang, tetapi unggahan tus di server sudah hilang (404 atau 410 pada
   `HEAD`): berkas itu harus dimulai ulang dari 0 dengan unggahan baru, bukan gagal permanen. Dikunci di Tugas 5.
4. Token aplikasi dicabut atau salah (401/403), atau Access mengalihkan (3xx): jangan coba ulang tanpa akhir; tampilkan
   alasan yang bisa ditindaklanjuti. Dikunci di Tugas 5.
5. Pekerjaan sudah tidak `uploading` saat unggahan dilanjutkan (hook menjawab 409): hentikan dan tawarkan membuat
   pekerjaan baru dari paket yang sama. Dikunci di Tugas 5.

---

## File Structure

Core (murni, dites):
- `Assets/Eutopos/Scripts/Core/Capture/CapturePolicy.cs`: keputusan foto otomatis, manual, foto uji, batas.
- `Assets/Eutopos/Scripts/Core/Capture/PoseContinuity.cs`: salinan dari cabang spike, versi `System.Numerics`.
- `Assets/Eutopos/Scripts/Core/Capture/CaptureRecords.cs`: `AreaId`, baris `frames.jsonl`, isi `meta.json`.
- `Assets/Eutopos/Scripts/Core/Capture/CaptureSession.cs`: folder sesi, tambah, batal, pulih, manifest.
- `Assets/Eutopos/Scripts/Core/Capture/FramePackage.cs`: menulis ZIP.
- `Assets/Eutopos/Scripts/Core/Capture/CaptureHttp.cs`: antarmuka HTTP, klien tus, API pekerjaan, keadaan unggah.
- `Assets/Eutopos/Scripts/Core/Capture/JobStatusView.cs`: status pekerjaan ke tampilan.
- Tes: `Assets/Eutopos/Tests/EditMode/Capture*Tests.cs` (assembly `Eutopos.Core.Tests` yang ada).

Capture (AR dan UI, assembly baru):
- `Assets/Eutopos/Capture/Eutopos.Capture.asmdef`
- `Assets/Eutopos/Capture/CaptureController.cs`: mesin layar dan alur.
- `Assets/Eutopos/Capture/CaptureUi.cs`: uGUI dibangun lewat kode.
- `Assets/Eutopos/Capture/FrustumLayer.cs`, `Assets/Eutopos/Capture/PointLayer.cs`
- `Assets/Eutopos/Capture/UnityHttp.cs`: implementasi `ICaptureHttp` dengan `UnityWebRequest`.
- `Assets/Eutopos/Capture/Editor/Eutopos.Capture.Editor.asmdef`, `Assets/Eutopos/Capture/Editor/BuildCapture.cs`
- `Assets/Eutopos/Capture/Capture.unity` (dibuat oleh `BuildCapture.CreateScene`)

---

### Task 1: Aturan foto dan kesinambungan pose

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/Capture/CapturePolicy.cs`, `Assets/Eutopos/Scripts/Core/Capture/PoseContinuity.cs`
- Test: `Assets/Eutopos/Tests/EditMode/CapturePolicyTests.cs`

**Interfaces:**
- Produces (namespace `Eutopos.Core.Capture`):
  - `enum ShotDecision { Capture, NotTracking, TooFast, BelowThreshold, LimitReached, LowStorage, TooCloseToTest }`
  - `static class CapturePolicy` dengan konstanta `MinMoveM = 0.4f`, `MinTurnDeg = 10f`, `MaxSpeedMps = 0.7f`,
    `MaxPhotos = 1500`, `MinFreeBytes = 1L << 30`, `MinTestPhotos = 5`, `MinTestSpacingM = 1f`.
  - `ShotDecision Auto(bool tracking, float speed, Vector3 pos, Quaternion rot, bool hasPrevious, Vector3 prevPos, Quaternion prevRot, int count, long freeBytes)`
  - `ShotDecision Manual(bool tracking, float speed, int count, long freeBytes)`
  - `ShotDecision TestShot(bool tracking, float speed, Vector3 pos, IReadOnlyList<Vector3> previousTests)`
  - `string Reason(ShotDecision d)`: teks Indonesia singkat untuk UI ("Pelacakan belum siap", "Terlalu cepat, pelankan",
    "Batas 1500 foto tercapai", "Ruang penyimpanan kurang dari 1 GB", "Terlalu dekat dengan foto uji lain (min 1 m)");
    `null` untuk `Capture` dan `BelowThreshold`.
  - `sealed class PoseContinuity` dengan `MinJumpM = 0.5f`, `MinJumpSpeedMps = 1.4f`, `int Segment`, `string Event`,
    `float Speed`, `bool Observe(Vector3 position, double now, bool tracking)`; perilaku sama persis dengan
    `D:/wt/spike-depth-mobile/Assets/Eutopos/Spike/PoseContinuity.cs` (cabang `spike/mesh-depth`, hanya baca).
  - Urutan pemeriksaan `Auto`: tracking, batas foto, ruang, kecepatan, lalu ambang gerak. Sudut dihitung seperti
    `Keyframes.Decide` di spike (nilai mutlak dot kuaternion, q dan -q sama).

- [ ] **Step 1: Tes gagal** `CapturePolicyTests`:
  - Semua kasus `KeyframeTests` di `Assets/Eutopos/Spike/Tests/KeyframeTests.cs` disalin ke `Auto` dengan hasil sama
    (termasuk 0,39 m dan 9,9 derajat menjadi `BelowThreshold`, dan tanda kuaternion saja bukan gerakan).
  - `Auto` dengan `count = 1500` -> `LimitReached`; `freeBytes = (1L << 30) - 1` -> `LowStorage`.
  - `Manual` tidak memakai ambang gerak: diam di tempat -> `Capture`; tidak tracking -> `NotTracking`; 0,71 m/s ->
    `TooFast`.
  - `TestShot` 0,99 m dari foto uji lain -> `TooCloseToTest`; 1,0 m -> `Capture`; daftar kosong -> `Capture`.
  - `PoseContinuity`: hilang tracking menaikkan `Segment` dan `Event == "tracking_lost"`; lompatan 0,6 m dalam 0,2 s
    menaikkan `Segment` dan `Event == "position_jump"`; jalan 0,1 m per 0,2 s tidak.
- [ ] **Step 2:** `TES -testFilter CapturePolicyTests`. Expected: gagal kompilasi tes (tipe belum ada).
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** `TES -testFilter CapturePolicyTests`. Expected: semua lulus.
- [ ] **Step 5:** Catat berkas.

### Task 2: Nama area, baris foto, dan meta

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/Capture/CaptureRecords.cs`
- Test: `Assets/Eutopos/Tests/EditMode/CaptureRecordsTests.cs`

**Interfaces:**
- Produces:
  - `static class AreaId { string FromName(string name, DateTime now); bool IsValid(string id); }`. `FromName`:
    huruf kecil, setiap rangkaian karakter selain `a-z0-9` menjadi satu `-`, buang `-` di awal dan akhir, potong 40
    karakter lalu buang `-` di akhir lagi; hasil kosong -> `"area-" + now.ToString("yyyyMMdd-HHmm")`.
  - `sealed class FrameRecord { string File; long TimestampNs; Vector3 Position; Quaternion Rotation; string Tracking;
    Intrinsics K; int Segment; }` dan `static string FrameRecord.ToLine(FrameRecord r)` (JSON satu baris, kunci `file`,
    `t_ns`, `position`, `rotation_xyzw`, `tracking`, `K` {`fx`,`fy`,`cx`,`cy`,`width`,`height`}, `segment`),
    `static FrameRecord FromLine(string line)`. `Intrinsics` adalah struct Core yang sudah ada.
  - `static string CaptureMeta.Json(string appVersion, string deviceModel, DateTime startedUtc, int photos, int trackingLosses, string role)`
    dengan kunci tambahan tetap `coordinate_system = "unity_world"`, `up_axis = "Y"`, `handedness = "left"`.

- [ ] **Step 1: Tes gagal** `CaptureRecordsTests`:
  - `FromName("Lorong Lantai 10 / Sayap B", t) == "lorong-lantai-10-sayap-b"`; `FromName("  ---  ", t)` diawali `"area-"`;
    nama 60 huruf -> panjang 40 dan `IsValid`; hasil selalu `IsValid` untuk 200 string acak (seed tetap).
  - `ToLine` lalu `FromLine` mengembalikan nilai sama; `t_ns = 12_345_678_901` utuh (tidak jadi float).
  - `CaptureMeta.Json` memuat ketiga kunci koordinat dan `photo_count`.
- [ ] **Step 2:** `TES -testFilter CaptureRecordsTests`. Expected: gagal.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** `TES -testFilter CaptureRecordsTests`. Expected: lulus.
- [ ] **Step 5:** Catat berkas.

### Task 3: Sesi rekam di disk (tambah, batal, pulih)

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/Capture/CaptureSession.cs`
- Test: `Assets/Eutopos/Tests/EditMode/CaptureSessionTests.cs` (folder sementara di `Path.GetTempPath()`, dihapus di
  `TearDown`)

**Interfaces:**
- Consumes: `FrameRecord` (Task 2).
- Produces:
  - Tata letak: `<root>/<sessionId>/session.json`, `<root>/<sessionId>/peta/{images/,frames.jsonl}`,
    `<root>/<sessionId>/uji/{images/,frames.jsonl}`. `sessionId = "yyyyMMdd-HHmmss"` waktu mulai.
  - `enum SessionStage { Recording, Testing, Packaged, Uploading, Submitted }`
  - `sealed class CaptureSession`:
    - `static CaptureSession Create(string root, string areaName, string areaId, DateTime now)`
    - `static CaptureSession Open(string sessionDir)`: memulihkan dan merapikan (lihat bawah).
    - `static IReadOnlyList<string> Unfinished(string root)`: folder sesi dengan `Stage != Submitted`, terbaru dulu.
    - `string Dir`, `string AreaName`, `string AreaId`, `SessionStage Stage`, `int? JobId`, `void SetStage(SessionStage s)`,
      `void SetJobId(int id)` (keduanya langsung menulis `session.json`).
    - `int Count(string role)` (`role` adalah `"peta"` atau `"uji"`), `IReadOnlyList<FrameRecord> Records(string role)`.
    - `string Add(string role, byte[] jpeg, FrameRecord record)`: menulis `images/NNNNNN.jpg` lewat berkas sementara lalu
      rename, baru menambah baris `frames.jsonl` dan flush; mengisi `record.File`; mengembalikan nama berkas.
    - `bool UndoLast(string role)`: hapus baris terakhir (tulis ulang `frames.jsonl` lewat berkas sementara lalu
      rename) dan JPEG-nya; `false` bila kosong.
  - Pemulihan di `Open`, per role: buang baris yang tidak bisa diurai atau JPEG-nya tidak ada atau berukuran 0; buang
    JPEG yang tidak punya baris; buang berkas sementara; pastikan nomor `1..n` tanpa celah (bila ada celah, potong di
    celah pertama). Hasil dicatat di `string RecoveryNote` (null bila tidak ada yang dibuang).

- [ ] **Step 1: Tes gagal** `CaptureSessionTests`:
  - `AddThenUndoKeepsNumberingGapless`: tambah 3, batal 1, tambah 1 -> berkas `000001..000003.jpg`, 3 baris, baris
    terakhir `file == "000003.jpg"`.
  - `UndoOnEmptyReturnsFalse`: sesi baru, `UndoLast("peta") == false`, folder utuh.
  - `OpenDropsJpegWithoutLine`: tulis `000004.jpg` manual tanpa baris -> setelah `Open`, `Count == 3`, berkas 4 hilang,
    `RecoveryNote != null`.
  - `OpenDropsTruncatedLastLine`: tambahkan setengah baris JSON di akhir `frames.jsonl` -> dibuang.
  - `OpenDropsLineWhoseJpegIsEmpty`: kosongkan `000003.jpg` -> `Count == 2`.
  - `UndoAfterReopenWorks`: `Open` lalu `UndoLast` -> `Count == 2`.
  - `UnfinishedListsNewestFirstAndSkipsSubmitted`.
  - `RolesAreIndependent`: foto `uji` bernomor sendiri mulai `000001.jpg`.
- [ ] **Step 2:** `TES -testFilter CaptureSessionTests`. Expected: gagal.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** `TES -testFilter CaptureSessionTests`. Expected: lulus.
- [ ] **Step 5:** Catat berkas.

### Task 4: Paket ZIP

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/Capture/FramePackage.cs`
- Test: `Assets/Eutopos/Tests/EditMode/FramePackageTests.cs`

**Interfaces:**
- Consumes: `CaptureSession` (Task 3), `CaptureMeta` (Task 2).
- Produces: `static string FramePackage.Write(CaptureSession s, string role, string metaJson, string configsJson)`:
  menulis `<Dir>/<role>.zip` lewat berkas sementara lalu rename dan mengembalikan path-nya. Isi persis
  `images/NNNNNN.jpg` (tersimpan tanpa kompresi, `CompressionLevel.NoCompression`), `frames.jsonl`, `meta.json`,
  `configs.json` (`configsJson` boleh `"{}"`), nama entri memakai `/`, tanpa entri folder. Menolak role kosong
  (`InvalidOperationException`).

- [ ] **Step 1: Tes gagal** `FramePackageTests`: buat sesi 3 foto (byte JPEG minimal tetap di tes, diawali
  `FF D8` dan diakhiri `FF D9`), tulis paket, buka dengan `ZipArchive`:
  - nama entri persis `{images/000001.jpg, images/000002.jpg, images/000003.jpg, frames.jsonl, meta.json, configs.json}`;
  - tidak ada entri berakhiran `/`, tidak ada `\`;
  - entri JPEG `CompressedLength == Length`;
  - tiap baris `frames.jsonl` merujuk satu JPEG yang ada dan tidak berulang;
  - role kosong melempar `InvalidOperationException`.
- [ ] **Step 2:** `TES -testFilter FramePackageTests`. Expected: gagal.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** `TES -testFilter FramePackageTests`. Expected: lulus.
- [ ] **Step 5:** Validasi silang dengan validator server: tes menyimpan satu paket ke `%TEMP%\eutopos-paket-uji.zip`
  bila variabel lingkungan `EUTOPOS_KEEP_PACKAGE=1`. Lalu di repo eutopos-vps (hanya baca, tanpa mengubah berkas, folder
  keluaran di `%TEMP%`):
  `cd D:\Dev\Projects\eutopos-vps; uv run --no-sync python -c "import os,tempfile; from pathlib import Path; from server.frame_package import extract; t=Path(tempfile.mkdtemp()); print(extract(Path(os.environ['TEMP'])/'eutopos-paket-uji.zip', t/'images', t/'meta', True))"`
  Expected: mencetak `3`, tanpa `PackageError`. Tulis perintah dan hasilnya di laporan.
- [ ] **Step 6:** Catat berkas.

### Task 5: Klien tus, API pekerjaan, dan status

**Files:**
- Create: `Assets/Eutopos/Scripts/Core/Capture/CaptureHttp.cs`, `Assets/Eutopos/Scripts/Core/Capture/JobStatusView.cs`
- Test: `Assets/Eutopos/Tests/EditMode/CaptureHttpTests.cs`, `Assets/Eutopos/Tests/EditMode/JobStatusViewTests.cs`

**Interfaces:**
- Produces:
  - `sealed class HttpCall { string Method; string Url; Dictionary<string,string> Headers; byte[] Body; }`,
    `sealed class HttpReply { long Status; Dictionary<string,string> Headers; string Text; bool NetworkError; }`
    (nama header dicari tanpa peka huruf).
  - `interface ICaptureHttp { Task<HttpReply> Send(HttpCall call); }`
  - `sealed class CaptureAuth { string BaseUrl; string ClientId; string ClientSecret; string AppToken; }` dan
    `void CaptureAuth.Apply(HttpCall c)` (tiga header di Global Constraints).
  - `enum FailureKind { None, Retryable, TokenRejected, AccessRedirect, JobClosed, UploadGone, Fatal }` dan
    `static FailureKind Classify(HttpReply r)`: jaringan atau 5xx atau 408/429 -> `Retryable`; 401/403 ->
    `TokenRejected`; 3xx -> `AccessRedirect`; 409 -> `JobClosed`; 404/410 -> `UploadGone`; 2xx -> `None`; lainnya
    `Fatal`.
  - `static TimeSpan RetryDelay(int attempt)`: 1, 2, 4, 8, 16, 30, 30 ... detik; `MaxAttempts = 8`.
  - `sealed class CaptureApi(ICaptureHttp http, CaptureAuth auth)`:
    - `Task<int> CreateJob(string areaId, string areaName, IReadOnlyList<(string name, string role, long size)> files)`:
      `POST {base}/api/jobs`, body `{area_id, area_name, videos:[{name, role, size, kind:"frames"}]}`.
    - `Task<JobSnapshot> GetJob(int jobId)`: `GET {base}/api/jobs/{id}`.
    - Kegagalan dilempar sebagai `CaptureHttpException { FailureKind Kind; long Status; string Detail; }`
      (`Detail` dari `detail` JSON FastAPI bila ada).
  - `sealed class UploadState { int JobId; string Name; string Path; long Size; string Url; long Offset; }` disimpan
    sebagai `<sessionDir>/upload-<name>.json` lewat `UploadState.Save/Load`.
  - `sealed class TusUploader(ICaptureHttp http, CaptureAuth auth, int chunkBytes = 50 * 1024 * 1024)`:
    `Task Upload(UploadState st, string stateFile, IProgress<double> progress, CancellationToken ct)`:
    - `st.Url == null`: `POST {base}/files/` dengan `Tus-Resumable: 1.0.0`, `Upload-Length`, `Upload-Metadata`
      `job_id <b64>,name <b64>`; simpan `Location` (absolutkan bila relatif) ke `st.Url`, `Offset = 0`, simpan berkas
      keadaan.
    - Bila `st.Url` ada: `HEAD` untuk `Upload-Offset`. `UploadGone` -> `Url = null`, `Offset = 0`, buat ulang.
    - `PATCH` dari offset dengan `Content-Type: application/offset+octet-stream`, potongan paling besar `chunkBytes`,
      perbarui `Offset` dari header balasan, simpan keadaan setelah setiap potongan.
    - `Retryable` dicoba ulang dengan `RetryDelay` (penundaan lewat parameter `Func<TimeSpan, Task> delay` di
      konstruktor, bawaan `Task.Delay`, supaya tes tidak menunggu); habis percobaan atau jenis lain ->
      `CaptureHttpException`.
  - `sealed class JobSnapshot { int Id; string Status; string Stage; string Error; string GateDecision;
    IReadOnlyList<string> GateReasons; int? MapVersionId; bool Stalled; }` dari JSON `JobOut` server
    (`summary.gate.decision`, `summary.gate.reasons[].message`).
  - `enum JobView { Uploading, Queued, Processing, ReadyToTest, Held, Failed, Deleting }` dan
    `static (JobView view, string text) JobStatusView.Map(JobSnapshot j)`: `uploading` -> Mengunggah; `queued` -> Antre;
    `running` -> "Memproses (<stage>)"; `done` + `publish` -> "Siap diuji"; `done` + `hold` -> "Ditahan: <alasan
    digabung>"; `failed` -> "Gagal: <error baris pertama>"; `deleting` -> Dihapus. `Stalled` menambah " (macet)".

- [ ] **Step 1: Tes gagal** `CaptureHttpTests` dengan `FakeHttp : ICaptureHttp` yang merekam panggilan dan memberi
  balasan berurutan, `delay` palsu tanpa menunggu:
  - `CreateJobSendsFramesKindAndAllAuthHeaders`: body memuat dua entri `kind == "frames"`; header memuat ketiga header
    auth.
  - `UploadSendsChunksFromOffset`: berkas 120 byte, `chunkBytes = 50` -> tiga `PATCH` dengan `Upload-Offset` 0, 50, 100.
  - `ResumeUsesHeadOffset`: `Url` ada, `HEAD` menjawab offset 50 -> `PATCH` pertama mulai 50.
  - `ResumeRecreatesWhenUploadGone`: `HEAD` 404 -> `POST /files/` baru, lalu `PATCH` dari 0.
  - `RetryableErrorsAreRetriedThenGiveUp`: lima balasan 503 lalu 204 -> sukses; sembilan 503 -> `CaptureHttpException`
    `Retryable`.
  - `TokenRejectedIsNotRetried`: 401 -> satu panggilan saja, `Kind == TokenRejected`; 302 -> `AccessRedirect`.
  - `JobClosedOnCreateUpload`: `POST /files/` 409 -> `Kind == JobClosed`.
  - `StateIsSavedAfterEachChunk`: setelah `PATCH` kedua gagal permanen, berkas keadaan berisi `Offset == 50`.
- [ ] **Step 2: Tes gagal** `JobStatusViewTests`: setiap baris pemetaan di Interfaces, termasuk `done`+`hold` dengan dua
  alasan, `failed` dengan error multi-baris, dan `Stalled`.
- [ ] **Step 3:** `TES -testFilter CaptureHttpTests` dan `JobStatusViewTests`. Expected: gagal.
- [ ] **Step 4:** Implementasi.
- [ ] **Step 5:** Kedua tes lulus, lalu `TES` penuh. Expected: semua lulus, tes lama tetap lulus.
- [ ] **Step 6:** Catat berkas.

### Task 5b: Revisi Core setelah tiket 1 (gedung dan ZIP bawaan)

Ditambahkan 2026-11-23 setelah verifikasi tiket 1. Dua temuan: (1) server menolak area baru tanpa `building_id`
(`server/db.py` `ensure_area`), keputusan Bagus: perekam memilih gedung di aplikasi; (2) penulis ZIP Stored buatan
sendiri di `FramePackage.cs` hanya ada karena tes Tugas 4 mewajibkan Stored, padahal server menerima DEFLATE.

**Files:**
- Modify: `Assets/Eutopos/Scripts/Core/Capture/CaptureHttp.cs`, `Assets/Eutopos/Scripts/Core/Capture/FramePackage.cs`,
  `Assets/Eutopos/Scripts/Core/Capture/CaptureSession.cs`
- Test: `Assets/Eutopos/Tests/EditMode/CaptureHttpTests.cs`, `Assets/Eutopos/Tests/EditMode/FramePackageTests.cs`,
  `Assets/Eutopos/Tests/EditMode/CaptureSessionTests.cs` (tambah `BuildingIdRoundTripsAndDefaultsToNull`)

**Interfaces:**
- Produces:
  - `sealed class BuildingInfo { int Id; string Slug; string Name; IReadOnlyList<string> AreaIds; }`
  - `Task<IReadOnlyList<BuildingInfo>> CaptureApi.ListBuildings()`: `GET {base}/api/buildings` (server: tiket server
    kecil yang mengizinkan token aplikasi di rute ini).
  - `CaptureApi.CreateJob` mendapat parameter terakhir `int? buildingId`; dikirim sebagai `building_id` bila tidak null,
    dihilangkan bila null (area yang sudah ada).
  - `CaptureSession.Create` mendapat parameter `int? buildingId` setelah `areaId`; disimpan di `session.json` dan
    dibaca kembali oleh `Open` sebagai `int? BuildingId`. Sesi lama tanpa kunci itu terbaca `null`.
  - `FramePackage.Write` memakai `System.IO.Compression.ZipArchive` (`ZipArchiveMode.Create`, entri JPEG
    `CompressionLevel.NoCompression`, teks `Optimal`) menggantikan `StoredZip`. Tata letak dan nama entri tidak berubah.

- [ ] **Step 1: Tes gagal:** `ListBuildingsParsesAreaIds`; `CreateJobSendsBuildingIdOnlyWhenGiven` (dengan 3 ->
  `"building_id": 3`; null -> kunci tidak ada). Di `FramePackageTests`, hapus assert `CompressedLength == Length` dan
  ganti dengan `ZipEntriesAreStoredOrDeflated` (metode kompresi tiap entri 0 atau 8, dibaca dari header lokal) serta
  `EntriesRoundTripByteForByte` (isi tiap JPEG yang dibaca ulang identik dengan berkas sumber).
- [ ] **Step 2:** `TES -testFilter CaptureHttpTests` dan `FramePackageTests`. Expected: tes baru gagal.
- [ ] **Step 3:** Implementasi; hapus kelas `StoredZip`.
- [ ] **Step 4:** Kedua tes lulus, lalu `TES` penuh. Ulangi validasi silang Tugas 4 Step 5. Expected: `extract`
  mencetak `3`.
- [ ] **Step 5:** Catat berkas.

### Task 6: Assembly capture terpisah dan build APK capture

**Files:**
- Create: `Assets/Eutopos/Capture/Eutopos.Capture.asmdef`, `Assets/Eutopos/Capture/UnityHttp.cs`,
  `Assets/Eutopos/Capture/Editor/Eutopos.Capture.Editor.asmdef`, `Assets/Eutopos/Capture/Editor/BuildCapture.cs`,
  `Assets/Eutopos/Capture/CaptureController.cs` (kerangka: layar "Siap" saja di tugas ini)

**Interfaces:**
- `Eutopos.Capture.asmdef`: rujukan `Eutopos.Core`, `Eutopos.App`, `Unity.XR.ARFoundation`,
  `Unity.XR.ARSubsystems`, `UnityEngine.UI`; `defineConstraints: ["UNITY_EDITOR || EUTOPOS_CAPTURE"]`. Bila Unity
  6000.3 tidak menerima operator `||` di define constraint, berhenti dan tulis alternatif yang kamu usulkan di laporan
  sebelum mengubah pendekatan.
- `UnityHttp : ICaptureHttp` dengan `UnityWebRequest` (`redirectLimit = 0`, `timeout = 30` detik, body mentah untuk
  `PATCH`), memakai `await` pada `SendWebRequest()`.
- `BuildCapture`:
  - `[MenuItem("Eutopos/Build Capture APK")] static void Build()`: scene `Assets/Eutopos/Capture/Capture.unity`
    (dibuat `CreateScene()` bila belum ada), `extraScriptingDefines = { "EUTOPOS_CAPTURE" }`, application id asli +
    `".capture"`, product name `"Eutopos Capture"`, keluaran `Builds/eutopos-capture.apk`, dan pengaturan asli
    dipulihkan di `finally` (pola `Assets/Eutopos/Spike/Editor/BuildSpike.cs`). Ikon bawaan proyek (ikon khusus
    capture menyusul).
  - `static void CreateScene()`: AR Session, XR Origin dengan `ARCameraManager`, `ARPointCloudManager`,
    `FrameCapture`, satu GameObject `CaptureController`, Canvas uGUI. Ikuti `BuildSpike.CreateScene`.
- Token aplikasi: layar "Pengaturan" pertama kali meminta token (field kata sandi), disimpan di `PlayerPrefs` kunci
  `eutopos.capture.app_token`; tombol "Ganti token". Token tidak pernah ditulis ke log. `VpsConfig` yang ada dipakai
  untuk `base_url` dan token layanan.

- [ ] **Step 1:** Buat asmdef dan `UnityHttp`, `TES` penuh. Expected: kompilasi bersih di Editor, tes lulus.
- [ ] **Step 2:** Build pengunjung lewat menu/`executeMethod` yang ada (`Eutopos.EditorTools.BuildAndroid.Build`, lihat
  `docs/plans/2026-10-15-mvp-navigasi.md` baris 1502). Ekstrak `assets/bin/Data/Managed/Metadata/global-metadata.dat`
  dari APK dan cari string `Eutopos.Capture`. Expected: tidak ada. Catat ukuran APK pengunjung sebelum dan sesudah
  seluruh rencana.
- [ ] **Step 3:** `-executeMethod Eutopos.Capture.EditorTools.BuildCapture.Build` (sesuaikan namespace). Expected: APK
  terbentuk, `global-metadata.dat` memuat `Eutopos.Capture`, application id dan product name proyek kembali seperti
  semula (`git diff ProjectSettings/` kosong, abaikan perubahan akhir baris saja dengan `--ignore-cr-at-eol`).
- [ ] **Step 4:** Catat berkas dan hasil.

### Task 7: Layar rekam

**Files:**
- Create: `Assets/Eutopos/Capture/CaptureUi.cs`, `Assets/Eutopos/Capture/FrustumLayer.cs`,
  `Assets/Eutopos/Capture/PointLayer.cs`
- Modify: `Assets/Eutopos/Capture/CaptureController.cs`

**Interfaces:**
- Consumes: `CapturePolicy`, `PoseContinuity` (Task 1), `CaptureSession`, `FrameRecord` (Tasks 2 dan 3), `FrameCapture`
  yang ada (`Request(Action<CapturedFrame, string>)`), `Conv` yang ada.
- Alur layar: `Pengaturan` (bila token kosong) -> `Beranda` (tombol "Rekam peta baru" dan daftar sesi belum selesai
  dari `CaptureSession.Unfinished`) -> `Nama peta` -> `Rekam`.
- Layar `Nama peta` (Task 5b): pilih gedung dari `ListBuildings()` (wajib), lalu pilih salah satu: "Area baru" (input
  nama, pratinjau `area_id`; ditolak bila `area_id` itu sudah ada di gedung mana pun, dengan pesan "Area sudah ada,
  pilih dari daftar untuk rekam ulang") atau "Rekam ulang area" (daftar `AreaIds` gedung terpilih). Gagal memuat gedung
  memakai pesan `FailureKind` yang sama dengan Task 8 dan tombol "Coba lagi".
- Layar `Rekam` (sketsa di spesifikasi bagian 7):
  - Atas: jumlah foto, jarak tempuh (jumlah jarak antar pose foto berurutan), durasi, ruang kosong; saklar
    "Otomatis" (bawaan menyala).
  - Bawah: "Batal foto", tombol rana bulat, "Selesai" (aktif bila minimal 1 foto peta).
  - Spanduk peringatan saat `PoseContinuity.Event` tidak null atau tidak Tracking: "Pelacakan putus. Kembali ke area
    bingkai terakhir." Pengambilan otomatis berhenti selama spanduk tampil dan lanjut setelah Tracking stabil 1 detik.
  - Penolakan rana manual menampilkan `CapturePolicy.Reason` selama 2 detik.
  - Satu foto dalam proses pada satu waktu (rana ditolak selama `FrameCapture` belum menjawab).
- `FrustumLayer`: satu `Mesh` dengan `MeshTopology.Lines` di ruang dunia AR, dibangun ulang saat foto ditambah atau
  dibatalkan. Piramida kedalaman 0,15 m dengan rasio sisi dari intrinsik foto; segmen 0 hijau, segmen lain oranye, foto
  terakhir kuning. Bukan `ARAnchor` (keputusan spesifikasi bagian 10).
- `PointLayer` (opsional): titik `ARPointCloudManager` dikumpulkan per identifier, batas 20.000, saklar "Titik" (bawaan
  mati). Boleh dilewati bila menurunkan frame rate di bawah 30 fps di ponsel; tulis keputusannya di laporan.
- Setelah sesi dibuka ulang (`Open`), bingkai dibangun dari `Records("peta")`, dan `RecoveryNote` ditampilkan bila ada.

- [ ] **Step 1:** Implementasi layar dan lapisan. Logika baru yang bukan tampilan (misalnya jarak tempuh dari daftar
  pose) masuk Core dengan tes EditMode kecil di `CapturePolicyTests`.
- [ ] **Step 2:** `TES` penuh. Expected: lulus.
- [ ] **Step 3:** Build capture (Task 6 Step 3). Expected: berhasil.
- [ ] **Step 4:** Catat berkas.

### Task 8: Foto uji, paket, unggah, dan status

**Files:**
- Modify: `Assets/Eutopos/Capture/CaptureController.cs`, `Assets/Eutopos/Capture/CaptureUi.cs`

**Interfaces:**
- Consumes: `CapturePolicy.TestShot` (Task 1), `FramePackage.Write` (Task 4), `CaptureApi`, `TusUploader`,
  `UploadState`, `JobStatusView` (Task 5), `UnityHttp` (Task 6).
- Layar `Foto uji`: rana manual saja, `TestShot` dengan posisi foto uji sebelumnya, hitungan "n dari minimal 5",
  bingkai foto uji berwarna biru, "Batal foto", tombol "Lanjut unggah" aktif pada 5 atau lebih.
- Layar `Unggah`:
  1. `SetStage(Packaged)`; tulis `peta.zip` dan `uji.zip` (`meta.json` dari `CaptureMeta`, `configs.json` dari konfigurasi
     kamera terpilih seperti `SpikeRecorder`, atau `"{}"`).
  2. Bila `JobId` kosong: `CreateJob` dengan `area_id`, nama sesi, dan `BuildingId` sesi (null untuk rekam ulang
     area yang sudah ada), simpan `JobId`, `SetStage(Uploading)`.
  3. Unggah `peta.zip` lalu `uji.zip` dengan `TusUploader`, persentase gabungan menurut byte.
  4. Selesai -> `SetStage(Submitted)` dan pindah ke layar `Status`.
  - `JobClosed`: pesan "Pekerjaan ini sudah tidak menerima unggahan" dan tombol "Buat pekerjaan baru" (hapus `JobId`
    dan berkas `upload-*.json`, lalu ulang dari langkah 2 dengan paket yang sama).
  - `TokenRejected`: "Token aplikasi ditolak. Periksa atau ganti token di Pengaturan." `AccessRedirect`: "Akses
    Cloudflare menolak permintaan." Keduanya tanpa coba ulang otomatis, dengan tombol "Coba lagi".
  - `Retryable` habis: "Koneksi bermasalah" dan tombol "Coba lagi" yang melanjutkan dari offset.
  - Layar tetap menyala selama unggah (`Screen.sleepTimeout = SleepTimeout.NeverSleep`, dikembalikan setelahnya).
- Layar `Status`: `GetJob` tiap 5 detik, tampilkan `JobStatusView.Map`; berhenti poll pada `ReadyToTest`, `Held`,
  `Failed`, `Deleting`. `Held` dan `Failed` punya tombol "Rekam ulang" (sesi baru dengan nama sama).
- Sesi belum selesai dari Beranda melanjutkan sesuai `Stage`: `Recording` -> layar Rekam; `Testing` -> Foto uji;
  `Packaged` atau `Uploading` -> Unggah (melanjutkan dari `upload-*.json`); `Submitted` -> Status.

- [ ] **Step 1:** Implementasi.
- [ ] **Step 2:** `TES` penuh. Expected: lulus.
- [ ] **Step 3:** Build capture. Expected: berhasil, `Builds/eutopos-capture.apk` ada; catat ukurannya.
- [ ] **Step 4:** Catat berkas.

### Task 9: Uji di ponsel (Bagus, dengan Claude)

Bukan untuk agen pelaksana. Kriteria spesifikasi bagian 7 dan irisan 1 bagian 9:

- [ ] Merekam area L: bingkai tampil, pelacakan tidak memburuk, batal foto bekerja, jumlah foto dan ukuran paket dicatat.
- [ ] Tutup paksa di tengah merekam: foto yang sudah diambil masih ada saat sesi dibuka lagi.
- [ ] Matikan jaringan di tengah unggah lalu nyalakan: lanjut dari offset, bukan dari nol.
- [ ] Pekerjaan sampai `done`, versi tetap `candidate`, layar status "Siap diuji" cocok dengan dashboard.
- [ ] APK pengunjung tidak memuat `Eutopos.Capture` (hasil Task 6 Step 2).
