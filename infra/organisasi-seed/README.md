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

## Yang sengaja tidak dilakukan

- `"User"."passwordHash"` dan `"email"` dibiarkan `NULL`: login lewat SSO, dan keduanya tidak pernah
  diproyeksikan ke respons.
- Unit OTK tidak punya `provinsi` (OTK tidak memuatnya), jadi lingkup `WILAYAH` hanya menjangkau unit
  demo Riau. Mengisi provinsi untuk unit vertikal nyata butuh sumber lain (BMN/SIMAN) dan di luar cakupan.
- `"KantorBmn"."unitId"` tetap `NULL` (lihat `infra/kantor-bmn-seed`).
- Tanpa perubahan struktur tabel.

## Penjaga "tidak pernah di production"

Sama dengan `kantor-bmn-seed`: `NODE_ENV` bukan `production`, host `DATABASE_URL` harus host
development yang dikenal, dan `--yes-development` wajib.

## Tes

```bash
node --test infra/organisasi-seed/bangun.spec.mjs
```
