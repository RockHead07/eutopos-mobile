# eutopos-mobile

Aplikasi Android (Unity + ARCore) untuk indoor navigation dengan layanan VPS lokal
[eutopos-vps](https://github.com/RockHead07/eutopos-vps). Ponsel mengirim satu foto ke layanan,
menerima posisi dan arah hadap dalam kerangka gedung, lalu ARCore melacak gerak di antara koreksi.

Status: MVP satu lantai dalam pengembangan. Spesifikasi: `docs/specs/2026-10-15-mvp-navigasi-design.md`.

## Menyiapkan

1. Unity 6000.3.14f1 dengan modul Android.
2. Salin `vps-token.example.json` menjadi `Assets/Eutopos/Resources/vps-token.local.json` dan isi
   Client ID dan Secret service token Cloudflare Access. Berkas itu tidak pernah di-commit.

## Lisensi

Copyright (C) 2026 Bagus Insan Pradana. GNU Affero General Public License v3.0, lihat `LICENSE`.

### Aset pihak ketiga

- Model penunjuk tujuan `Assets/Eutopos/Models/MapPointer/`: "Map Pointer (3D Icon)" oleh Alexander
  (https://sketchfab.com/pravdin), sumber https://sketchfab.com/3d-models/map-pointer-3d-icon-a30e2619537a425d90618ae5901c2989,
  lisensi CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Diubah: warna diterapkan lewat material
  Unity sendiri, ukuran dan arah diatur saat ditampilkan. Rincian di `Assets/Eutopos/Models/MapPointer/LICENSE-ASSETS.md`.
