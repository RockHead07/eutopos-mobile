# Spesifikasi: eutopos-mobile, MVP navigasi satu lantai

Tanggal: 2026-10-15. Status: draf untuk ditinjau Bagus.
Repo: `RockHead07/eutopos-mobile` (publik, AGPL-3.0). Server: `RockHead07/eutopos-vps`.
Dasar: arahan pembimbing pada bimbingan 14 Oktober 2026, `eutopos-vps/docs/design-notes.md`
(dua lapis posisi, kerangka gedung, kontrak API), dan sesi perancangan 15 Oktober 2026.

## 1. Tujuan

Aplikasi Android yang membuktikan bahwa indoor navigation berjalan di lantai 10:

1. Di posisi 1 (P1), pengguna menekan **Lokalisasi**. Aplikasi mengirim satu foto ke layanan VPS
   eutopos dan menampilkan posisi serta **arah hadap** pengguna di kerangka gedung.
2. Pengguna memilih target (P2). Aplikasi menggambar rute di lantai (AR) dan di denah mini.
3. Pengguna berjalan. ARCore melacak gerak di antara koreksi, dan VPS mengoreksi posisi secara
   berkala.
4. Di P2 aplikasi menyatakan **Sampai**.

**Ukuran berhasil MVP:** pada uji lapangan T2 (bagian 12), satu percobaan berhasil bila aplikasi
menyatakan Sampai **dan** jarak sebenarnya ke P2 menurut meteran ≤ 1,0 m. Semua percobaan
dilaporkan, termasuk yang gagal. Galat posisi dan arah saat navigasi dicatat untuk buku PA.

## 2. Lingkup

**Masuk:** satu lantai (lantai 10), satu area peta, lokalisasi dengan tombol dan berkala, penyatuan
pose VPS dengan ARCore (4-DoF), rute pada graf titik terukur, tampilan AR dan denah mini, log
evaluasi, panel uji tersembunyi, dua tambahan kecil di server.

**Tidak masuk (spesifikasi terpisah nanti):** aplikasi capture (peta lantai 10 dibuat dari video
kamera bawaan lewat dashboard web), avatar 3D, suara, RAG, multi-lantai dan barometer,
NavMesh, anchoring tool untuk POI, pembuatan graf otomatis, distribusi aplikasi ke pengguna umum.

## 3. Keputusan

| Keputusan | Pilihan | Alasan |
|---|---|---|
| Bentuk aplikasi | Aplikasi navigasi adalah satu build Unity (APK Android native lewat IL2CPP, UI uGUI), tanpa host Flutter, UaaL, atau WebView. Aplikasi capture (eutopos Mapper) tetap aplikasi terpisah, build kedua dari project yang sama, di luar MVP (`eutopos-vps/docs/specs/2026-09-29-capture-and-map-pipeline-design.md` bagian 9 dan 10.2) | Tidak ada aplikasi tuan rumah (beda dengan DARSI yang menempel ke MyRSIy); layar MVP sedikit dan cukup dengan uGUI; satu build dan satu bahasa. Kotlin + ARCore SDK ditolak karena rute AR harus dirender sendiri dan avatar nanti tetap butuh Unity. Hybrid dipertimbangkan ulang hanya bila muncul banyak layar 2D non-AR atau aplikasi harus masuk ke aplikasi instansi |
| Tampilan | AR + denah mini | Tujuan 6 PA (ARCore); denah mini untuk demo dan debug |
| Penyatuan pose | 4-DoF (geser XYZ, putar yaw) | Kemiringan dari ARCore (Y searah gravitasi); galat pitch/roll VPS tidak memiringkan rute |
| Geometri rute | Graf titik terukur + A* | Titik meteran sudah dalam kerangka gedung; tanpa denah dan tanpa bake Unity |
| Sumber graf | Server (`GET /navgraph`) | Satu pemilik data; koordinat gedung tidak masuk repo publik atau APK |
| Akses ke server | Cloudflare Access service token | Server ada di balik Access; token dikirim sebagai header |
| Koreksi | Tombol + otomatis tiap N detik (awal 5 detik) | Lokalisasi awal terkendali; data drift dan latensi terkumpul |
| Ponsel uji | Ponsel perekam peta lantai 10 | Kamera sama dengan peta |
| Repo | Publik, AGPL-3.0 | Sama dengan eutopos-vps; klaim kode terbuka untuk jurnal |
| Barometer | Setelah satu lantai berjalan | Risiko lebih kecil |

## 4. Komponen

Logika inti ditulis sebagai kelas C# murni tanpa ketergantungan Unity agar bisa diuji di EditMode.

| Unit | Jenis | Tugas | Bergantung pada |
|---|---|---|---|
| `FrameConvert` | inti | Konversi kerangka G ke B (posisi, arah depan), yaw dari vektor depan, pemutaran intrinsik untuk foto tegak | - |
| `Aligner4Dof` | inti | Hitung θ dan t dari pose kamera di U (ARCore) dan di B (VPS) | `FrameConvert` |
| `JumpGuard` | inti | Terima, tahan sebagai kandidat, atau tolak koreksi berdasarkan lompatan | - |
| `NavGraph` | inti | Baca dan validasi graf, proyeksi posisi ke sisi terdekat, A* | - |
| `FrameCapture` | Unity | Ambil gambar CPU ARCore + pose kamera pada frame yang sama + intrinsik; putar tegak; perkecil; JPEG | AR Foundation |
| `VpsClient` | Unity | `POST /localize` dan `GET /navgraph` dengan header service token, batas waktu | `UnityWebRequest` |
| `BuildingRootController` | Unity | Pasang `BuildingRoot` pada `ARAnchor` sesuai θ dan t | `Aligner4Dof`, `JumpGuard` |
| `RouteView` | Unity | Garis rute di lantai, panah belokan, deteksi Sampai | `NavGraph` |
| `MiniMap` | Unity | Denah 2D: simpul, rute, posisi, kerucut arah | `NavGraph` |
| `SessionLog` | Unity | Log JSONL per sesi, ekspor | semua |
| `DebugPanel` | Unity | Panel tersembunyi: interval, ambang, ground truth, ekspor log | `SessionLog` |
| `AppController` | Unity | Mengatur keadaan aplikasi dan timer koreksi | semua |

## 5. Kerangka koordinat

| Kerangka | Asal | Sumbu |
|---|---|---|
| **G** gedung (server) | `pose_building` dari `/localize`, diselaraskan oleh `align.json` | Tangan kanan: x sepanjang lorong, y di kiri saat menghadap +x, z ke atas; lantai z = 0 |
| **B** gedung (Unity) | Konversi dari G | Tangan kiri seperti Unity: X = x, Y = z (atas), Z = y |
| **U** sesi ARCore | AR Foundation | Tangan kiri, Y searah gravitasi (diverifikasi, bagian 14), asal sembarang tiap sesi |

Konvensi G mengikuti protokol titik acuan di `eutopos-vps/docs/spike-plan.md` (cabang
`feat/heading-error`): sumbu y berada di kiri saat berdiri menghadap +x.

**Konversi G ke B.** Posisi: `p_B = S · p_G`, dengan `S` menukar komponen kedua dan ketiga
(`(x, y, z) → (x, z, y)`). `pose_building` berisi `position` (meter, G) dan `rotation_xyzw`
(kuaternion urutan x, y, z, w, rotasi gedung-dari-kamera `R_G`, kamera dalam konvensi COLMAP:
x kanan, y bawah, z depan; lihat `Localizer.to_building` di `server/localizer.py`). Arah depan kamera
di B: `fwd_B = S · R_G · (0, 0, 1)`. Untuk 4-DoF hanya
arah depan dan posisi yang dipakai, sehingga roll kamera (termasuk akibat memutar foto) tidak
berpengaruh.

**Yaw.** `yaw(f) = atan2(f.x, f.z)` pada bidang mendatar (dari +Z ke +X, sesuai Unity). Bila
komponen mendatar `f` kurang dari 0,3 kali panjangnya (kamera nyaris tegak lurus lantai), koreksi
ditolak dengan alasan "kamera terlalu menunduk".

**Penyelarasan 4-DoF.** Dari pose kamera di U saat foto diambil (`p_U`, `fwd_U`) dan hasil VPS di B
(`p_B`, `fwd_B`):

```text
θ = yaw(fwd_U) − yaw(fwd_B)
t = p_U − Rot_Y(θ) · p_B
```

`BuildingRoot` ditempatkan di posisi `t` dengan rotasi `Rot_Y(θ)` dan ditempelkan ke `ARAnchor`
(dibuat ulang setiap koreksi diterima). Semua konten gedung (graf, rute, target) adalah anak
`BuildingRoot` dalam koordinat B. Komponen vertikal `t` ikut dari rumus, sehingga lantai (Y_B = 0)
berada di bawah kamera setinggi pegangan ponsel.

**Posisi dan arah pengguna di B** (untuk denah, rute, Sampai, log): pose kamera ARCore saat ini
diubah dengan kebalikan transformasi `BuildingRoot`.

## 6. Alur satu koreksi

1. **Pemicu.** Tombol Lokalisasi, atau timer tiap N detik (awal 5) selama navigasi berjalan.
   Hanya satu permintaan boleh berjalan; giliran timer dilewati bila permintaan sebelumnya belum
   selesai. Timer berhenti selama ARCore tidak dalam keadaan Tracking.
2. **Ambil.** Pada handler `ARCameraManager.frameReceived`, `FrameCapture` memanggil
   `TryAcquireLatestCpuImage`, menyimpan pose kamera (`Camera` di XR Origin) **pada frame yang sama**,
   dan `TryGetIntrinsics`. Saat kamera mulai, dipilih konfigurasi kamera dengan resolusi gambar CPU
   terbesar yang sisi panjangnya tidak lebih dari 1920 piksel (resolusi bawaan ARCore bisa hanya
   640 x 480, terlalu kecil untuk VPS). Gambar diubah ke RGBA, diputar tegak, lalu dikompres JPEG
   (kualitas 90). Intrinsik diskalakan ke ukuran gambar dan diputar bersama gambar.
   Rumus pemutaran 90° searah jarum jam untuk gambar sensor W × H: `fx' = fy`, `fy' = fx`,
   `cx' = H − cy`, `cy' = cx`, ukuran baru H × W. Arah putar ditentukan dari orientasi perangkat
   saat itu dan diuji untuk kedua arah.
3. **Kirim.** `VpsClient` mengirim `POST /localize` (multipart: `image`, `fx`, `fy`, `cx`, `cy`)
   dengan header `CF-Access-Client-Id` dan `CF-Access-Client-Secret`. Batas waktu 15 detik.
4. **Saring.** Ditolak bila `status` bukan `ok`, `inliers` < 50, atau `pose_building` kosong.
5. **Jaga lompatan** (`JumpGuard`, bagian 9).
6. **Terapkan.** `Aligner4Dof` menghitung θ dan t; `BuildingRootController` memindahkan
   `BuildingRoot`. Tampilan boleh dihaluskan sekitar 0,5 detik; log selalu mentah.
7. **Catat** kejadian `correction` (bagian 11), termasuk bila ditolak.

## 7. Rute dan Sampai

- Saat aplikasi mulai, `VpsClient` mengambil `GET /navgraph` sekali dan menyimpannya.
- Posisi pengguna (B, bidang mendatar) diproyeksikan ke sisi graf terdekat. A* dihitung dari titik
  proyeksi ke simpul target, diulang setiap ada koreksi diterima atau pengguna bergerak > 1 m sejak
  perhitungan terakhir.
- `RouteView` menggambar polyline dari posisi pengguna melalui simpul rute di Y_B = 0, plus panah ke
  arah segmen berikutnya.
- **Sampai:** jarak mendatar posisi pengguna ke simpul target < 1,0 m. Kejadian `arrival` dicatat.

**Format `navgraph.json`** (di server, kerangka G, meter):

```json
{
  "frame": "building",
  "units": "m",
  "nodes": [{"id": "P01", "x": 0.0, "y": 0.4}, {"id": "P02", "x": 1.5, "y": 1.6}],
  "edges": [["P01", "P02"]],
  "targets": [{"node": "P02", "name": "Pintu Lab"}]
}
```

Sisi ditulis tangan dari lembar lapangan (satu lorong: rangkaian simpul berurutan). Graf tidak
berarah; panjang sisi = jarak Euclid.

## 8. Tambahan di server (PR terpisah di eutopos-vps)

**a. `GET /navgraph`.** Mengembalikan `{"area_id", "map_version", "graph": <isi navgraph.json>}`
untuk area yang sedang aktif. Berkas dibaca dari `<maps>/<area_id>/navgraph.json` (per area, bukan
per versi peta, karena koordinat gedung tidak berubah saat peta dibangun ulang). 404 bila berkas
tidak ada; 503 bila tidak ada peta aktif. Tanpa identitas pengguna, sama seperti `/localize`.

**b. `python -m server.manage align --version <id> [--align <align.json>] [--navgraph <navgraph.json>]`.**
- `align.json` disalin ke `<folder versi>/kp1024-r1024/align.json` setelah diperiksa: skala > 0,
  kuaternion berpanjang 1 (toleransi 1e-3), `reference_points` ≥ 4, `residual_median_m` ada.
- `navgraph.json` disalin ke `<maps>/<area_id>/navgraph.json` setelah diperiksa: `frame` dan `units`
  benar, ID simpul unik, koordinat berupa angka, setiap sisi dan target merujuk simpul yang ada,
  graf terhubung (peringatan bila tidak).
- Berkas rusak ditolak dengan pesan yang menyebut kesalahannya; tidak ada penyalinan sebagian.
- `align.json` terikat pada satu versi peta. Peta yang dibangun ulang kembali "belum diselaraskan"
  sampai diselaraskan lagi.
- Layanan membaca `align.json` saat memuat peta, sehingga perintah mencetak pengingat untuk
  menerbitkan ulang versi itu dari dashboard.

**c. Service token (dikerjakan Bagus di Cloudflare).** Buat service token `eutopos-mobile` di
Zero Trust, tambahkan kebijakan *Service Auth* untuk token itu pada aplikasi Access `eutopos`.
Client ID dan Secret disimpan di `vps-token.local.json` (gitignored) di proyek Unity.
- Rahasia di APK bisa diekstrak: APK hanya untuk uji, tidak disebar, token dicabut setelah pengujian.
- Harus diuji: token membuka `/localize`, `/health`, `/navgraph`, tetapi **tidak** `/api/*`
  (`server/auth.py` membutuhkan email, service token tidak membawa email).

## 9. Penanganan galat

| Kejadian | Reaksi aplikasi |
|---|---|
| Jaringan putus atau batas waktu | Penyelarasan terakhir tetap dipakai, pesan "koreksi gagal (jaringan)", coba lagi giliran berikutnya |
| 401 atau 403 | Pesan token tidak ada atau salah, timer berhenti |
| 503 | Banner "layanan belum punya peta", lokalisasi nonaktif |
| `status: failed` | Diabaikan; setelah 3 kali berturut-turut, saran "arahkan kamera ke area bertekstur, bukan dinding polos" |
| `pose_building` kosong | Navigasi diblokir, pesan "peta belum diselaraskan" |
| Kamera terlalu menunduk | Koreksi ditolak, saran "tegakkan ponsel" |
| Lompatan > 3 m dari posisi perkiraan dan koreksi terakhir yang diterima < 30 detik lalu | Disimpan sebagai kandidat; diterapkan hanya bila koreksi berikutnya berjarak < 1 m darinya; bila tidak, kandidat dibuang dan kejadian dicatat. Koreksi pertama dalam sesi selalu diterima |
| ARCore kehilangan pelacakan | Timer berhenti, pesan "gerakkan ponsel perlahan"; setelah pulih, navigasi menunggu satu koreksi baru |
| `/navgraph` 404 | Posisi dan arah tetap tampil, pilihan target nonaktif |

Ambang 3 m sama dengan ambang "salah yakin" `eval_meter.py`. Ambang 50 inlier sama dengan layanan.
Ambang lompatan (3 m) dan interval koreksi dapat diubah di panel uji; 30 detik dan 1 m tetap di kode.

## 10. Tampilan (tegak)

- Kamera AR penuh layar; garis rute di lantai dan panah ke belokan berikutnya.
- Bilah status atas: koneksi, `map_version`, inlier koreksi terakhir, umur koreksi, keadaan pelacakan.
- Bawah: tombol Lokalisasi, pilihan Target, tombol mulai atau berhenti navigasi.
- Denah mini di pojok (bisa dibesarkan): simpul, rute, posisi, kerucut arah.
- Banner Sampai beserta jarak akhir menurut aplikasi.
- Panel uji (ketuk logo 5 kali): interval koreksi, ambang JumpGuard, ekspor log, dan tombol
  **"Saya berdiri di titik …"** (pilih ID simpul) yang mencatat posisi dan arah menurut aplikasi
  terhadap koordinat meteran titik itu.

## 11. Log evaluasi (`SessionLog`)

Satu berkas JSONL per sesi di `Application.persistentDataPath/logs/`, dapat diekspor lewat panel uji.
Setiap baris punya `t` (detik sejak sesi mulai) dan `type`.

| `type` | Isi |
|---|---|
| `session_start` | ponsel, versi aplikasi, `area_id`, `map_version`, pengaturan |
| `pose` (tiap 1 detik) | pose ARCore mentah di U; posisi dan yaw di B (bila sudah selaras) |
| `correction` | waktu ambil, latensi total dan `t_s` dari server, inliers, status, pose VPS di B, lompatan (m), keputusan (diterima, kandidat, ditolak) dan alasannya |
| `route` | target, daftar simpul, panjang rute |
| `arrival` | target, jarak menurut aplikasi, durasi sejak mulai, jumlah koreksi diterima |
| `ground_truth` | ID titik, posisi dan yaw menurut aplikasi, koordinat meteran, galat posisi (m) |
| `tracking` | perubahan keadaan pelacakan ARCore |

## 12. Pengujian

**Otomatis (Unity EditMode, kelas inti):**
- `FrameConvert`: kasus sumbu yang diketahui (G ke B), yaw tiap arah sumbu, penolakan kamera menunduk,
  pemutaran intrinsik (titik 3D sintetis diproyeksikan sebelum dan sesudah pemutaran harus sama).
- `Aligner4Dof`: θ dan t buatan dipulihkan dari pasangan pose sintetis.
- `NavGraph`: A* pada graf kecil, proyeksi ke sisi, graf rusak ditolak (ID ganda, sisi ke simpul tak
  ada, koordinat bukan angka).
- `JumpGuard`: koreksi pertama diterima, lompatan besar jadi kandidat, kandidat dikonfirmasi dan
  dibuang, lompatan setelah 30 detik diterima.
- Ambang Sampai.

**Server (pytest di eutopos-vps):** `/navgraph` 200, 404, 503; `manage align` menolak berkas rusak dan
tidak menyalin sebagian.

**Lapangan** (setelah titik meteran, `align.json`, dan `navgraph.json` terpasang):

| Uji | Isi | Yang dicatat |
|---|---|---|
| T1 Lokalisasi di titik | Berdiri di titik terukur, Lokalisasi, "Saya berdiri di titik" | Galat posisi (m) dan arah (°) |
| T2 Navigasi P1 ke P2 | Pilih target P2, ikuti rute, berhenti saat Sampai, ukur jarak sebenarnya ke P2 | Berhasil atau tidak, jarak sebenarnya, durasi, koreksi, lompatan |
| T3 Kehilangan pelacakan | Tutup kamera beberapa detik di tengah rute | Pemulihan |

Setiap pasangan titik diulang minimal 3 kali. Titik untuk menyetel parameter (interval, ambang)
berbeda dengan titik untuk melaporkan hasil (prinsip "jangan membakar set uji",
`design-notes.md` bagian 5).

## 13. Struktur proyek

```text
eutopos-mobile/
  Assets/Eutopos/
    Scripts/Core/      FrameConvert, Aligner4Dof, JumpGuard, NavGraph, NavGraphJson
    Scripts/App/       FrameCapture, VpsClient, BuildingRootController, RouteView, MiniMap,
                       SessionLog, DebugPanel, AppController
    Tests/EditMode/
    Scenes/Main.unity
  docs/specs/
  vps-token.local.json  (gitignored)
  LICENSE               (AGPL-3.0)
```

Unity 6000.3.14f1 (sama dengan DARSI), AR Foundation dan ARCore XR Plugin 6.3.x, Input System, uGUI.
Tanpa MultiSet, Photon, AI Navigation, dan avatar.

`Scripts/Core` memegang peran folder `Shared` di rancangan 29 September: tanpa ketergantungan Unity,
sehingga aplikasi capture nanti bisa memakainya tanpa bergantung pada `Scripts/App` (navigasi).

**Dari DARSI:** hampir tidak ada berkas yang disalin. Navigasi DARSI terikat ke SDK MultiSet dan
material jalurnya berasal dari sampel MultiSet, sehingga tidak boleh disalin. Yang diambil hanya
pola: panel uji tersembunyi untuk ground truth (ADR-025) dan penyimpanan rahasia di berkas yang
di-gitignore.

**Repo saat ini:** `README.md` tersimpan dalam UTF-16 dan belum ada `LICENSE`; keduanya dibereskan
pada commit pertama.

## 14. Risiko dan hal yang harus diverifikasi di awal implementasi

| Hal | Cara cek |
|---|---|
| Sumbu Y sesi ARCore searah gravitasi | Log vektor atas kamera saat ponsel diam tegak dan diputar; bila tidak, pertimbangkan 6-DoF |
| Resolusi intrinsik ARCore sama dengan resolusi gambar CPU | Bandingkan `XRCameraIntrinsics.resolution` dengan ukuran gambar CPU; skalakan bila beda |
| Arah putar gambar untuk layar tegak | Uji kirim satu foto tegak ke `/localize`; foto miring menghasilkan pose salah (`design-notes.md` bagian 6) |
| Latensi `/localize` (layanan berjalan di CPU) | Catat `t_s` di log; menentukan interval koreksi yang masuk akal |
| Service token tidak membuka `/api/*` | Uji dengan `curl` setelah token dibuat |
| Kamera ARCore berbeda dari kamera video peta (meski ponsel sama) | Bandingkan inlier foto ARCore dengan foto uji sebelumnya di titik yang sama |
| `ARAnchor` dibuat ulang tiap koreksi | Pantau jumlah anchor; hapus anchor lama |

## 15. Urutan kerja (garis besar untuk rencana implementasi)

1. Kerangka proyek Unity, LICENSE, README, gitignore, paket.
2. Kelas inti + tes EditMode (`FrameConvert`, `Aligner4Dof`, `JumpGuard`, `NavGraph`).
3. PR server: `GET /navgraph` dan `manage align` + pytest.
4. `FrameCapture` + `VpsClient` + pemeriksaan bagian 14 di ponsel (foto ke `/localize`, latensi).
5. `BuildingRootController`, `RouteView`, `MiniMap`, `SessionLog`, `DebugPanel`, `AppController`.
6. Uji lapangan T1 sampai T3 setelah titik meteran dan `align.json` tersedia.
