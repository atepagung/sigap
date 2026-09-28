# skema

Sumber kebenaran struktur database SIGAP: **32 tabel prototipe apa adanya, ditambah dua
perubahan yang disetujui pemilik proyek.** sigap-api memetakannya secara schema-first (P4.2) —
EF Core tidak pernah membuat maupun mengubah tabel.

```bash
docker compose up -d postgres
node infra/skema/terapkan.mjs --yes-development
```

| Berkas | Isi | Asal |
| --- | --- | --- |
| `00-prototipe-32-tabel.sql` | 32 tabel, 11 enum, 19 indeks, 49 foreign key | `prisma migrate diff` dari `schema.prisma` prototipe, **tidak disunting tangan** |
| `10-disetujui-kantorbmn-iskoordinatdummy.sql` | kolom `"KantorBmn"."isKoordinatDummy"` | disetujui 18 Sep 2026 (P3.5) |
| `11-disetujui-broadcast-sasaran-unit.sql` | tabel ke-33 `"BroadcastSasaranUnit"` | salinan verbatim API_CONTRACT bagian 5, disetujui 18 Sep 2026 |

Prototipe tidak punya folder migrasi — skemanya dipasang dengan `prisma db push`. Karena itu
DDL hasil Prisma sendiri adalah bentuk fisik yang sesungguhnya, dan dipakai apa adanya.

## Mengubah skema

Jangan. Aturan mutlak proyek no. 5: struktur 32 tabel tidak berubah, dan setiap perubahan
dibawa ke pemilik proyek lebih dulu. Bila disetujui, tambahkan berkas `1x-disetujui-*.sql`
baru — jangan menyunting `00-*` — lalu sesuaikan entity di
`apps/sigap-api/src/Sigap.Infrastructure/Persistensi/`. Tes `SkemaTests` akan gagal sampai
keduanya sama.

## Penjaga

| Tes | Membuktikan |
| --- | --- |
| `SkemaTests` (tanpa database) | model EF = DDL di folder ini: tabel, kolom, tipe, nullability, PK, 52 FK beserta aksi hapusnya, indeks termasuk yang parsial, CHECK, label enum |
| `DatabaseTests` (PostgreSQL dev) | DDL di folder ini = database yang terpasang; setiap tabel terbaca dan tertulis lewat EF |

`DatabaseTests` dilaporkan **Skipped** — bukan lulus — bila database dev tidak terjangkau.

## `terapkan.mjs`

- Hanya development. Tiga penjaga, sama dengan seeder P3.5: menolak `NODE_ENV=production`,
  host di luar daftar dev, dan tanpa `--yes-development`.
- Satu transaksi: gagal di tengah berarti tidak ada yang berubah. Aman dijalankan ulang.
- `"KantorBmn"` boleh sudah ada lebih dulu (dibuat seeder P3.5). Barisnya dipertahankan; kolom
  waktunya dikembalikan dari `timestamptz` ke `TIMESTAMP(3)` sesuai Prisma — penyimpangan dari
  seeder itu tidak pernah disetujui. Dibuktikan 21 Sep 2026: 1.431 baris cocok 100% dengan
  berkas sumber seeder, satu-satunya perubahan adalah pembulatan mikrodetik ke milidetik,
  persis cara Prisma menyimpan.
- Migrasi di lingkungan selain dev menunggu jawaban BaTII (Lampiran E #14).
