-- KantorBmn — bangunan gedung kantor permanen milik Kementerian Keuangan.
-- Kolom & tipe mengikuti model Prisma "KantorBmn" di prototipe
-- (C:\dev\MKB APPS\App\prisma\schema.prisma) — SKEMA INI TIDAK BOLEH DIUBAH sebagai bagian
-- dari kesepakatan struktur tabel, KECUALI kolom "isKoordinatDummy" di bawah, yang sudah
-- disetujui pemilik proyek (18 Sep 2026, P3.5) sebagai penanda koordinat hasil generate.
--
-- Sejak P4.2 (21 Sep 2026) skema penuh dipasang lebih dulu oleh infra/skema/terapkan.mjs, dan
-- berkas ini praktis tidak berbuat apa-apa (IF NOT EXISTS). Ia dipertahankan hanya supaya
-- seeder tetap dapat dijalankan sendirian. Foreign key "unitId" → "Unit" dipasang oleh
-- infra/skema, bukan di sini, karena di sini "Unit" belum tentu ada.
--
-- [DIPERBAIKI 21 Sep 2026, P4.2] Kolom waktu sebelumnya ditulis timestamptz, menyimpang dari
-- Prisma (TIMESTAMP(3) tanpa zona waktu, berisi waktu UTC) padahal komentar di atas menyatakan
-- tipenya mengikuti Prisma. Penyimpangan itu tidak pernah disetujui, dan kini diluruskan.
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
  "ditarikPada"        timestamp(3) NOT NULL,
  "createdAt"          timestamp(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

  -- [DISETUJUI 18 Sep 2026] Penanda koordinat hasil generate seeder (belum tersedia dari
  -- SIMAN), BUKAN bagian skema Prisma prototipe. Lihat README.md di folder ini.
  "isKoordinatDummy"   boolean NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS "KantorBmn_eselon1_idx"  ON "KantorBmn" ("eselon1");
CREATE INDEX IF NOT EXISTS "KantorBmn_provinsi_idx" ON "KantorBmn" ("provinsi");
CREATE INDEX IF NOT EXISTS "KantorBmn_kondisi_idx"  ON "KantorBmn" ("kondisi");
