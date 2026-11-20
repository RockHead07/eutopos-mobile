# Prompt agen: Spike capture ARCore, Bagian A (kode)

Kerjakan hanya Bagian A dari rencana `D:/Dev/Projects/eutopos-mobile/docs/plans/2026-11-19-spike-capture-arcore.md`
(belum ter-commit; baca dari path absolut itu, hanya baca). Bagian B (lapangan) dan C (analisis di PC lab) bukan
tugasmu.

## C. Context
`eutopos-mobile` (Unity 6000.3, AR Foundation dan ARCore 6.3.4) adalah aplikasi navigasi indoor. Pemilik repo
memutuskan aplikasi capture terpisah yang merekam foto kunci berpose ARCore. Sebelum aplikasi itu dibangun, spike ini
mengukur: resolusi gambar ARCore, kualitas peta dari foto kunci vs video, dan kecocokan pose ARCore dengan SfM.
`Assets/Eutopos/Scripts/App/FrameCapture.cs` sudah mengambil gambar CPU ARCore tegak plus intrinsik dan memilih
konfigurasi kamera terbesar dengan sisi terpanjang maksimal 1920. `CapturedFrame` belum menyimpan rotasi penuh.
Build APK lewat `Assets/Eutopos/Editor/BuildAndroid.cs`.

## O. Objective
1. **APK spike perekam** (eutopos-mobile, worktree `D:/wt/spike-capture-mobile` dari `origin/main`):
   - Scene `Assets/Eutopos/Spike/SpikeRecorder.unity`, skrip di `Assets/Eutopos/Spike/` dengan assembly definition sendiri.
   - Saat mulai, tulis semua `XRCameraConfiguration` (lebar, tinggi, fps) dan yang terpilih ke `configs.json`.
   - Foto kunci memakai `FrameCapture`: hanya saat `TrackingState.Tracking`; ambil bila geser minimal 0,4 m atau putar
     minimal 10 derajat dari foto terakhir; lewati bila kecepatan kamera di atas 0,7 m/s. Hitung frame yang dilewati
     per alasan.
   - Simpan ke `Application.persistentDataPath/spike/<yyyyMMdd-HHmmss>/`: `images/000001.jpg ...` (JPEG 90, nama
     berurutan nol di depan); `frames.jsonl` (per foto: `file`, `t_ns`, `position` [3], `rotation_xyzw` [4],
     `tracking`, `K` {`fx`,`fy`,`cx`,`cy`,`width`,`height`}); `meta.json` (model ponsel, versi aplikasi, konfigurasi
     kamera terpilih, jumlah kehilangan pelacakan, frame dilewati per alasan, konvensi `unity_world`, Y atas, kidal).
   - UI uGUI minimal: tombol Mulai dan Berhenti, jumlah foto, status pelacakan, path folder sesi.
   - Menu `Eutopos/Build Spike APK` menghasilkan `Builds/eutopos-spike.apk` dengan package ID `<id sekarang>.spike`
     dan hanya scene spike; package ID semula dikembalikan di blok `finally`.
2. **Skrip analisis pose** (eutopos-vps, worktree `D:/wt/spike-capture-vps` dari `origin/main`):
   `spike/compare_arcore_poses.py`, tanpa dependensi baru (numpy dan pycolmap sudah ada lewat hloc).
   - Masukan: folder sesi spike (`frames.jsonl`) dan folder model SfM (`sfm/`). Cocokkan per nama berkas, ambil pusat
     kamera SfM foto yang terdaftar.
   - Ubah posisi Unity (kidal, Y atas) ke kerangka tangan kanan dengan membalik satu sumbu sebelum estimasi (Umeyama
     tidak menangani pantulan). Tulis eksplisit dan uji.
   - Estimasi Sim3 (Umeyama) SfM ke ARCore: skala, RMSE, residu median dan maksimum dalam meter. Drift: skala
     terpisah untuk paruh pertama dan kedua lintasan, selisih dalam persen.
   - Keluaran JSON dan ringkasan teks. `--self-test` dengan data sintetis (rotasi, skala, translasi, pantulan diketahui)
     harus kembali dengan galat mendekati nol.

## S. Style
Throwaway tapi jujur: sederhana, tanpa abstraksi. Uji EditMode hanya untuk logika murni (aturan foto kunci, format
baris `frames.jsonl`). Baca dan ikuti `CLAUDE.md` di kedua repo.

## T. Tone
Lugas. Pisahkan "diuji unit" dari "dibuktikan di ponsel". Bahasa Indonesia, istilah teknis Inggris, tanpa em dash.

## A. Audience
Bagus yang membawa APK ke lorong, dan Claude yang memverifikasi kode serta menganalisis hasil.

## R. Response
Tulis `LAPORAN-SPIKE-A.md` di root tiap worktree (jangan di-commit): berkas yang dibuat, tes dan output-nya, hasil
build APK (path dan ukuran), output `--self-test`, SHA-256 berkas yang berubah, dan daftar yang tidak terbukti
(semua perilaku di ponsel termasuk di sini).

## Batas
- Jangan commit, push, PR, atau merge. Kerja hanya di dua worktree di atas
  (`git fetch && git worktree add -b spike/capture-arcore <path> origin/main` di masing-masing repo). Jangan berpindah
  cabang atau mengubah berkas di checkout utama.
- Jangan mengubah scene `Main`, `AppController`, alur navigasi, `VpsClient`, atau konfigurasi build navigasi. Perubahan
  di luar folder `Spike/` hanya boleh: field tambahan di `CapturedFrame` bila tidak mengubah perilaku navigasi, dan
  menu build spike.
- Jangan menyentuh PC lab, production, Cloudflare, atau rahasia. Jangan menambah paket Unity atau dependensi Python.
- Jangan memasang APK ke ponsel.
- Bila Unity Editor atau build Android tidak bisa dijalankan di lingkunganmu, tulis itu di laporan sebagai "TIDAK
  dibuild" dan jangan mengklaim APK ada.
