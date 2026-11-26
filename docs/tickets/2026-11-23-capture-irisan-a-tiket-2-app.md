# Brief serah terima: aplikasi capture irisan A, tiket 2 (revisi Core dan aplikasi Unity, Tugas 5b sampai 8)

Ditulis 2026-11-23 untuk agen pelaksana. Kerangka CO-STAR. Melanjutkan tiket 1 di worktree yang sama.

---

## C. Context (konteks)

Tiket 1 sudah selesai dan diverifikasi Claude: logika capture di `Assets/Eutopos/Scripts/Core/Capture/` (aturan foto,
sesi di disk, paket ZIP, klien tus, API pekerjaan, status), 164 tes EditMode lulus, paket lolos validator server.
Laporannya `D:/wt/capture-design/LAPORAN-CAPTURE-TIKET-1.md`. Semua itu belum di-commit dan menjadi dasar tiket ini.

Dua temuan setelah tiket 1 sudah dimasukkan ke rencana sebagai **Tugas 5b**:
1. Server menolak area baru tanpa `building_id`. Keputusan Bagus: perekam memilih gedung di aplikasi lewat
   `GET /api/buildings`. Server untuk itu dikerjakan di tiket terpisah (eutopos-vps, `D:/wt/capture-buildings`); di sini
   cukup klien dan tesnya dengan HTTP palsu.
2. Penulis ZIP Stored buatan sendiri (`StoredZip`) diganti `System.IO.Compression.ZipArchive`; server menerima DEFLATE.

Dokumen yang mengikat (worktree `D:/wt/capture-design`):
- **Rencana:** `docs/plans/2026-11-23-aplikasi-capture-irisan-a.md`, **Tugas 5b, 6, 7, dan 8**.
- **Spesifikasi:** `docs/specs/2026-11-23-aplikasi-capture-matang-design.md` bagian 7 (sketsa layar rekam) dan
  `docs/specs/2026-11-05-mode-rekam-peta-irisan-1-design.md` bagian 3 dan 8.
- Rujukan hanya baca: `Assets/Eutopos/Spike/SpikeRecorder.cs` dan `Assets/Eutopos/Spike/Editor/BuildSpike.cs` (pola
  perekam, UI lewat kode, pembuatan scene, build dengan pengaturan dipulihkan), `Assets/Eutopos/Scripts/App/FrameCapture.cs`,
  `VpsConfig.cs`, `Conv.cs`.

Rencana menang atas brief ini bila ada beda rincian; spesifikasi menang atas rencana.

## O. Objective (tujuan)

Kerjakan **Tugas 5b, 6, 7, dan 8** di rencana. Hasil akhir:
- Revisi Core Tugas 5b dengan tesnya lulus, termasuk validasi silang ulang paket ke server.
- Assembly `Eutopos.Capture` yang terbukti **tidak ada** di APK pengunjung dan **ada** di APK capture.
- `Builds/eutopos-capture.apk` (id aplikasi asli + `.capture`, nama "Eutopos Capture") dengan alur Pengaturan token,
  Beranda, Nama peta (pilih gedung, area baru atau rekam ulang), Rekam, Foto uji, Unggah, Status, dan melanjutkan sesi
  yang belum selesai.
- Pengaturan proyek kembali seperti semula setelah build.

## S. Style (gaya kerja)

- TDD untuk semua logika yang bukan tampilan; logika baru semacam itu masuk Core dengan tes EditMode. Kode tampilan dan
  AR diverifikasi lewat kompilasi, build, dan pemeriksaan APK.
- UI dibangun lewat kode (uGUI) seperti `SpikeRecorder`; teks bahasa Indonesia. Tanpa paket baru.
- Garis dasar sebelum mulai: tes EditMode penuh dan ukuran APK pengunjung (build `Eutopos.EditorTools.BuildAndroid.Build`).
  SHA-256 berkas yang sudah ada yang akan diubah, sebelum dan sesudah.
- Untuk define constraint `UNITY_EDITOR || EUTOPOS_CAPTURE`: bila Unity menolaknya, berhenti dan tulis usulan pengganti
  di laporan sebelum mengubah pendekatan.
- Perintah tes dan build ada di Global Constraints rencana dan di Tugas 6.

## T. Tone (nada)

Lugas dan jujur. Pisahkan "diuji otomatis", "dibuktikan lewat build atau APK", dan "belum dibuktikan (perlu ponsel)".
Laporan bahasa Indonesia, istilah teknis Inggris, tanpa em dash.

## A. Audience (pembaca)

Bagus yang memutuskan commit dan menguji di ponsel (Tugas 9), lalu Claude yang memverifikasi ulang setiap klaim secara
mandiri sebelum APK dipasang.

## R. Response (format keluaran)

Tulis `D:/wt/capture-design/LAPORAN-CAPTURE-TIKET-2.md` (jangan di-commit) berisi:
1. Garis dasar tes, ukuran APK pengunjung, dan SHA-256 sebelum.
2. Per tugas: berkas yang dibuat atau diubah, tes gagal lalu lulus, penyimpangan dari rencana beserta alasannya.
3. Hasil tes EditMode penuh di akhir; validasi silang `frame_package.extract` (Tugas 5b Step 4).
4. Bukti pemisahan: perintah dan hasil pencarian `Eutopos.Capture` di `global-metadata.dat` kedua APK; ukuran APK
   pengunjung sebelum dan sesudah; ukuran APK capture.
5. Bukti `git diff --ignore-cr-at-eol ProjectSettings/` kosong setelah build.
6. Keputusan `PointLayer` (dibuat atau dilewati, dan alasannya).
7. Tangkapan layar Editor (Play mode tanpa AR boleh) untuk layar Beranda, Nama peta, dan Rekam, bila bisa diambil.
8. SHA-256 sesudah, draf pesan commit (bahasa Indonesia, gaya `feat(capture): ...`, **tanpa atribusi AI apa pun**), dan
   daftar yang belum dibuktikan.

## Batas

- **Jangan commit, push, PR, atau merge.** Tetap di worktree `D:/wt/capture-design` (cabang
  `docs/aplikasi-capture-matang`). Jangan berpindah cabang atau menyentuh checkout lain, termasuk repo eutopos-vps.
- **Jangan memasang APK ke ponsel** dan jangan menjalankan `adb`. Uji ponsel adalah Tugas 9 bersama Bagus.
- Jangan membaca, menyalin, atau mencetak `Assets/Eutopos/Resources/vps-token.local.json`, token aplikasi, atau rahasia
  lain. Build boleh memuat berkas itu sebagaimana build yang ada.
- Berkas yang boleh dibuat atau diubah:
  - `Assets/Eutopos/Scripts/Core/Capture/*` dan tes `Assets/Eutopos/Tests/EditMode/Capture*Tests.cs`,
    `FramePackageTests.cs`, `JobStatusViewTests.cs` (beserta `.meta`);
  - semua di bawah `Assets/Eutopos/Capture/` (baru, beserta `.meta`, termasuk `Capture.unity`);
  - `ProjectSettings/EditorBuildSettings.asset` **tidak** boleh berubah (build capture memakai daftar scene sendiri).
  Perlu berkas lain? Berhenti dan tulis alasannya.
- Jangan menyentuh `Assets/Eutopos/Spike/`, `Assets/Eutopos/Scripts/App/`, `Main.unity`, `BuildAndroid.cs`,
  `Packages/`. Perubahan `ProjectSettings/*.asset` yang hanya akhir baris dipulihkan dengan `git checkout`.
- Jangan melonggarkan atau menghapus tes lama, kecuali assert `CompressedLength == Length` yang memang diganti Tugas 5b.
- Jangan menghapus `uji-akses-capture.ps1`, laporan tiket 1, rencana, spesifikasi, atau brief ini.

## Kriteria selesai

- Tugas 5b sampai 8 selesai sesuai rencana; tes EditMode penuh lulus dan tidak lebih buruk dari garis dasar.
- APK capture terbentuk; APK pengunjung tidak memuat `Eutopos.Capture`; `ProjectSettings` bersih.
- Laporan lengkap sesuai bagian R.
