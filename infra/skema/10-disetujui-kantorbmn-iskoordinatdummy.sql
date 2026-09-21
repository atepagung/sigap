-- ═══════════════════════════════════════════════════════════════════════════════════════
-- "KantorBmn"."isKoordinatDummy" — PERUBAHAN SKEMA YANG DISETUJUI.
--
-- Disetujui pemilik proyek 18 Sep 2026 (P3.5) sebagai penanda koordinat gedung yang
-- digenerate seeder, karena SIMAN belum menyediakan koordinat asli (Lampiran E #16).
-- UI peta WAJIB membaca kolom ini dan menampilkan peringatan selama ada baris bernilai true.
-- Latar lengkap: infra/kantor-bmn-seed/README.md.
-- ═══════════════════════════════════════════════════════════════════════════════════════

ALTER TABLE "KantorBmn" ADD COLUMN IF NOT EXISTS "isKoordinatDummy" BOOLEAN NOT NULL DEFAULT false;
