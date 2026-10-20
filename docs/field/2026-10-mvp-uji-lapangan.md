# Prosedur uji lapangan MVP (spesifikasi bagian 12)

Hasil mentah (log JSONL, koordinat, foto) disimpan di luar git.

Area uji MVP: **`lab10`** (lab lantai 10, area peta tersendiri; keputusan 2026-10-16). Prosedur ini berlaku sama
untuk area lain dengan mengganti nama area.

## Persiapan
- Layanan melayani area uji: `/health` menunjukkan `area_id` yang benar dan `aligned: true`.
- `align.json` dan `navgraph.json` terpasang untuk versi peta aktif (`python -m server.manage align`).
- Laporkan jarak P1 ke P2 menurut graf bersama hasil T2.
- Ponsel perekam peta, baterai penuh, aplikasi versi terbaru.
- Titik untuk menyetel parameter (interval, ambang) berbeda dengan titik untuk melaporkan hasil.

## T1 Lokalisasi di titik (per titik, minimal 3 kali)
1. Berdiri dengan ujung kaki di titik terukur, ponsel setinggi dada.
2. Tekan Lokalisasi, tunggu "Posisi diperbarui".
3. Panel uji: pilih ID titik, tekan "Saya berdiri di titik ini".

## T2 Navigasi P1 ke P2 (per pasangan, minimal 3 kali)
1. Lokalisasi di P1. Pilih target P2, tekan Mulai.
2. Ikuti garis; jangan menunjuk dinding polos lama-lama.
3. Saat banner Sampai muncul, berhenti di tempat dan ukur jarak sebenarnya ke P2 dengan meteran.
4. Berhasil bila Sampai muncul DAN jarak sebenarnya ≤ 1,0 m.

## T3 Kehilangan pelacakan
1. Di tengah rute, tutup kamera 3 sampai 5 detik.
2. Catat: pesan muncul, rute disembunyikan, navigasi pulih setelah satu koreksi.

## Tabel hasil (isi di salinan lokal, bukan di repo)

| Uji | Titik / pasangan | Ulangan | Sampai? | Jarak sebenarnya (m) | Galat posisi T1 (m) | Durasi (s) | Koreksi diterima | Lompatan > 3 m | Catatan |
|---|---|---|---|---|---|---|---|---|---|
