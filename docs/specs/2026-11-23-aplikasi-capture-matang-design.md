# Aplikasi capture yang matang, dengan uji di tempat (desain)

Status: draf, menunggu persetujuan Bagus. Tanggal: 2026-11-23. Belum dikerjakan apa pun.

Dokumen ini melanjutkan `2026-11-05-mode-rekam-peta-irisan-1-design.md` (selanjutnya "irisan 1"). Yang tetap berlaku dari
irisan 1: dua aplikasi lewat Build Profile (bagian 8), format paket (bagian 5, sudah diterima server lewat PR #110),
klien tus (bagian 6), aturan foto kunci (bagian 4), dan anti-tujuan (bagian 2). Yang diganti: alur layar perekam
(bagian 3) dan urutan irisan (bagian 13). Panduan perekaman yang dulu ditunda ke irisan 3 dan 4 sebagian ditarik ke sini.

Bagian "Keputusan Bagus" memuat hanya yang Bagus katakan. "Pilihan Claude" adalah tebakan yang harus diperiksa.

## 1. Maksud

Perekam (developer atau admin organisasi) merekam satu area dengan ponsel, lalu **menguji lokalisasi di lokasi yang
sama** untuk membuktikan hasil scan layak. Yang membuat perekam yakin adalah uji di lapangan itu, bukan indikator di
layar. Tampilan selama merekam hanya mengurangi jumlah perjalanan ulang ke lapangan.

## 2. Bukti yang membentuk desain ini

Dari peta P1-P2 (118 foto berpose, 8 foto uji dari sesi lain, diagnosis 22-23 Nov,
`D:/spike-lapangan/capture-p1-p2-terpandu-20261121/uji-manual-offline/`):

- **Keterhubungan peta bukan masalah.** 118 dari 118 foto terdaftar dalam satu peta (ambang ALIKED 0,01).
- **Retrieval bukan masalah.** Lima foto peta terbaik (oracle) memberi inlier hampir sama dengan retrieval top 5.
- **Penyebab gagal adalah cakupan arah pandang dan isi pandangan.** Foto uji yang gagal menghadap jendela silau,
  pemandangan luar, atau dinding putih dari dekat. Tidak ada foto peta yang melihat arah itu dengan fitur cukup.
  Lolos 5 dari 8.
- **Ukurannya masih tipis.** 8 foto uji dari satu sesi tidak cukup untuk menyebut peta "layak".

Akibatnya: fitur yang membantu perekam **melihat arah mana yang belum difoto** dan **sengaja memotret benda bertekstur**
bernilai tinggi. Fitur yang menilai keterhubungan antarfoto tidak menjawab masalah yang ada.

## 3. Tujuan, bukan tujuan, anti-tujuan

Tujuan:
1. Perekam melihat di dunia AR di mana dan ke arah mana setiap foto diambil, selama merekam.
2. Perekam bisa memotret manual selain otomatis, dan membatalkan foto terakhir.
3. Rekaman sampai ke server dari ponsel dan perekam melihat status pekerjaan sampai Siap, Ditahan, atau Gagal.
4. Setelah peta kandidat jadi, perekam menguji lokalisasi di tempat **tanpa menerbitkan peta ke production**, dan
   setiap titik uji tampil sebagai penanda hijau atau merah di dunia AR.
5. Perekam melihat titik 3D peta hasil server menempel pada dunia nyata setelah lokalisasi berhasil.

Bukan tujuan:
- Menambah foto ke peta yang sudah ada. Bila ada titik merah, **seluruh area direkam ulang** (keputusan Bagus).
- Notifikasi push. FCM dipilih, tetapi masuk irisan berikutnya (bagian 9).
- Indikator keterhubungan antarfoto dari ID titik fitur ARCore bersama (ditolak, bagian 5).
- Petunjuk gagal lokalisasi di aplikasi navigasi pengunjung (irisan terpisah, bagian 9).
- Penilaian isi gambar di ponsel (deteksi jendela, buram berbasis isi).
- iOS, unggahan latar belakang.

Anti-tujuan (selain yang di irisan 1 bagian 2):
- Uji di tempat yang diam-diam mengganti peta yang dilayani ke pengguna.
- Penanda hijau yang tampil padahal pose dari server tidak lolos ambang inlier.
- Tampilan AR yang membuat ponsel tersendat sampai pelacakan ARCore memburuk.
- Hasil uji di tempat otomatis dipakai sebagai foto uji gerbang untuk versi berikutnya yang dibangun setelah
  melihat hasil itu (himpunan uji terbakar bila dipakai untuk memperbaiki lalu mengukur lagi).

## 4. Keputusan Bagus

- 2026-11-23: alasan utama adalah bisa turun ke lapangan dan menguji lokalisasi di sana untuk memvalidasi scan.
- 2026-11-23: menunggu pembangunan peta selama apa pun boleh, asalkan ada notifikasi saat selesai.
- 2026-11-23: notifikasi lewat **FCM** (sempat memilih ntfy on-premise, lalu pindah ke FCM setelah mendengar
  batasannya). **Notifikasi belum perlu di irisan ini**, masukkan ke rencana.
- 2026-11-23: bila ada bagian kurang, **rekam ulang** saja dulu; menambah foto ke peta yang ada masuk rencana.
- 2026-11-23: utamakan aplikasi capture yang **matang**; titik hasil scan ditampilkan seperti Immersal.
- 2026-11-23: menyetujui urutan yang dikoreksi: server kandidat, irisan A, irisan B, lalu notifikasi dan petunjuk gagal.
- Gaya Immersal Mapper: rekam manual, objek 3D melayang di tiap foto, titik fitur.
- 2026-11-23 (mengikuti rekomendasi Claude): **pekerjaan dari aplikasi capture tidak pernah terbit otomatis.**
  Berhenti sebagai kandidat berstatus "Siap diuji"; admin menerbitkan manual dari dashboard setelah uji di tempat
  lolos. Tombol terbit di aplikasi menyusul bersama identitas admin.
- 2026-11-23 (mengikuti rekomendasi Claude): **identitas aplikasi capture memakai token aplikasi yang diperiksa
  FastAPI sendiri**, bukan token layanan Cloudflare, supaya tetap jalan setelah pindah ke nginx.

## 5. Pendekatan yang dipertimbangkan

**Tampilan titik hasil scan**
1. **Dua lapisan, dua irisan (dipilih).** Irisan A: titik ARCore saat merekam (opsional). Irisan B: titik 3D peta
   server di AR setelah lokalisasi. Lapisan server adalah satu-satunya yang menunjukkan isi peta sebenarnya.
2. Hanya titik ARCore. Cepat, tetapi kualitas peta tetap tidak terlihat sampai uji navigasi biasa.
3. Hanya titik server. Validasi kuat, tetapi perekaman tetap buta.

**Indikator keterhubungan (ditolak).** Usulan awal Claude: menilai tumpang tindih tiap foto dari ID titik fitur ARCore
yang dilihat bersama. Ditolak karena keterhubungan sudah 118/118 (bagian 2), titik ARCore bukan fitur ALIKED, dan
kualitasnya belum pernah diukur. Penggantinya adalah peringatan pelacakan putus dari segmen pose yang sudah ada.
Petunjuk luma `FrameOverlap` dari spike tidak dibawa ke aplikasi.

**Notifikasi.** FCM (dipilih, irisan berikutnya): sampai walau aplikasi ditutup, ponsel tidak perlu menjangkau server
notifikasi, tanpa aplikasi tambahan; butuh proyek Firebase milik Bagus. ntfy self-host: tanpa Google, tetapi butuh
nama host publik baru (bertentangan dengan arah keluar dari Cloudflare ke nginx), aplikasi ntfy terpisah, dan koneksi
terbuka terus. Notifikasi lokal dari perkiraan waktu: ditolak karena hanya tebakan.

**Menguji peta kandidat.** Menerbitkan dulu lalu menguji: ditolak karena mengganti peta pengguna. Dipilih: server
memuat versi kandidat di samping peta yang dilayani, hanya untuk uji (bagian 6).

## 6. Prasyarat server (eutopos-vps, rencana sendiri)

Diperiksa terhadap `origin/main` `0277bd4`.

**S1. Lokalisasi terhadap versi kandidat.** Sekarang `server/active_map.py` memuat satu `Localizer` untuk area
`EUTOPOS_AREA`; `/localize` hanya melayani peta itu.
- Rute baru, misalnya `POST /versions/{version_id}/localize`, dengan masukan dan bentuk respons sama dengan `/localize`.
- Peta kandidat dimuat sesuai permintaan, paling banyak satu sekaligus, dibuang setelah menganggur (nilai ditentukan
  pembangun). Peta yang dilayani tidak berubah dan tidak dimuat ulang.
- Hanya versi yang pekerjaannya selesai dan berkasnya ada. Versi tanpa penyelarasan tetap bisa diuji (hasil hanya
  lolos atau gagal dan inlier), tetapi titik server di irisan B butuh penyelarasan.
- Setiap uji dicatat di log lokalisasi dengan penanda versi kandidat, terpisah dari lalu lintas pengguna.

**S2. Titik peta.** `GET /versions/{version_id}/points?max=N`: titik 3D peta, disampel turun (bawaan beberapa ribu),
dalam kerangka yang sama dengan pose hasil lokalisasi. Dipakai irisan B.

**S3. Identitas aplikasi capture.** `server/auth.py` menolak token layanan Cloudflare Access ("service tokens are not
allowed"), jadi aplikasi belum bisa membuat pekerjaan atau mengunggah. Keputusan: **token aplikasi yang diperiksa
FastAPI sendiri**, tidak bergantung pada Cloudflare (menggantikan irisan 1 bagian 7 butir 2).
- Token dibuat admin dari dashboard, satu per perangkat atau perekam, diberi nama, bisa dicabut.
- Server hanya menyimpan hash token. Token mentah tampil sekali saat dibuat.
- Token hanya berlaku untuk rute capture (membuat pekerjaan, unggah tus dan hook-nya, status pekerjaan), S1, dan S2.
  Rute admin lain tetap menolaknya.
- Identitas dicatat sebagai `app:<nama token>` di pekerjaan dan log.
- Selama Cloudflare Access masih di depan server, rute-rute ini harus bisa dilewati aplikasi; cara melewatkannya di
  lapisan proxy diputuskan di rencana server dan ditanyakan ke Bagus sebelum mengubah PC lab.

**S4. Pekerjaan capture tidak terbit otomatis.** `publish_loop` di `server/active_map.py` menerbitkan versi yang lolos
gerbang bila `EUTOPOS_AUTO_PUBLISH` menyala. Pekerjaan yang dibuat dengan token aplikasi dikecualikan: berhenti sebagai
kandidat walau lolos gerbang. Penerbitan manual dari dashboard tidak berubah. Aplikasi menampilkan kandidat yang lolos
gerbang sebagai "Siap diuji" dan yang tidak lolos sebagai "Ditahan" beserta alasan gerbang.

Urutan: S3, S4, dan S1 sebelum irisan A selesai diuji di lapangan. S2 sebelum irisan B. Rencana server ditulis dan
dikerjakan lebih dulu; rencana aplikasi menyusul dan mengunci bentuk respons S1 dan S2 dengan fixture kontrak, seperti
`ContractFixturesTests` yang sudah ada.

## 7. Irisan A: merekam

Membangun irisan 1 (Build Profile `Eutopos Capture`, assembly `EUTOPOS_CAPTURE`, paket, tus, status, lanjut setelah
aplikasi ditutup) dengan layar rekam berikut.

**Layar rekam**

```
+---------------------------------------+
| 142 foto  38 m  06:12   [Otomatis ON]  |
|                                       |
|     (kamera)   [] []  bingkai foto    |
|              []     lama melayang     |
|        . . :. titik ARCore (opsi)     |
|                                       |
| ! Pelacakan putus, kembali ke bingkai  |  <- hanya bila terjadi
|                                       |
|   [Batal foto]    ( O )    [Selesai]  |
+---------------------------------------+
```

- **Otomatis dan manual dalam satu layar.** Saklar Otomatis memakai aturan irisan 1 bagian 4 (0,4 m atau 10 derajat,
  di bawah 0,7 m/s, hanya Tracking). Tombol rana selalu ada: memotret satu foto kapan pun selama Tracking dan di bawah
  batas kecepatan. Bila ditolak, tampilkan alasannya singkat.
- **Bingkai kamera melayang.** Setiap foto digambar sebagai piramida kerangka kecil di pose foto itu, ujung di
  pusat kamera, alas ke arah pandang. Foto terakhir disorot. Bingkai dari segmen pose yang berbeda dari segmen
  pertama diberi warna lain, sehingga putus pelacakan terlihat di ruang.
- **Batal foto.** Menghapus foto terakhir (berkas, baris `frames.jsonl`, bingkainya). Boleh diulang mundur.
  Penomoran berkas tetap berurutan tanpa celah.
- **Peringatan pelacakan putus.** Saat status bukan Tracking atau `PoseContinuity` mendeteksi lompatan pose: pengambilan
  otomatis berhenti dan spanduk meminta kembali ke area bingkai terakhir.
- **Titik ARCore (opsional, boleh dipotong bila mahal).** Titik dari `ARPointCloudManager` dikumpulkan menurut ID
  sehingga menumpuk, dengan batas jumlah, bisa dimatikan. Hanya petunjuk visual; tidak memengaruhi apa pun.
- **Penghitung:** jumlah foto, jarak tempuh, durasi, ruang kosong.

**Setelah Selesai:** langkah foto uji (irisan 1 bagian 3 butir 4, minimal 5, manual), lalu unggah dan layar status.
Tambahan: foto uji ditolak bila kurang dari 1 m dari foto uji lain, supaya tidak semua dari satu titik.

**Kriteria selesai A (di ponsel, oleh Bagus):** merekam area L dengan bingkai tampil tanpa pelacakan memburuk; batal
foto bekerja; paket sampai di server dan pekerjaan selesai sebagai kandidat; layar status cocok dengan dashboard;
tutup paksa di tengah merekam tidak menghilangkan foto. Uji otomatis EditMode untuk logika murni (keputusan rana
manual, batal foto dan penomoran, jarak minimal foto uji, pemetaan status).

## 8. Irisan B: uji di tempat

Masuk dari layar status saat pekerjaan selesai (kandidat atau terbit), atau dari daftar sesi.

- Perekam berjalan, mengarahkan kamera, mengetuk **Uji di sini**. Aplikasi mengirim foto dan intrinsik ke S1.
- Penanda ditaruh di **pose ARCore ponsel saat foto diambil**, jadi tidak bergantung pada hasil server: hijau bila
  `status` lolos ambang inlier server, merah bila tidak, dengan jumlah inlier. Penanda merah menunjukkan di mana
  peta kurang.
- **Titik peta server.** Setelah satu uji lolos, aplikasi mengambil S2 dan menggambar titik itu memakai transformasi
  dari pose lokalisasi, dengan cara yang sama aplikasi navigasi menempatkan akar gedung (`BuildingRootController`).
  Titik yang menempel di dinding dan pintu nyata berarti peta selaras; yang melayang berarti tidak.
- **Ringkasan uji:** lolos sekian dari sekian, disimpan di ponsel beserta foto dan pose ARCore tiap titik, bisa
  diekspor. Ini menjadi lembar uji lapangan (12 titik atau lebih). Hasil ini tidak diunggah sebagai foto uji gerbang
  secara otomatis (anti-tujuan).

**Kriteria selesai B:** di area L, uji 12 titik atau lebih tanpa menerbitkan peta; jumlah lolos tercatat; titik
server tampil menempel di tempatnya; peta yang dilayani di `/health` tidak berubah selama dan sesudah uji.

## 9. Sesudahnya (masuk rencana, bukan irisan ini)

1. Notifikasi FCM saat pekerjaan Siap, Ditahan, atau Gagal. Pesan hanya memuat id pekerjaan dan status. Bagus yang
   membuat proyek Firebase.
2. Petunjuk di aplikasi navigasi saat lokalisasi gagal (arahkan ke pintu atau papan, bukan jendela atau dinding polos).
3. Menambah foto ke peta yang ada untuk bagian merah.

## 10. Pilihan Claude (tebakan, periksa)

- Otomatis dan manual dalam satu layar dengan saklar, bukan dua mode terpisah.
- Bingkai digambar sebagai objek biasa di ruang sesi AR, bukan `ARAnchor` per foto (ratusan anchor mahal; sesi
  rekam pendek). Akibatnya bingkai bisa sedikit bergeser bila ARCore mengoreksi peta internalnya.
- Batal foto boleh mundur berulang, penomoran tanpa celah.
- Jarak minimal 1 m antarfoto uji.
- Penanda uji di pose ARCore ponsel, bukan di pose hasil server.
- Rute S1 dan S2 berbasis `version_id`, satu kandidat dimuat sekaligus.
- Titik ARCore opsional dan boleh dipotong.
- Dua rencana berurutan: server (S1 sampai S4) dulu, lalu aplikasi (irisan A, lalu B).
- Irisan A membangun seluruh irisan 1 yang belum ada (klien tus, Build Profile, assembly capture); `PoseContinuity`
  dipindah dari cabang spike `spike/mesh-depth`.
- Ambang hijau atau merah mengikuti `MIN_INLIERS` server (50), sama dengan `/localize` dan gerbang; aplikasi memakai
  `status` dari respons, tidak menghitung ambang sendiri.

## 11. Dibiarkan ke pembangun

- Bentuk, ukuran, dan warna bingkai serta titik; cara menggambar titik (mesh titik atau partikel), dengan syarat
  tidak membuat pelacakan memburuk.
- Batas jumlah titik ARCore dan titik server, dan jumlah sampel bawaan S2.
- Lama menganggur sebelum kandidat dibuang dari memori.
- Tata letak layar status dan daftar sesi, mengikuti gaya aplikasi yang ada.

## 12. Terbuka

1. Memori GPU PC lab cukup atau tidak untuk dua peta sekaligus (yang dilayani dan satu kandidat). Harus diukur sebelum
   S1 dianggap aman; bila tidak cukup, kandidat dimuat di CPU dengan waktu uji lebih lama.
2. Cara rute aplikasi melewati Cloudflare Access selama masih dipakai (S3), sampai pindah ke nginx.
3. Kerangka koordinat titik S2 untuk versi tanpa penyelarasan: ditolak, atau dalam kerangka peta mentah dengan skala
   tidak diketahui.
4. Biaya menggambar ratusan bingkai dan ribuan titik di TECNO SPARK 30 Pro (MT6789) belum diukur.

## 13. Urutan

1. Server S3, S4, dan S1 (eutopos-vps).
2. Irisan A (eutopos-mobile).
3. Server S2, lalu irisan B.
4. Bagian 9, sesuai urutannya.
