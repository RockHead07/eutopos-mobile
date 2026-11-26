# Brief serah terima: aplikasi capture irisan A, tiket 1 (logika Core, Tugas 1 sampai 5)

Ditulis 2026-11-23 untuk agen pelaksana. Kerangka CO-STAR. Diperiksa terhadap `origin/main` eutopos-mobile `31afaec`
ditambah commit dokumen `061b18c` di cabang `docs/aplikasi-capture-matang`.

---

## C. Context (konteks)

eutopos-mobile adalah proyek Unity 6000.3.14f1 (AR Foundation/ARCore, Android) untuk navigasi indoor. Repo ini akan
menghasilkan dua APK: aplikasi navigasi pengunjung (sudah ada) dan aplikasi **Eutopos Capture** untuk merekam peta (belum
ada). Server pasangannya (eutopos-vps) sudah siap di production: token aplikasi lewat `Authorization: Bearer`, paket foto
ZIP lewat tus, pekerjaan dari aplikasi berhenti sebagai kandidat.

Tiket ini hanya membangun **logika murni** aplikasi capture di `Eutopos.Core` (tanpa UnityEngine, dites EditMode): aturan
foto, sesi di disk yang bisa pulih setelah ditutup paksa, paket ZIP, klien tus dan API pekerjaan lewat antarmuka HTTP,
dan pemetaan status. UI dan AR adalah tiket 2, jangan dikerjakan di sini.

Dokumen yang mengikat (semua di worktree `D:/wt/capture-design`):
- **Rencana (ikuti Tugas 1 sampai 5 persis):** `docs/plans/2026-11-23-aplikasi-capture-irisan-a.md`
- **Spesifikasi:** `docs/specs/2026-11-23-aplikasi-capture-matang-design.md` (bagian 7) dan
  `docs/specs/2026-11-05-mode-rekam-peta-irisan-1-design.md` (bagian 4 sampai 6).
- Rujukan hanya baca: `D:/wt/spike-depth-mobile/Assets/Eutopos/Spike/PoseContinuity.cs` (sumber salinan Tugas 1),
  `Assets/Eutopos/Spike/Keyframes.cs` dan `Assets/Eutopos/Spike/Tests/KeyframeTests.cs` (aturan dan kasus tes yang
  disalin), `D:/Dev/Projects/eutopos-vps/server/frame_package.py` dan `server/upload_client.py` (perilaku server).

Rencana menang atas brief ini bila ada beda rincian; spesifikasi menang atas rencana.

## O. Objective (tujuan)

Kerjakan **Tugas 1, 2, 3, 4, dan 5** di rencana. Hasil akhir:
- `Assets/Eutopos/Scripts/Core/Capture/` berisi `CapturePolicy.cs`, `PoseContinuity.cs`, `CaptureRecords.cs`,
  `CaptureSession.cs`, `FramePackage.cs`, `CaptureHttp.cs`, `JobStatusView.cs` dengan nama dan tanda tangan persis
  seperti blok Interfaces di rencana (tiket 2 bergantung padanya).
- Tes EditMode `CapturePolicyTests`, `CaptureRecordsTests`, `CaptureSessionTests`, `FramePackageTests`,
  `CaptureHttpTests`, `JobStatusViewTests` ada dan lulus, termasuk semua tes Review Focus 1 sampai 5 rencana.
- Paket hasil `FramePackage.Write` lolos `server.frame_package.extract` asli (Tugas 4 Step 5).

## S. Style (gaya kerja)

- TDD: tulis tes dulu, jalankan, lihat gagal, baru implementasi. Nama tes dan assert ikuti rencana.
- `Eutopos.Core` tetap `noEngineReferences: true`: `System.Numerics`, `System.IO`, `System.IO.Compression`,
  `Newtonsoft.Json` saja. Bila `System.IO.Compression` tidak bisa dirujuk dari asmdef Core, berhenti dan tulis
  alasannya di laporan sebelum mencari jalan lain.
- Ikuti gaya berkas Core yang ada (misalnya `VpsResponses.cs`, `LogFiles.cs`) dan tes yang ada di
  `Assets/Eutopos/Tests/EditMode/`.
- Sebelum mulai: jalankan tes EditMode penuh untuk garis dasar (jumlah lulus dan gagal) dan catat SHA-256 tiap berkas
  yang sudah ada yang akan kamu ubah. Setelah selesai, SHA-256 lagi.
- Perintah tes ada di Global Constraints rencana (`TES`). Jalankan pertama memakan waktu impor karena worktree belum
  punya `Library`.

## T. Tone (nada)

Lugas dan jujur. Pisahkan "diuji otomatis" dari "tidak dibuktikan". Laporan bahasa Indonesia, istilah teknis bahasa
Inggris, tanpa em dash.

## A. Audience (pembaca)

Bagus yang memutuskan commit, lalu Claude yang memverifikasi ulang setiap klaim secara mandiri (menjalankan tes
sendiri, membaca diff, memvalidasi paket ke server) sebelum tiket 2.

## R. Response (format keluaran)

Tulis `D:/wt/capture-design/LAPORAN-CAPTURE-TIKET-1.md` (jangan di-commit) berisi:
1. Garis dasar tes dan SHA-256 sebelum.
2. Per tugas: berkas yang dibuat atau diubah, tes yang gagal lalu lulus (kutipan singkat hasil XML atau log),
   penyimpangan dari rencana beserta alasannya.
3. Hasil tes EditMode penuh di akhir (jumlah lulus, gagal, dilewati).
4. Perintah dan keluaran validasi silang `frame_package.extract` (Tugas 4 Step 5).
5. SHA-256 berkas yang berubah sesudahnya.
6. Draf pesan commit (bahasa Indonesia, gaya `feat(capture): ...`, **tanpa atribusi AI apa pun**).
7. Daftar yang tidak terbukti atau ragu.

## Batas

- **Jangan commit, push, PR, atau merge.** Bekerja di worktree yang sudah ada `D:/wt/capture-design` (cabang
  `docs/aplikasi-capture-matang`). Jangan berpindah cabang, jangan membuat worktree lain, jangan mengubah checkout utama
  `D:/Dev/Projects/eutopos-mobile`, `D:/wt/spike-depth-mobile`, atau repo eutopos-vps.
- Jangan membuka proyek ini di Unity Editor bersamaan dengan menjalankan tes batchmode.
- Berkas yang boleh dibuat atau diubah hanya:
  - `Assets/Eutopos/Scripts/Core/Capture/*.cs` (dan `.meta`-nya),
  - `Assets/Eutopos/Tests/EditMode/Capture*Tests.cs`, `FramePackageTests.cs`, `JobStatusViewTests.cs` (dan `.meta`),
  - `Assets/Eutopos/Tests/EditMode/Eutopos.Core.Tests.asmdef` dan `Assets/Eutopos/Scripts/Core/Eutopos.Core.asmdef`
    hanya bila rujukan memang perlu ditambah (sebutkan di laporan).
  Perlu berkas lain? Berhenti dan tulis alasannya.
- **Jangan** mengerjakan Tugas 6 sampai 9: tanpa assembly `Eutopos.Capture`, tanpa UI, tanpa build APK.
- Jangan menyentuh `Assets/Eutopos/Spike/`, `Assets/Eutopos/Scripts/App/`, scene, `ProjectSettings/`, atau `Packages/`.
  Bila Unity menulis ulang `ProjectSettings/*.asset` hanya karena akhir baris, pulihkan dengan `git checkout` dan
  sebutkan di laporan.
- Jangan membaca, menyalin, atau mencetak `Assets/Eutopos/Resources/vps-token.local.json` atau token apa pun.
- Jangan melonggarkan, menghapus, atau melewati tes lama.
- Jangan menghapus `uji-akses-capture.ps1`, rencana, spesifikasi, atau brief ini.

## Kriteria selesai

- Tugas 1 sampai 5 selesai sesuai rencana, semua tes yang disebut rencana ada dan lulus.
- Tes EditMode penuh tidak lebih buruk dari garis dasar.
- Validasi silang paket ke `frame_package.extract` lulus.
- Laporan lengkap sesuai bagian R.
