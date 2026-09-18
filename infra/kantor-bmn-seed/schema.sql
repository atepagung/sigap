-- KantorBmn — bangunan gedung kantor permanen milik Kementerian Keuangan.
-- Kolom & tipe mengikuti model Prisma "KantorBmn" di prototipe
-- (C:\dev\MKB APPS\App\prisma\schema.prisma) — SKEMA INI TIDAK BOLEH DIUBAH sebagai bagian
-- dari kesepakatan struktur tabel, KECUALI kolom "isKoordinatDummy" di bawah, yang sudah
-- disetujui pemilik proyek (18 Sep 2026, P3.5) sebagai penanda koordinat hasil generate.
--
-- unitId SENGAJA tanpa foreign key: tabel "Unit" belum dibuat (menyusul saat EF Core/P4.2
-- membangun skema penuh). Seeder ini hanya mengisi data BMN; menautkan ke unit organisasi
-- di luar cakupan permintaan ini dan dibiarkan NULL untuk semua baris.
--
-- Nama kolom quoted camelCase, konsisten dengan seluruh skema Prisma prototipe (tanpa @map).
CREATE TABLE IF NOT EXISTS "KantorBmn" (
  "id"                 text PRIMARY KEY,
  "isDemo"             boolean NOT NULL DEFAULT false,
  "namaGedung"         text,
  "namaSatker"         text,
  "kodeSatker"         text,
  "eselon1"            text,
  "kondisi"            text,
  "umurTahun"          integer,
  "jumlahLantai"       integer,
  "luasBangunan"       double precision,
  "luasTanah"          double precision,
  "nilaiBuku"          bigint,
  "nilaiPerolehan"     bigint,
  "statusSertifikat"   text,
  "statusPenggunaan"   text,
  "alamat"             text,
  "kelurahan"          text,
  "kecamatan"          text,
  "kabkota"            text,
  "kodeKabkota"        text,
  "provinsi"           text,
  "provinsiDiturunkan" boolean NOT NULL DEFAULT false,
  "kodeProvinsi"       text,
  "kodePos"            text,
  "lintang"            double precision,
  "bujur"              double precision,
  "unitId"             text,
  "sumber"             text NOT NULL,
  "ditarikPada"        timestamptz NOT NULL,
  "createdAt"          timestamptz NOT NULL DEFAULT now(),

  -- [DISETUJUI 18 Sep 2026] Penanda koordinat hasil generate seeder (belum tersedia dari
  -- SIMAN), BUKAN bagian skema Prisma prototipe. Lihat README.md di folder ini.
  "isKoordinatDummy"   boolean NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS "KantorBmn_eselon1_idx"  ON "KantorBmn" ("eselon1");
CREATE INDEX IF NOT EXISTS "KantorBmn_provinsi_idx" ON "KantorBmn" ("provinsi");
CREATE INDEX IF NOT EXISTS "KantorBmn_kondisi_idx"  ON "KantorBmn" ("kondisi");
