# Mode rekam peta, irisan pertama (desain)

Status: draf, arah dikunci Bagus 2026-11-19 (dua aplikasi, foto kunci berpose); rincian lain masih menunggu persetujuan. Tanggal: 2026-11-05, direvisi 2026-11-19. Belum dikerjakan apa pun.

Bagian "Keputusan Bagus" memuat hanya yang Bagus katakan. Bagian "Pilihan Claude" memuat tebakan yang harus diperiksa.

Konteks: desain induk ada di repo `eutopos-vps` (`docs/specs/2026-11-05-capture-publish-otomatis-multi-org-design.md`, belum di-commit) dan rencana server gerbang di `docs/plans/2026-11-05-gerbang-publish-otomatis.md`. Rancangan awal aplikasi capture ada di `eutopos-vps/docs/specs/2026-09-29-capture-and-map-pipeline-design.md` (foto kunci dengan pose dan intrinsik ARCore, bukan video).

Catatan keputusan (direvisi 2026-11-19): spesifikasi MVP navigasi di repo ini (2026-10-15, bagian 3) menyebut aplikasi capture sebagai build kedua yang terpisah. Pada 2026-11-05 Bagus memutuskan capture menjadi mode di dalam aplikasi navigasi. Pada 2026-11-19 Bagus **membalik keputusan itu dan menguncinya**: satu repo, **dua aplikasi** lewat Build Profile Unity (Eutopos untuk pengunjung, Eutopos Capture untuk developer/admin), kode capture terisolasi sehingga tidak ikut dikompilasi ke aplikasi pengunjung. Jalur publish utama adalah foto kunci berpose ARCore dari aplikasi capture, bukan sekadar video. Alasan dan rinciannya di bagian 8 dan 10.

## 1. Maksud

Seorang anggota organisasi merekam satu peta dari ponselnya tanpa membuka website: beri nama peta, rekam, ambil foto uji, unggah, tunggu hasil. Irisan pertama hanya membangun jalur tipis ini dari ujung ke ujung. Panduan merekam (peta kecil, tanda bagian kurang) dan penyelarasan skala dari pose adalah irisan berikutnya.

## 2. Tujuan, bukan tujuan, anti-tujuan

Tujuan:
1. Rekaman dari ponsel masuk antrean pembangunan peta yang sudah ada, tanpa langkah di website.
2. Perekam melihat kemajuan unggah, status pemrosesan, dan alasan bila ditahan atau ditolak.
3. Pose kamera dan intrinsik tiap foto tersimpan di paket unggahan untuk dipakai irisan berikutnya.
4. Foto uji ikut terkirim, supaya gerbang server bisa menerbitkan peta otomatis.

Bukan tujuan (irisan ini):
- Peta kecil, jejak jalur, penanda posisi, tanda bagian kurang (irisan 3 dan 4).
- Memakai pose untuk menyelaraskan skala atau menilai galat meter (irisan 2).
- Login per orang, organisasi, dan kunci akses (rencana tenant di server).
- iOS, penyamaran wajah otomatis, deteksi buram berbasis isi gambar, antrean unggah di latar belakang.

Anti-tujuan:
- Rekaman hilang karena aplikasi ditutup atau ponsel mati di tengah jalan.
- Unggahan yang mengulang dari nol setiap koneksi putus.
- Penyimpanan ponsel terisi tanpa peringatan.
- Aplikasi menampilkan "siap" padahal server belum menerbitkan.
- Semua foto uji diambil dari satu titik, sehingga gerbang menilai tanpa makna.

## 3. Alur perekam

1. **Buka aplikasi Eutopos Capture** (APK terpisah dari aplikasi navigasi pengunjung).
2. **Rekam peta, nama**: perekam mengetik nama peta. Aplikasi menurunkan `area_id` darinya (huruf kecil, angka, tanda hubung, awalan huruf atau angka, maksimal 40 karakter, sama dengan pola di `server/uploads.py`).
3. **Merekam**: foto kunci diambil otomatis saat berjalan (bagian 4). Layar menampilkan jumlah foto, status pelacakan ARCore, dan tombol Berhenti. Foto langsung ditulis ke penyimpanan ponsel.
4. **Foto uji**: setelah Berhenti, aplikasi meminta minimal 5 foto uji, diambil manual (ketuk untuk memotret) dari titik berbeda di sepanjang area. Layar menampilkan hitungan. Tombol lanjut aktif setelah minimal 5.
5. **Unggah**: dua paket (peta dan uji) dikirim berurutan dengan persentase. Bila koneksi putus, aplikasi melanjutkan dari offset yang diakui server.
6. **Status**: aplikasi menanyakan status pekerjaan tiap 5 detik. Tampilan: Mengunggah, Antre, Memproses (nama tahap), Memeriksa, Siap, Ditahan, atau Ditolak. Untuk Ditahan dan Ditolak, tampilkan alasan dari server dan satu tombol untuk merekam ulang.

## 4. Pengambilan foto kunci

Memakai `FrameCapture` yang sudah ada (gambar CPU ARCore tegak, intrinsik diputar dan diskalakan, JPEG kualitas 90, sisi terpanjang maksimal 1920). `CapturedFrame` saat ini hanya menyimpan posisi dan arah depan; irisan ini menambah rotasi penuh (kuaternion) dan penanda status pelacakan.

Aturan ambil foto (konstanta, bisa disetel setelah uji pertama):
- Hanya saat status pelacakan ARCore adalah Tracking.
- Ambil bila bergeser minimal 0,4 m atau berputar minimal 10 derajat dari foto terakhir (rentang rancangan awal: 0,3 sampai 0,5 m dan 10 derajat).
- Lewati bila kecepatan kamera di atas 0,7 m/s (pengganti kasar untuk buram; deteksi buram berbasis isi gambar bukan bagian irisan ini).
- Batas keras 1500 foto per sesi, dan berhenti dengan pesan bila ruang kosong ponsel kurang dari 1 GB.

Penamaan foto berurutan nol-di-depan (`000001.jpg`), karena pembangunan peta memakai pasangan berurutan (`--seq 10`).

## 5. Format paket unggahan

Dua berkas zip per sesi, satu paket per tus upload:
- `peta.zip` dan `uji.zip`, masing-masing berisi `images/000001.jpg ...`, `frames.jsonl`, dan `meta.json`.
- `frames.jsonl`, satu baris per foto: `file`, `t_ns`, `position` (3 angka), `rotation_xyzw` (4 angka), `tracking` (string), `K` (`fx`, `fy`, `cx`, `cy`, `width`, `height`).
- `meta.json`: versi aplikasi, model ponsel, resolusi, waktu mulai, jumlah kehilangan pelacakan, dan konvensi koordinat (`unity_world`, sumbu Y ke atas, kidal kiri).

Alasan memilih zip dan bukan satu-satu: model pekerjaan memuat paling banyak 4 berkas dengan batas 2 GiB masing-masing (`MAX_VIDEOS`, `MAX_VIDEO_BYTES` di `server/uploads.py`), dan satu unggahan resumable lebih sederhana dari ratusan.

Ukuran perkiraan satu sesi beberapa ratus foto: puluhan sampai seratusan MB. Angka ini perkiraan dan baru terukur pada uji pertama.

## 6. Klien unggah (tus) di Unity

Menulis klien tus minimal sendiri (tanpa dependensi baru), mengikuti `server/upload_client.py` sebagai rujukan perilaku:
- Membuat pekerjaan: `POST {base}/api/jobs` dengan `area_id`, `area_name`, dan daftar berkas (`name`, `role`, `size`, dan `kind: "frames"` setelah server menambahnya).
- Membuat unggahan: `POST` ke `/files/` dengan `Tus-Resumable: 1.0.0`, `Upload-Length`, dan `Upload-Metadata` (`job_id`, `name`, base64).
- Mengirim potongan 50 MiB lewat `PATCH` dengan `Upload-Offset` dan `Content-Type: application/offset+octet-stream` (batas 100 MB per permintaan Cloudflare Free).
- Melanjutkan: `HEAD` ke URL unggahan untuk membaca offset, lalu `PATCH` dari offset itu. Status unggahan (URL, offset, id pekerjaan, nama berkas) disimpan di berkas kecil di penyimpanan aplikasi, supaya bisa dilanjutkan setelah aplikasi dibuka ulang.
- Percobaan ulang dengan jeda bertambah. Header `CF-Access-Client-Id` dan `CF-Access-Client-Secret` dipasang seperti `VpsClient` sekarang, dengan `redirectLimit = 0`.

Hanya jalan saat aplikasi di latar depan (layar menyala). Unggahan latar belakang bukan bagian irisan ini.

## 7. Perubahan di server (rencana terpisah di eutopos-vps)

Irisan ini bergantung pada tiga hal yang belum ada di server:
1. **Menerima paket foto.** Entri berkas diberi `kind` (`video` atau `frames`). Pekerja mengekstrak zip ke `dataset/mapping` atau `dataset/query` dengan pemeriksaan di batas kepercayaan: tolak nama anggota yang mencoba keluar folder, batasi jumlah berkas dan total ukuran terekstrak, terima hanya `.jpg`, lalu melewati `extract_frames.py`. Zip yang rusak atau berbahaya harus menggagalkan pekerjaan dengan alasan yang jelas.
2. **Identitas untuk aplikasi.** Server sekarang menolak token layanan Cloudflare Access untuk `/api/*` dan hook tus karena tidak ada email di token (`server/auth.py`). Irisan ini butuh jalur sempit yang menerima token layanan untuk rute capture saja dan mencatat identitas sebagai `service:<common_name>`. Catatan: bahwa token layanan membawa klaim `common_name` berisi client id baru saya temukan di satu implementasi pihak ketiga, belum di dokumentasi Cloudflare. Harus dibuktikan sebelum dipakai.
3. **Gerbang dan publish otomatis** (`docs/plans/2026-11-05-gerbang-publish-otomatis.md`). Tanpa ini, pekerjaan berakhir sebagai `candidate` dan aplikasi menampilkan Ditahan sampai operator menerbitkan manual.

## 8. Struktur kode di eutopos-mobile

Logika murni di `Assets/Eutopos/Scripts/Core/` (dites EditMode, tanpa AR): kebijakan foto kunci, penulisan baris `frames.jsonl` dan `meta.json`, pemetaan status pekerjaan ke tampilan, penghitungan offset dan percobaan ulang tus, penurunan `area_id` dari nama. Bagian yang menyentuh AR dan UI di `Assets/Eutopos/Scripts/App/` atau folder baru `Scripts/Capture/`: pengendali rekam, tampilan uGUI, klien tus berbasis `UnityWebRequest`, dan penyimpanan sesi.

Pemisahan dua aplikasi (keputusan 2026-11-19):
- **Dua Build Profile Unity** dengan package ID, nama, dan daftar scene masing-masing: `Eutopos` (scene navigasi saja) dan `Eutopos Capture` (scene capture).
- **Kode capture di assembly definition sendiri** dengan define constraint `EUTOPOS_CAPTURE`, yang hanya diaktifkan di profil capture. Akibatnya kode capture tidak dikompilasi ke APK pengunjung.
- **Kode bersama** (ARCore, `FrameCapture`, klien API) di assembly yang dirujuk keduanya. Kode navigasi dilarang merujuk assembly capture; pelanggaran terdeteksi sebagai gagal kompilasi di build pengunjung.
- **Verifikasi:** ukuran APK pengunjung sebelum dan sesudah pemisahan dibandingkan, dan build report Unity diperiksa tidak memuat kelas capture.

## 9. Pengujian dan kriteria selesai

Otomatis (EditMode): tiap aturan di bagian 4, format bagian 5, pemetaan status, dan logika tus dengan transportasi palsu (termasuk lanjut dari offset dan percobaan ulang).

Di ponsel (oleh Bagus, tidak bisa diotomatisasi):
1. Berjalan merekam di area uji. Foto terambil, bukan nol, dan tidak meledak. Jumlah dan ukuran paket dicatat.
2. Matikan jaringan di tengah unggahan lalu nyalakan lagi: unggahan lanjut dari offset, bukan dari nol.
3. Tutup paksa aplikasi di tengah merekam: foto yang sudah terambil masih ada dan sesi bisa dilanjutkan atau diunggah.
4. Pekerjaan sampai status `queued` di server, lalu `done`, dan layar status aplikasi cocok dengan dashboard.
5. Jarak tempuh dari `frames.jsonl` dibandingkan meteran di lintasan yang diketahui, hanya dicatat sebagai bahan irisan 2 (bukan syarat lulus).

Kriteria jumlah foto yang "wajar" belum ditetapkan karena belum ada pengukuran; ditetapkan setelah uji pertama.

## 10. Keputusan Bagus (dikatakan Bagus)

- ~~Capture adalah fitur di dalam eutopos-mobile.~~ Dibalik 2026-11-19.
- **2026-11-19 (dikunci):** pendekatan terbaik adalah aplikasi capture terpisah yang mempublikasikan foto kunci berpose ARCore, bukan sekadar video. Dibangun sebagai satu repo dengan dua aplikasi (Build Profile), supaya aplikasi pengunjung tidak membawa fitur capture. Unggah video lewat website tetap ada sebagai jalur cadangan (kamera 360, ponsel tanpa ARCore), dengan skala dari jarak diketahui.
- Alur perekam: beri nama peta, rekam, unggah, tunggu proses. Progres tampil sebagai persentase unggah lalu status pending.
- Unggah lewat website tetap ada. Aplikasi menjadi jalur utama untuk capture dan publish.
- Yang gagal tampil lengkap dengan indikasi dan log kenapa; perekam diminta merekam ulang (bagian yang kurang saja, pada irisan 4).
- Arahan Bu Evi: panduan seperti Google, lanjutkan pindaian sampai tumpang tindih, penanda posisi pengguna, lapisan aplikasi capture. Semua panduan ada di irisan 3 dan 4.

## 11. Pilihan Claude (tebakan, periksa)

- Merekam foto kunci dengan pose, bukan video (mengikuti rancangan awal 2026-09-29 dan apa yang sudah dibuktikan `FrameCapture`).
- ~~Satu aplikasi dua mode.~~ Diganti keputusan Bagus 2026-11-19: dua aplikasi dari satu repo.
- Paket zip, nilai ambang foto kunci (0,4 m, 10 derajat, 0,7 m/s), batas 1500 foto, minimal 5 foto uji manual.
- Unggah hanya di latar depan.
- Identitas sementara lewat token layanan (irisan ini), diganti login per orang oleh rencana tenant.
- `area_id` diturunkan dari nama yang diketik. Memakai nama yang sama menambah versi baru pada area yang sama.
- Koordinat pose disimpan apa adanya dalam kerangka dunia Unity beserta penandanya; konversi ke kerangka SfM ditunda ke irisan 2.

## 12. Terbuka

1. Bahwa foto kunci berjarak 0,4 m menghasilkan peta sebagus video 2 fps belum dibuktikan. Uji pertama harus membandingkan keduanya di lorong yang sama.
2. Orientasi pose dan intrinsik ARCore terhadap gambar CPU (butir 3 di bagian 8 spesifikasi 2026-09-29). Navigasi sudah memecahkannya untuk lokalisasi, tetapi untuk pose penuh yang disimpan perlu diverifikasi lagi.
3. Apakah `System.IO.Compression.ZipFile` tersedia dan cukup cepat di build IL2CPP Unity 6000.3 untuk paket ratusan foto. Harus dicoba.
4. Cara masuk jangka panjang (rencana tenant).
5. Berapa foto uji minimal yang menjadi aturan gerbang; angka 5 di sini harus sama dengan konstanta gerbang di server.

## 13. Urutan irisan berikutnya

2. Pose dipakai untuk skala meter dan galat posisi gerbang.
3. Panduan merekam: peta kecil, jejak jalur, penanda posisi.
4. Tanda bagian kurang dan melengkapi peta (butuh laporan per segmen dari server).
