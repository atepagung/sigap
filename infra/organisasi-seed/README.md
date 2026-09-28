# organisasi-seed

Mengisi `"Unit"`, `"User"`, dan `"UserRole"` di PostgreSQL **development**. Tanpa ini lingkup data
sepuluh akun uji Keycloak kosong (fail-closed) dan setiap dashboard menampilkan angka nol.
**Bukan dummy platform**: OTK-nya asli; yang demo hanya lima unit dan akun ujinya, semuanya
`isDemo = true` sehingga bisa dihapus sekaligus sebelum dipakai sungguhan.

```bash
node infra/organisasi-seed/seed.mjs --yes-development
```

Sumber OTK (`otk_bundle.json`) ada di luar repo. Jalur bawaan `C:/dev/MKB APPS/App/data/otk_bundle.json`;
ganti lewat `--otk=<path>` atau variabel `OTK_BUNDLE`.

## Isinya

| Sumber | Baris | Catatan |
| --- | --- | --- |
| OTK sampai Eselon III | 444 unit, `isDemo = false` | Eselon IV sengaja tidak dimuat (seperti prototipe). `kode` = `unit_key` OTK, `id` = `otk-<unit_key>`, jadi seeding ulang tidak menggandakan |
| Lima unit demo | `isDemo = true` | Yang dirujuk klaim `kode_satker` akun uji: KPP Madya Pekanbaru, Kanwil Riau, KPPN Pekanbaru, dua KPP Pratama. Semuanya `provinsi = 'Riau'` |
| Sepuluh akun uji | `isDemo = true` | Dibaca dari `infra/keycloak/import/kemenkeu-realm.json` (satu sumber kebenaran): NIP, unit (`kode_satker`), dan peran dari grup |
| Pegawai pelengkap | 12 pegawai di KPP Madya | NIP `9000000000000001xx`, peran `PEGAWAI`; agar penyebut rekap Safety Check bukan 1 |
| Akun layanan BMKG | `SISTEM-BMKG`, `isDemo = false` | Baris `"User"` **aktif tanpa satu pun `"UserRole"`** di unit akar `kemenkeu`: pelaku broadcast otomatis BMKG (ACCESS_RULES A11). NIP-nya dijaga tes agar sama dengan `Bmkg:NipLayanan` di `appsettings.json`. Bukan data demo, jadi tidak ikut terhapus saat data demo dibersihkan |

## Yang sengaja tidak dilakukan

- `"User"."passwordHash"` dan `"email"` dibiarkan `NULL`: login lewat SSO, dan keduanya tidak pernah
  diproyeksikan ke respons.
- Unit OTK tidak punya `provinsi` maupun `kabkota` (OTK tidak memuatnya), jadi lingkup `WILAYAH` hanya menjangkau unit
  demo Riau, dan pemicu otomatis BMKG hanya menjangkau unit yang `kabkota`-nya terisi (kelima unit demo: `Kota Pekanbaru`). Mengisi provinsi untuk unit vertikal nyata butuh sumber lain (BMN/SIMAN) dan di luar cakupan.
- `"KantorBmn"."unitId"` diisi skrip terpisah di bawah, bukan `seed.mjs`.
- Tanpa perubahan struktur tabel.

## Unit vertikal dummy (KPP/KPPN/KPPBC)

Di platform asli data organisasi sudah sampai lokasi kantor dan ditarik lewat API; sementara ini
`kantor-vertikal-dummy.mjs` membuat satu unit `isDemo = true` per `kodeSatker` di `"KantorBmn"`
dan mengisi `"KantorBmn"."unitId"`. Jalankan setelah `seed.mjs` dan `kantor-bmn-seed`.
Bawaannya **dry-run** (melaporkan saja); menulis butuh `--yes-development`. Aman dijalankan ulang.

```bash
node infra/organisasi-seed/kantor-vertikal-dummy.mjs                    # laporan
node infra/organisasi-seed/kantor-vertikal-dummy.mjs --yes-development  # tulis
```

Hasil pada data dev (25 Sep 2026): 849 unit, 1.424 dari 1.431 gedung tertaut. Tujuh gedung dilewati
(`kodeSatker`/`namaSatker`/`kabkota` kosong atau Eselon I tak dikenal) dan 44 satker tersebar di lebih
dari satu kabupaten/kota, sehingga unitnya memakai kabupaten/kota terbanyak. Lihat DUMMY_REGISTRY
bagian 9 butir 19.

## Penjaga "tidak pernah di production"

Sama dengan `kantor-bmn-seed`: `NODE_ENV` bukan `production`, host `DATABASE_URL` harus host
development yang dikenal, dan `--yes-development` wajib.

## Tes

```bash
node --test infra/organisasi-seed/*.spec.mjs
```
