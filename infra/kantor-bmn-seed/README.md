# kantor-bmn-seed

Seeder data master **1.431 gedung kantor** Kemenkeu (Master Aset BMN) untuk PostgreSQL
development. **Bukan dummy platform** — datanya asli dari BMN. Yang dummy hanya **koordinatnya**,
karena SIMAN belum menyediakannya (PLAYBOOK Lampiran E #16, satu-satunya penghalang fitur peta).

```bash
node infra/kantor-bmn-seed/seed.mjs --yes-development
```

## Kenapa ada penanda `isKoordinatDummy`

**[DISETUJUI 18 Sep 2026, P3.5]** — perubahan struktur tabel `KantorBmn` (kolom baru
`isKoordinatDummy`), disetujui pemilik proyek secara eksplisit sebelum dikerjakan.

Untuk aplikasi tanggap darurat, koordinat palsu yang terlanjur dianggap asli bisa berarti tim
dikirim ke lokasi yang salah. Karena itu setiap baris yang koordinatnya digenerate (semua 1.431
baris saat ini, karena SIMAN belum mengirim satu pun) ditandai `isKoordinatDummy = true` di
database — bukan sekadar dicatat di dokumen. **Kode UI peta (P4) wajib membaca kolom ini dan
menampilkan banner peringatan selama ada baris `isKoordinatDummy = true` di data yang
ditampilkan** — UI-nya sendiri belum dibangun (route `katalog-kantor` di luar scope Fase 1, tapi
`PetaBencana`/`data-bencana` yang in-scope kemungkinan memakai data ini juga).

## Cara koordinat dummy dibuat

`generate.mjs` mengisi `lintang`/`bujur` yang kosong dengan titik acak **di dalam kotak batas
(bounding box) provinsi gedung itu berada** (`provinsi-bbox.mjs`, 34 provinsi — persis yang ada di
data), ditarik 8% ke tengah supaya tidak nangkring di sudut kotak yang sering kali laut.

- **Deterministik**: RNG di-seed dari `register` (id BMN), jadi seeding ulang tidak membuat titik
  "melompat". Terbukti: jalankan seeder dua kali → 1.431 baris, koordinat identik (bukan hanya
  idempoten jumlahnya, tapi nilainya).
- **Tidak mengarang di luar provinsi yang dikenal.** Kalau ada provinsi baru yang bbox-nya belum
  terdaftar, koordinatnya dibiarkan `null` (bukan ditebak sembarangan) dan dicatat di
  `provinsiTakDikenal`.
- **Bbox itu kotak kasar, bukan batas administratif presisi.** Untuk provinsi kepulauan (Maluku,
  NTT, Kepulauan Riau, dll.) sebagian titik bisa jatuh di laut dalam kotak itu. Cukup untuk
  "tersebar di wilayah provinsi yang sesuai" seperti diminta, tidak untuk presisi geografis.
- **`unitId` dibiarkan `NULL` untuk semua baris.** Menautkan tiap gedung ke unit organisasi (OTK)
  di luar cakupan permintaan ini dan butuh logika pencocokan tersendiri.

## Verifikasi yang sudah dijalankan (18 Sep 2026)

- 7 tes `generate.spec.mjs` lulus (`node --test infra/kantor-bmn-seed/generate.spec.mjs`):
  bbox tervalidasi masuk akal, baris berkoordinat asli tidak disentuh, deterministik, provinsi
  tak dikenal tidak dikarang.
- Seeding sungguhan ke Postgres: 1.431/1.431 baris masuk, kolom `isKoordinatDummy` terverifikasi
  ada di skema (`boolean NOT NULL DEFAULT false`), rata-rata koordinat per provinsi masuk akal
  (mis. DKI Jakarta ≈ -6.23/106.84, Aceh ≈ 4.02/96.68).
  Detail per-provinsi dan sampel baris di riwayat sesi.
- Idempotensi dibuktikan (bukan diasumsikan): jalan dua kali → tetap 1.431 baris, koordinat
  baris yang sama persis identik sebelum/sesudah.
- Tiga penjaga "tidak pernah di production" masing-masing diuji sampai benar-benar menolak:
  tanpa `--yes-development`, `NODE_ENV=production`, dan host database di luar daftar dev.

## Bentrok port 5432 (temuan penting untuk laptop ini)

Laptop pengembangan ini punya **PostgreSQL 17 native Windows** yang juga mendengarkan di port
5432, bentrok dengan pemetaan port Docker (`netstat` menunjukkan dua proses: `com.docker.backend`
dan `postgres.exe` sama-sama `LISTENING` di 5432). Percobaan pertama gagal dengan
`password authentication failed` — bukan karena kredensial salah, tapi koneksi ditangkap Postgres
native yang kredensialnya berbeda.

**Keputusan (18 Sep 2026):** `docker-compose.yml` diubah memetakan Postgres ke **port host 5433**
(`5433:5432`), bukan menyentuh instalasi Postgres native Anda. Kalau menjalankan skrip lain yang
menyambung ke database dev, pakai `postgresql://sigap_app:sigap_password@localhost:5433/sigap_dev`
— **bukan port 5432**. Ini hanya soal port di *host* Windows; jaringan antar-container Docker (mis.
nanti `sigap-api` memanggil `postgres:5432`) tidak terpengaruh.

## Batas & asumsi

- Skema kolom `KantorBmn` mengikuti model Prisma prototipe apa adanya (skema-first), **kecuali**
  `isKoordinatDummy` yang baru. Lihat `schema.sql` untuk DDL lengkap dan alasan `unitId` tanpa FK.
- Path sumber default: `C:\dev\MKB APPS\App\data\kantor-bmn.json` — salinan lokal yang sama
  dipakai `[path file salinan SIMAN]` di PLAYBOOK P3.5 (dicatat di MIGRATION_NOTES bagian 5.1).
  Sudah berupa JSON hasil ekstraksi xlsx, bukan xlsx mentah.
- Saat koordinat asli dari SIMAN tersedia (PLAYBOOK Lampiran D urutan #6): jalankan ulang seeder
  dengan sumber yang sudah diisi `lintang`/`bujur` asli — baris itu otomatis `isKoordinatDummy =
  false` dan tidak digenerate ulang (lihat `generateKoordinat` di `generate.mjs`).
