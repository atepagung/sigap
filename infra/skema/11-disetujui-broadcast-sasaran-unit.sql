-- ═══════════════════════════════════════════════════════════════════════════════════════
-- TABEL KE-33 "BroadcastSasaranUnit" — PERUBAHAN SKEMA YANG DISETUJUI.
--
-- Disetujui pemilik proyek 18 Sep 2026 sebagai satu-satunya pengecualian atas aturan
-- "struktur 32 tabel tidak berubah". Isi di bawah disalin VERBATIM dari API_CONTRACT.md
-- bagian 5 — itulah spesifikasinya; ubah di sana lebih dulu, bukan di sini.
--
-- Migrasi di lingkungan selain dev perlu dikoordinasikan dengan BaTII
-- (PLAYBOOK Lampiran E #14: siapa menjalankan migrasi skema).
-- ═══════════════════════════════════════════════════════════════════════════════════════

CREATE TYPE "StatusSasaran" AS ENUM ('DISASAR', 'DILEWATI');

CREATE TABLE "BroadcastSasaranUnit" (
  "id"                        TEXT         PRIMARY KEY,
  "broadcastId"               TEXT         NOT NULL REFERENCES "ActiveBroadcast"("id"),
  "unitId"                    TEXT         NOT NULL REFERENCES "Unit"("id"),
  "jenisBencana"              TEXT         NOT NULL,  -- salinan dari broadcast, untuk indeks unik
  "status"                    "StatusSasaran" NOT NULL,
  "dilewatiKarenaBroadcastId" TEXT         REFERENCES "ActiveBroadcast"("id"),
  "aktif"                     BOOLEAN      NOT NULL DEFAULT TRUE,  -- false saat broadcast selesai
  "isDemo"                    BOOLEAN      NOT NULL DEFAULT FALSE, -- konsisten dengan 32 tabel lain
  "createdAt"                 TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "sasaran_unik_per_broadcast" UNIQUE ("broadcastId", "unitId"),
  CONSTRAINT "sasaran_dilewati_wajib_pemegang"
    CHECK (("status" = 'DILEWATI') = ("dilewatiKarenaBroadcastId" IS NOT NULL))
);

-- Menegakkan "satu pemegang per (unit, jenis bencana)" di tingkat database,
-- sehingga dua trigger yang ditekan bersamaan tidak dapat memegang unit yang sama.
CREATE UNIQUE INDEX "sasaran_satu_pemegang_aktif"
  ON "BroadcastSasaranUnit" ("unitId", "jenisBencana")
  WHERE "status" = 'DISASAR' AND "aktif";

CREATE INDEX "sasaran_unit_aktif" ON "BroadcastSasaranUnit" ("unitId") WHERE "aktif";
