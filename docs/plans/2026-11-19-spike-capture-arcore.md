# Spike: capture foto kunci berpose ARCore (Fase B)

Status: rencana, belum dikerjakan. Ditulis 2026-11-19. Spike ini **throwaway**: kodenya hanya untuk menjawab
pertanyaan di bawah, bukan dasar aplikasi capture. Aplikasi capture yang sebenarnya dirancang ulang dari hasilnya
(`docs/specs/2026-11-05-mode-rekam-peta-irisan-1-design.md`).

## 1. Pertanyaan yang harus dijawab

| # | Pertanyaan | Kenapa penting |
|---|---|---|
| Q1 | Resolusi gambar CPU ARCore berapa saja yang tersedia di ponsel Bagus, dan mana yang terpilih? | Bila hanya 640x480, kualitas peta bisa jauh di bawah video 1080p |
| Q2 | Peta dari foto kunci (geser 0,4 m atau putar 10 derajat) sebagus peta dari video 2 fps di lorong yang sama? | Menentukan apakah capture berpose layak jadi jalur utama |
| Q3 | Seberapa cocok posisi ARCore dengan posisi kamera hasil SfM (skala, residu, drift sepanjang lorong)? | Menentukan apakah skala meter bisa diambil dari pose tanpa meteran (irisan 2) |
| Q4 | Berapa foto, ukuran data, dan berapa kali pelacakan hilang per rekaman? | Batas penyimpanan dan unggah aplikasi capture |

## 2. Bagian A: kode spike (bisa diserahkan ke agen)

### C. Context

`eutopos-mobile` adalah aplikasi Unity 6000.3 dengan AR Foundation dan ARCore 6.3.4 untuk navigasi. Kelas
`Assets/Eutopos/Scripts/App/FrameCapture.cs` sudah mengambil gambar CPU ARCore, memutarnya tegak, menyimpan intrinsik
yang sudah diputar dan diskalakan, dan memilih konfigurasi kamera terbesar dengan sisi terpanjang maksimal 1920.
`CapturedFrame` hanya menyimpan posisi dan arah depan kamera, belum rotasi penuh. Build APK lewat
`Assets/Eutopos/Editor/BuildAndroid.cs` (menu Eutopos/Build APK, satu scene `ProjectSetup.ScenePath`).

Repo `eutopos-vps` membangun peta dari folder frame lewat `python -m server.manage job <area> <nama> --map <folder>`
(pasangan berurutan memakai urutan nama berkas).

### O. Objective

Buat dua alat kecil:

1. **APK spike perekam** di `eutopos-mobile`:
   - Scene terpisah `Assets/Eutopos/Spike/SpikeRecorder.unity` dan skrip di `Assets/Eutopos/Spike/` dengan
     assembly definition sendiri. Tidak mengubah scene `Main`, `AppController`, atau perilaku aplikasi navigasi.
   - Saat mulai: tulis daftar semua `XRCameraConfiguration` yang tersedia (lebar, tinggi, fps) dan konfigurasi yang
     terpilih ke `configs.json`.
   - Rekam foto kunci memakai `FrameCapture` yang ada. Aturan ambil: hanya saat `TrackingState.Tracking`; geser
     minimal 0,4 m atau putar minimal 10 derajat dari foto terakhir; lewati bila kecepatan kamera di atas 0,7 m/s.
     Simpan juga jumlah frame yang dilewati per alasan.
   - Tambah rotasi penuh kamera (kuaternion `xyzw`, kerangka dunia Unity) dan status pelacakan ke data per foto.
     Boleh menambah field di `CapturedFrame` bila tidak mengubah perilaku navigasi; bila ragu, salin ke struktur spike.
   - Tulis ke `Application.persistentDataPath/spike/<yyyyMMdd-HHmmss>/`: `images/000001.jpg ...` (JPEG kualitas 90,
     nama berurutan nol di depan), `frames.jsonl` (satu baris per foto: `file`, `t_ns`, `position` [3],
     `rotation_xyzw` [4], `tracking`, `K` {`fx`,`fy`,`cx`,`cy`,`width`,`height`}), dan `meta.json` (model ponsel,
     versi aplikasi, konfigurasi kamera terpilih, jumlah kehilangan pelacakan, jumlah frame dilewati per alasan,
     konvensi koordinat: `unity_world`, Y ke atas, kidal).
   - UI minimal uGUI: tombol Mulai dan Berhenti, jumlah foto, status pelacakan, dan path folder sesi.
   - Menu baru `Eutopos/Build Spike APK` menghasilkan `Builds/eutopos-spike.apk` dengan **package ID berbeda**
     (`<id sekarang>.spike`) dan hanya scene spike, lalu mengembalikan package ID semula di blok `finally`. APK
     spike tidak boleh menimpa aplikasi navigasi di ponsel.
2. **Skrip analisis pose** di `eutopos-vps/spike/compare_arcore_poses.py` (tanpa dependensi baru; numpy dan pycolmap
   sudah ada lewat hloc):
   - Masukan: folder sesi spike (`frames.jsonl`) dan folder model SfM (`sfm/`) hasil job.
   - Cocokkan foto per nama berkas, ambil pusat kamera SfM foto yang terdaftar.
   - Ubah posisi ARCore dari Unity (kidal, Y atas) ke kerangka tangan kanan dengan membalik satu sumbu sebelum
     estimasi, karena Umeyama tidak menangani pantulan. Tulis konversinya eksplisit dan uji.
   - Estimasi Sim3 (Umeyama) dari SfM ke ARCore: skala, RMSE residu dalam meter, residu median dan maksimum.
   - Drift: estimasi skala terpisah untuk paruh pertama dan kedua lintasan; laporkan selisihnya dalam persen.
   - Keluaran JSON dan ringkasan teks. Mode `--self-test` dengan data sintetis (rotasi, skala, translasi, dan
     pantulan yang diketahui harus kembali dengan galat mendekati nol).

### S. Style

Throwaway tapi jujur: kode sederhana, tanpa abstraksi. Uji EditMode hanya untuk logika murni (aturan foto kunci,
format baris `frames.jsonl`). Skrip analisis wajib punya `--self-test`. Ikuti `CLAUDE.md` kedua repo.

### T. Tone

Laporan lugas. Pisahkan "diuji unit" dari "dibuktikan di ponsel". Bahasa Indonesia, istilah teknis Inggris, tanpa
em dash.

### A. Audience

Bagus yang akan membawa APK ke lorong, dan Claude yang memverifikasi kode serta menganalisis hasilnya.

### R. Response

Laporan `LAPORAN-SPIKE-A.md` di root worktree masing-masing repo (tidak di-commit): berkas yang dibuat, tes dan
output-nya, hasil build APK (ukuran berkas), output `--self-test`, dan daftar yang tidak terbukti (semua perilaku di
ponsel termasuk di sini).

### Batas

- Jangan commit, push, PR, atau merge. Worktree baru di masing-masing repo:
  `D:/wt/spike-capture-mobile` dari `origin/main` eutopos-mobile dan `D:/wt/spike-capture-vps` dari `origin/main`
  eutopos-vps.
- Jangan mengubah scene `Main`, alur navigasi, `VpsClient`, atau konfigurasi build navigasi. Satu-satunya perubahan
  di luar folder `Spike/` yang boleh: field tambahan di `CapturedFrame` (bila perlu) dan menu build spike.
- Jangan menyentuh PC lab, production, Cloudflare, atau rahasia. Jangan menambah paket Unity atau dependensi Python.
- Jangan memasang APK ke ponsel; itu bagian Bagus.

## 3. Bagian B: lapangan (Bagus)

Satu sesi, hari dan jam yang sama untuk kedua rekaman, supaya cahaya dan keramaian setara.

1. Pilih lintasan lorong lantai 10 yang **memuat tikungan sekitar frame 150-165 rekaman `floor10-1080p`** (tikungan
   yang dulu memutus peta) dan satu lorong lurus minimal 20 m.
2. **Rekaman 1, APK spike:** pasang `eutopos-spike.apk`, catat model ponsel. Mulai, jalan pelan dengan ponsel
   mendatar setinggi dada, berhenti di titik akhir. Catat jumlah foto dan kehilangan pelacakan yang tampil.
3. **Rekaman 2, video:** lintasan yang sama, kamera biasa 1080p 30 fps, cara memegang sama.
4. Ambil folder sesi spike dari ponsel (`adb pull` atau kabel) dan video ke PC. Jangan diunggah ke repo mana pun:
   foto lorong bisa memuat wajah orang.
5. Opsional (untuk Q3 yang lebih kuat): ukur satu jarak lurus panjang di lintasan dengan meteran atau hitung ubin,
   catat titik awal dan akhirnya.

## 4. Bagian C: pembangunan dan analisis (Claude, atas izin Bagus untuk PC lab)

1. Bangun dua peta di PC lab dengan area uji terpisah, misalnya `spike-arcore` (`--map <folder images>`) dan
   `spike-video` (`--map <video>`), gedung PENS atau sesuai arahan Bagus. **Jangan diterbitkan.**
2. Bandingkan Q2: frame terdaftar, jumlah potongan, titik 3D, inlier di tikungan, waktu proses.
3. Jalankan `compare_arcore_poses.py` untuk Q3.
4. Laporkan Q1 sampai Q4 dengan angka mentah, lalu hapus area uji bila Bagus setuju.

## 5. Keputusan dari hasil

| Hasil | Keputusan |
|---|---|
| Q1: konfigurasi terbesar di bawah 1280 sisi terpanjang | Kaji ulang: rekam video kamera biasa bersama log pose, atau terima resolusi rendah bila Q2 tetap baik |
| Q2: peta foto kunci setara atau lebih baik dari video (frame terdaftar, satu potongan) | Lanjut spesifikasi irisan 1 dengan aturan foto kunci ini |
| Q2: lebih buruk | Setel ambang (jarak, sudut) dan ulangi, atau tambah frame di tikungan |
| Q3: residu dan selisih skala antar-paruh kecil (ambang ditetapkan dari angka spike) | Irisan 2: skala dari pose, tanpa meteran |
| Q3: drift skala besar | Skala dari pose ditambah satu jarak acuan (`known_distance`) |

Ambang pasti sengaja tidak ditetapkan di depan: belum ada pengukuran. Setelah angka keluar, ambang dicatat di
spesifikasi irisan 1 bersama alasannya.
