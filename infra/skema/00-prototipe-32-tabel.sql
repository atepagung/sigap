-- ═══════════════════════════════════════════════════════════════════════════════════════
-- 32 TABEL PROTOTIPE — SNAPSHOT APA ADANYA. JANGAN DISUNTING TANGAN.
--
-- Sumber : C:\dev\MKB APPS\App\prisma\schema.prisma (repo atepagung/sigap-prototipe),
--          commit terakhir berkas itu 6978e71 (7 Sep 2026); baseline porting 1b1487a.
-- Dibuat : npx prisma migrate diff --from-empty --to-schema-datamodel prisma/schema.prisma --script
--          (Prisma 5.22.0, dijalankan 21 Sep 2026, tanpa koneksi database)
--
-- Prototipe tidak punya folder migrasi — skemanya dipasang dengan `prisma db push`. Karena itu
-- DDL inilah bentuk fisik yang sesungguhnya dari 32 tabel itu, dan menjadi acuan
-- schema-first bagi EF Core di sigap-api (P4.2). Tes SkemaTests membaca berkas ini langsung
-- dan menggagalkan build bila model EF menyimpang darinya.
--
-- Struktur 32 tabel ini TIDAK BERUBAH (aturan mutlak proyek no. 5). Perubahan yang sudah
-- disetujui pemilik proyek ada di berkas terpisah (10-*, 11-*), bukan disisipkan ke sini.
--
-- Tidak idempoten. Pasang ke database dev hanya lewat terapkan.mjs.
-- ═══════════════════════════════════════════════════════════════════════════════════════

-- CreateEnum
CREATE TYPE "TingkatUnit" AS ENUM ('KEMENTERIAN', 'ESELON_I', 'STAF_AHLI', 'ESELON_II', 'ESELON_III', 'ESELON_IV', 'INSTANSI_VERTIKAL', 'UPT', 'NON_ESELON');

-- CreateEnum
CREATE TYPE "RoleKey" AS ENUM ('PEGAWAI', 'PIMPINAN', 'SATGAS', 'PENGEMBANG', 'IMPL_RKB', 'SUBKOORDINATOR', 'KOORDINATOR', 'PERWAKILAN', 'SEKJEN', 'ADMIN');

-- CreateEnum
CREATE TYPE "DocCode" AS ENUM ('ARKB', 'ADB', 'SKB', 'RTDB', 'RKBU', 'RPKK', 'LPKB');

-- CreateEnum
CREATE TYPE "DocStatus" AS ENUM ('BELUM', 'MENUNGGU', 'LENGKAP');

-- CreateEnum
CREATE TYPE "SafetyStatus" AS ENUM ('AMAN', 'BUTUH_BANTUAN');

-- CreateEnum
CREATE TYPE "AlertStatus" AS ENUM ('MENUNGGU', 'TERVERIFIKASI', 'DITOLAK');

-- CreateEnum
CREATE TYPE "DeklarasiStatus" AS ENUM ('NORMAL', 'DARURAT', 'PULIH');

-- CreateEnum
CREATE TYPE "BroadcastStatus" AS ENUM ('MENUNGGU', 'APPROVED', 'DITOLAK');

-- CreateEnum
CREATE TYPE "LpkbStatus" AS ENUM ('DRAFT', 'MENUNGGU_REVIEW', 'DIREVIEW', 'FINAL');

-- CreateEnum
CREATE TYPE "AttachmentType" AS ENUM ('FOTO', 'VIDEO', 'AUDIO', 'DOKUMEN');

-- CreateEnum
CREATE TYPE "StatusGangguan" AS ENUM ('TERGANGGU', 'BERHENTI_TOTAL', 'PULIH');

-- CreateTable
CREATE TABLE "Unit" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "nama" TEXT NOT NULL,
    "kode" TEXT,
    "tipe" TEXT NOT NULL,
    "tingkat" "TingkatUnit" NOT NULL DEFAULT 'INSTANSI_VERTIKAL',
    "eselonIKey" TEXT,
    "provinsi" TEXT,
    "kabkota" TEXT,
    "kodeKabkota" TEXT,
    "parentUnitId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,
    "lintang" DOUBLE PRECISION,
    "bujur" DOUBLE PRECISION,

    CONSTRAINT "Unit_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "User" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "nip" TEXT NOT NULL,
    "nama" TEXT NOT NULL,
    "email" TEXT,
    "jabatan" TEXT,
    "aktif" BOOLEAN NOT NULL DEFAULT true,
    "passwordHash" TEXT,
    "unitId" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "UserRole" (
    "id" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "role" "RoleKey" NOT NULL,

    CONSTRAINT "UserRole_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "MkbDocument" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "kode" "DocCode" NOT NULL,
    "status" "DocStatus" NOT NULL DEFAULT 'BELUM',
    "versi" TEXT,
    "isi" JSONB,
    "catatanRevisi" TEXT,
    "submittedById" TEXT,
    "approvedById" TEXT,
    "submittedAt" TIMESTAMP(3),
    "approvedAt" TIMESTAMP(3),
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "MkbDocument_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "SafetyCheckResponse" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "userId" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "status" "SafetyStatus" NOT NULL,
    "lat" DOUBLE PRECISION,
    "lng" DOUBLE PRECISION,
    "kehadiran" TEXT,
    "keterangan" TEXT,
    "broadcastId" TEXT,
    "dicatatOlehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "SafetyCheckResponse_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "DisasterAlert" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "pelaporId" TEXT NOT NULL,
    "jenisBencana" TEXT NOT NULL,
    "kategoriBencana" TEXT,
    "level" TEXT NOT NULL,
    "lokasi" TEXT NOT NULL,
    "deskripsi" TEXT,
    "status" "AlertStatus" NOT NULL DEFAULT 'MENUNGGU',
    "verifikatorId" TEXT,
    "verifiedAt" TIMESTAMP(3),
    "catatanVerifikasi" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "DisasterAlert_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "ChecklistKondisiLapangan" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "submittedById" TEXT NOT NULL,
    "sdmJumlah" TEXT NOT NULL,
    "sdmKorban" TEXT NOT NULL,
    "sdmFisik" TEXT NOT NULL,
    "sdmPsikis" TEXT NOT NULL,
    "sdmCatatan" TEXT,
    "asetGedungKonstruksi" TEXT NOT NULL,
    "asetGedungAkses" TEXT NOT NULL,
    "asetPeralatanKondisi" TEXT NOT NULL,
    "asetPeralatanJumlah" TEXT NOT NULL,
    "asetPerlengkapanKondisi" TEXT NOT NULL,
    "asetPerlengkapanJumlah" TEXT NOT NULL,
    "asetKendaraanLaik" TEXT NOT NULL,
    "asetKendaraanJumlah" TEXT NOT NULL,
    "asetCatatan" TEXT,
    "arsipVital" TEXT NOT NULL,
    "arsipPenting" TEXT NOT NULL,
    "arsipEvakuasi" TEXT NOT NULL,
    "arsipCatatan" TEXT,
    "tikKomputerKondisi" TEXT NOT NULL,
    "tikKomputerJumlah" TEXT NOT NULL,
    "tikJaringanAkses" TEXT NOT NULL,
    "tikJaringanPower" TEXT NOT NULL,
    "tikAplikasiUtama" TEXT NOT NULL,
    "tikCatatan" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "ChecklistKondisiLapangan_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "DamageAssessment" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "submittedById" TEXT NOT NULL,
    "jenisBencana" TEXT NOT NULL,
    "kategoriBencana" TEXT,
    "waktuKejadian" TIMESTAMP(3),
    "kondisiFisik" TEXT NOT NULL,
    "deskripsi" TEXT,
    "catatanPegawai" TEXT,
    "layananTerdampak" JSONB,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "DamageAssessment_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "DisasterDeclaration" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "declaredById" TEXT NOT NULL,
    "jenisBencana" TEXT NOT NULL,
    "kategoriBencana" TEXT,
    "lokasi" TEXT,
    "status" "DeklarasiStatus" NOT NULL DEFAULT 'DARURAT',
    "declaredAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "resolvedAt" TIMESTAMP(3),
    "eskalasiPada" TIMESTAMP(3),
    "eskalasiOlehId" TEXT,
    "alasanEskalasi" TEXT,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "DisasterDeclaration_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "BroadcastRequest" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "requestedByUnitId" TEXT NOT NULL,
    "requestedById" TEXT NOT NULL,
    "scope" TEXT NOT NULL,
    "jenisBencana" TEXT NOT NULL,
    "kategoriBencana" TEXT,
    "pesan" TEXT,
    "targetJenis" TEXT NOT NULL DEFAULT 'NASIONAL',
    "targetWilayah" TEXT,
    "targetUnitId" TEXT,
    "status" "BroadcastStatus" NOT NULL DEFAULT 'MENUNGGU',
    "approvedById" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "BroadcastRequest_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "ActiveBroadcast" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "jenisBencana" TEXT NOT NULL,
    "kategoriBencana" TEXT,
    "lokasi" TEXT NOT NULL,
    "wilayah" TEXT,
    "pesan" TEXT NOT NULL,
    "dikirimOlehId" TEXT NOT NULL,
    "isOverrideNasional" BOOLEAN NOT NULL DEFAULT false,
    "targetJenis" TEXT NOT NULL DEFAULT 'NASIONAL',
    "targetUnitId" TEXT,
    "targetKabkota" TEXT,
    "targetEselonIKey" TEXT,
    "otomatis" BOOLEAN NOT NULL DEFAULT false,
    "sumberKejadian" TEXT,
    "mmiTertinggi" INTEGER,
    "selesaiPada" TIMESTAMP(3),
    "diakhiriOlehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "ActiveBroadcast_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PemulihanLogEntry" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "petugasId" TEXT NOT NULL,
    "aksi" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "PemulihanLogEntry_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LpkbReport" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "isi" JSONB NOT NULL,
    "status" "LpkbStatus" NOT NULL DEFAULT 'DRAFT',
    "submittedById" TEXT NOT NULL,
    "reviewedById" TEXT,
    "catatanReview" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "LpkbReport_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Attachment" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "tipe" "AttachmentType" NOT NULL,
    "storageKey" TEXT NOT NULL,
    "url" TEXT NOT NULL,
    "mimeType" TEXT,
    "ukuranBytes" INTEGER,
    "disasterAlertId" TEXT,
    "checklistId" TEXT,
    "damageAssessmentId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Attachment_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LayananKritis" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "nama" TEXT NOT NULL,
    "kritis" BOOLEAN NOT NULL DEFAULT true,
    "rtoJam" INTEGER NOT NULL,
    "mtpdJam" INTEGER,
    "nilaiHarianRupiah" BIGINT,
    "sistemPengendalian" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,
    "skorDampak" JSONB,
    "totalSkor" INTEGER,
    "unitPelaksana" TEXT,
    "sdmPemulihan" INTEGER,
    "adbOlehId" TEXT,
    "adbPada" TIMESTAMP(3),
    "bentukStrategi" TEXT,
    "sumberDayaUtama" TEXT,
    "jumlahSumberDaya" INTEGER,
    "skbOlehId" TEXT,
    "skbPada" TIMESTAMP(3),
    "picInternal" TEXT,
    "alternateSite" TEXT,

    CONSTRAINT "LayananKritis_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "GangguanLayanan" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "layananId" TEXT NOT NULL,
    "status" "StatusGangguan" NOT NULL DEFAULT 'TERGANGGU',
    "mulai" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "pulihPada" TIMESTAMP(3),
    "keterangan" TEXT,
    "dilaporkanOlehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,
    "diperbaruiPada" TIMESTAMP(3),
    "dibatalkan" BOOLEAN NOT NULL DEFAULT false,
    "alasanBatal" TEXT,
    "dibatalkanPada" TIMESTAMP(3),
    "dibatalkanOlehId" TEXT,

    CONSTRAINT "GangguanLayanan_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "JejakPerubahan" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "entitas" TEXT NOT NULL,
    "entitasId" TEXT NOT NULL,
    "aksi" TEXT NOT NULL,
    "alasan" TEXT,
    "ringkasan" TEXT,
    "olehId" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "JejakPerubahan_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "KantorBmn" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "namaGedung" TEXT,
    "namaSatker" TEXT,
    "kodeSatker" TEXT,
    "eselon1" TEXT,
    "kondisi" TEXT,
    "umurTahun" INTEGER,
    "jumlahLantai" INTEGER,
    "luasBangunan" DOUBLE PRECISION,
    "luasTanah" DOUBLE PRECISION,
    "nilaiBuku" BIGINT,
    "nilaiPerolehan" BIGINT,
    "statusSertifikat" TEXT,
    "statusPenggunaan" TEXT,
    "alamat" TEXT,
    "kelurahan" TEXT,
    "kecamatan" TEXT,
    "kabkota" TEXT,
    "kodeKabkota" TEXT,
    "provinsi" TEXT,
    "provinsiDiturunkan" BOOLEAN NOT NULL DEFAULT false,
    "kodeProvinsi" TEXT,
    "kodePos" TEXT,
    "lintang" DOUBLE PRECISION,
    "bujur" DOUBLE PRECISION,
    "unitId" TEXT,
    "sumber" TEXT NOT NULL,
    "ditarikPada" TIMESTAMP(3) NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "KantorBmn_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LanggananPush" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "endpoint" TEXT NOT NULL,
    "p256dh" TEXT NOT NULL,
    "auth" TEXT NOT NULL,
    "peramban" TEXT,
    "userId" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "dipakaiPada" TIMESTAMP(3),

    CONSTRAINT "LanggananPush_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "KirimanPush" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "kunci" TEXT NOT NULL,
    "userId" TEXT,
    "judul" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "KirimanPush_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "RisikoBencana" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "kategori" TEXT NOT NULL,
    "jenisAncaman" TEXT NOT NULL,
    "kemungkinan" INTEGER NOT NULL,
    "dampak" INTEGER NOT NULL,
    "uraian" TEXT,
    "mitigasiEvakuasi" BOOLEAN NOT NULL DEFAULT false,
    "prosedurUtama" TEXT,
    "titikKumpul" TEXT,
    "picProsedur" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "RisikoBencana_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "AsetKritis" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "risikoId" TEXT NOT NULL,
    "nama" TEXT NOT NULL,
    "jenis" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "AsetKritis_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "GrabListItem" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "urutan" INTEGER NOT NULL DEFAULT 0,
    "barang" TEXT NOT NULL,
    "lokasi" TEXT,
    "pic" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "GrabListItem_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "NomorDarurat" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "institusi" TEXT NOT NULL,
    "nomorUtama" TEXT NOT NULL,
    "nomorCadangan" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "NomorDarurat_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "StandarPengendalian" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "eselonIKey" TEXT NOT NULL,
    "versi" TEXT NOT NULL,
    "berlakuMulai" TIMESTAMP(3) NOT NULL,
    "isi" TEXT NOT NULL,
    "ditetapkanOleh" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "StandarPengendalian_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "TemplatePesanKunci" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "risikoId" TEXT NOT NULL,
    "narasi" TEXT,
    "statusLevel" TEXT,
    "kategoriRisiko" TEXT,
    "informasiAwal" TEXT,
    "dampakBencana" TEXT,
    "tindakLanjut" TEXT,
    "antisipasi" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "TemplatePesanKunci_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "AnggotaCallTree" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "nama" TEXT NOT NULL,
    "jabatan" TEXT,
    "statusTim" TEXT NOT NULL,
    "kontak" TEXT,
    "urutan" INTEGER NOT NULL DEFAULT 0,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "AnggotaCallTree_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "SimulasiDrill" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "jenis" TEXT NOT NULL,
    "jadwalPada" TIMESTAMP(3) NOT NULL,
    "status" TEXT NOT NULL DEFAULT 'DIJADWALKAN',
    "dilaksanakanPada" TIMESTAMP(3),
    "jumlahPeserta" INTEGER,
    "durasiJam" DOUBLE PRECISION,
    "temuan" TEXT,
    "penilaian" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "SimulasiDrill_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "RilisKomunikasi" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "judul" TEXT NOT NULL,
    "isi" TEXT NOT NULL,
    "saluran" TEXT,
    "fase" TEXT,
    "status" TEXT NOT NULL DEFAULT 'MENUNGGU',
    "catatanPimpinan" TEXT,
    "disusunOlehId" TEXT NOT NULL,
    "disetujuiOlehId" TEXT,
    "disetujuiPada" TIMESTAMP(3),
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "RilisKomunikasi_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "StatusAset" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "kategori" TEXT NOT NULL,
    "kondisi" TEXT NOT NULL,
    "keterangan" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "StatusAset_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LangkahEksekusi" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "unitId" TEXT NOT NULL,
    "layananId" TEXT NOT NULL,
    "langkah" TEXT NOT NULL,
    "sumber" TEXT NOT NULL DEFAULT 'MANUAL',
    "selesai" BOOLEAN NOT NULL DEFAULT false,
    "selesaiPada" TIMESTAMP(3),
    "keterangan" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "LangkahEksekusi_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "KejadianManual" (
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "id" TEXT NOT NULL,
    "tahun" TEXT NOT NULL,
    "provinsi" TEXT NOT NULL,
    "jenis" TEXT NOT NULL,
    "jumlah" INTEGER NOT NULL,
    "sumber" TEXT,
    "catatan" TEXT,
    "olehId" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "KejadianManual_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "Unit_kode_key" ON "Unit"("kode");

-- CreateIndex
CREATE UNIQUE INDEX "User_nip_key" ON "User"("nip");

-- CreateIndex
CREATE UNIQUE INDEX "User_email_key" ON "User"("email");

-- CreateIndex
CREATE UNIQUE INDEX "UserRole_userId_role_key" ON "UserRole"("userId", "role");

-- CreateIndex
CREATE UNIQUE INDEX "MkbDocument_unitId_kode_key" ON "MkbDocument"("unitId", "kode");

-- CreateIndex
CREATE UNIQUE INDEX "LayananKritis_unitId_nama_key" ON "LayananKritis"("unitId", "nama");

-- CreateIndex
CREATE INDEX "JejakPerubahan_entitas_entitasId_idx" ON "JejakPerubahan"("entitas", "entitasId");

-- CreateIndex
CREATE INDEX "KantorBmn_eselon1_idx" ON "KantorBmn"("eselon1");

-- CreateIndex
CREATE INDEX "KantorBmn_provinsi_idx" ON "KantorBmn"("provinsi");

-- CreateIndex
CREATE INDEX "KantorBmn_kondisi_idx" ON "KantorBmn"("kondisi");

-- CreateIndex
CREATE UNIQUE INDEX "LanggananPush_endpoint_key" ON "LanggananPush"("endpoint");

-- CreateIndex
CREATE INDEX "LanggananPush_userId_idx" ON "LanggananPush"("userId");

-- CreateIndex
CREATE UNIQUE INDEX "KirimanPush_kunci_key" ON "KirimanPush"("kunci");

-- CreateIndex
CREATE INDEX "KirimanPush_createdAt_idx" ON "KirimanPush"("createdAt");

-- CreateIndex
CREATE UNIQUE INDEX "RisikoBencana_unitId_jenisAncaman_key" ON "RisikoBencana"("unitId", "jenisAncaman");

-- CreateIndex
CREATE UNIQUE INDEX "StandarPengendalian_eselonIKey_versi_key" ON "StandarPengendalian"("eselonIKey", "versi");

-- CreateIndex
CREATE UNIQUE INDEX "TemplatePesanKunci_risikoId_key" ON "TemplatePesanKunci"("risikoId");

-- CreateIndex
CREATE INDEX "KejadianManual_tahun_idx" ON "KejadianManual"("tahun");

-- CreateIndex
CREATE UNIQUE INDEX "KejadianManual_tahun_provinsi_jenis_key" ON "KejadianManual"("tahun", "provinsi", "jenis");

-- AddForeignKey
ALTER TABLE "Unit" ADD CONSTRAINT "Unit_parentUnitId_fkey" FOREIGN KEY ("parentUnitId") REFERENCES "Unit"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "User" ADD CONSTRAINT "User_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "UserRole" ADD CONSTRAINT "UserRole_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MkbDocument" ADD CONSTRAINT "MkbDocument_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MkbDocument" ADD CONSTRAINT "MkbDocument_submittedById_fkey" FOREIGN KEY ("submittedById") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MkbDocument" ADD CONSTRAINT "MkbDocument_approvedById_fkey" FOREIGN KEY ("approvedById") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "SafetyCheckResponse" ADD CONSTRAINT "SafetyCheckResponse_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "SafetyCheckResponse" ADD CONSTRAINT "SafetyCheckResponse_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "SafetyCheckResponse" ADD CONSTRAINT "SafetyCheckResponse_broadcastId_fkey" FOREIGN KEY ("broadcastId") REFERENCES "ActiveBroadcast"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "SafetyCheckResponse" ADD CONSTRAINT "SafetyCheckResponse_dicatatOlehId_fkey" FOREIGN KEY ("dicatatOlehId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DisasterAlert" ADD CONSTRAINT "DisasterAlert_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DisasterAlert" ADD CONSTRAINT "DisasterAlert_pelaporId_fkey" FOREIGN KEY ("pelaporId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DisasterAlert" ADD CONSTRAINT "DisasterAlert_verifikatorId_fkey" FOREIGN KEY ("verifikatorId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ChecklistKondisiLapangan" ADD CONSTRAINT "ChecklistKondisiLapangan_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ChecklistKondisiLapangan" ADD CONSTRAINT "ChecklistKondisiLapangan_submittedById_fkey" FOREIGN KEY ("submittedById") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DamageAssessment" ADD CONSTRAINT "DamageAssessment_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DamageAssessment" ADD CONSTRAINT "DamageAssessment_submittedById_fkey" FOREIGN KEY ("submittedById") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DisasterDeclaration" ADD CONSTRAINT "DisasterDeclaration_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DisasterDeclaration" ADD CONSTRAINT "DisasterDeclaration_declaredById_fkey" FOREIGN KEY ("declaredById") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "DisasterDeclaration" ADD CONSTRAINT "DisasterDeclaration_eskalasiOlehId_fkey" FOREIGN KEY ("eskalasiOlehId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "BroadcastRequest" ADD CONSTRAINT "BroadcastRequest_requestedByUnitId_fkey" FOREIGN KEY ("requestedByUnitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "BroadcastRequest" ADD CONSTRAINT "BroadcastRequest_requestedById_fkey" FOREIGN KEY ("requestedById") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "BroadcastRequest" ADD CONSTRAINT "BroadcastRequest_approvedById_fkey" FOREIGN KEY ("approvedById") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ActiveBroadcast" ADD CONSTRAINT "ActiveBroadcast_dikirimOlehId_fkey" FOREIGN KEY ("dikirimOlehId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PemulihanLogEntry" ADD CONSTRAINT "PemulihanLogEntry_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PemulihanLogEntry" ADD CONSTRAINT "PemulihanLogEntry_petugasId_fkey" FOREIGN KEY ("petugasId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LpkbReport" ADD CONSTRAINT "LpkbReport_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LpkbReport" ADD CONSTRAINT "LpkbReport_submittedById_fkey" FOREIGN KEY ("submittedById") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LpkbReport" ADD CONSTRAINT "LpkbReport_reviewedById_fkey" FOREIGN KEY ("reviewedById") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Attachment" ADD CONSTRAINT "Attachment_disasterAlertId_fkey" FOREIGN KEY ("disasterAlertId") REFERENCES "DisasterAlert"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Attachment" ADD CONSTRAINT "Attachment_checklistId_fkey" FOREIGN KEY ("checklistId") REFERENCES "ChecklistKondisiLapangan"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Attachment" ADD CONSTRAINT "Attachment_damageAssessmentId_fkey" FOREIGN KEY ("damageAssessmentId") REFERENCES "DamageAssessment"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LayananKritis" ADD CONSTRAINT "LayananKritis_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "GangguanLayanan" ADD CONSTRAINT "GangguanLayanan_layananId_fkey" FOREIGN KEY ("layananId") REFERENCES "LayananKritis"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "GangguanLayanan" ADD CONSTRAINT "GangguanLayanan_dilaporkanOlehId_fkey" FOREIGN KEY ("dilaporkanOlehId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "JejakPerubahan" ADD CONSTRAINT "JejakPerubahan_olehId_fkey" FOREIGN KEY ("olehId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "KantorBmn" ADD CONSTRAINT "KantorBmn_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LanggananPush" ADD CONSTRAINT "LanggananPush_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "RisikoBencana" ADD CONSTRAINT "RisikoBencana_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "AsetKritis" ADD CONSTRAINT "AsetKritis_risikoId_fkey" FOREIGN KEY ("risikoId") REFERENCES "RisikoBencana"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "GrabListItem" ADD CONSTRAINT "GrabListItem_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "NomorDarurat" ADD CONSTRAINT "NomorDarurat_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "TemplatePesanKunci" ADD CONSTRAINT "TemplatePesanKunci_risikoId_fkey" FOREIGN KEY ("risikoId") REFERENCES "RisikoBencana"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "AnggotaCallTree" ADD CONSTRAINT "AnggotaCallTree_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "SimulasiDrill" ADD CONSTRAINT "SimulasiDrill_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "RilisKomunikasi" ADD CONSTRAINT "RilisKomunikasi_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "StatusAset" ADD CONSTRAINT "StatusAset_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LangkahEksekusi" ADD CONSTRAINT "LangkahEksekusi_unitId_fkey" FOREIGN KEY ("unitId") REFERENCES "Unit"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LangkahEksekusi" ADD CONSTRAINT "LangkahEksekusi_layananId_fkey" FOREIGN KEY ("layananId") REFERENCES "LayananKritis"("id") ON DELETE CASCADE ON UPDATE CASCADE;

