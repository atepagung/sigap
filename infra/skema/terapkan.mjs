// Memasang skema SIGAP ke PostgreSQL DEVELOPMENT (docker compose, port host 5433).
//
//   node infra/skema/terapkan.mjs --yes-development
//
// Urutannya: 00 (32 tabel prototipe, apa adanya) → 10 & 11 (dua perubahan yang disetujui).
// Seluruhnya dalam SATU transaksi: gagal di tengah berarti tidak ada yang berubah.
// Aman dijalankan ulang — bagian yang sudah terpasang dilewati.
//
// Tiga penjaga "tidak pernah di production", sama dengan infra/kantor-bmn-seed/seed.mjs:
//   1. NODE_ENV tidak boleh "production".
//   2. Host di DATABASE_URL harus host dev yang dikenal (localhost/docker).
//   3. Flag --yes-development wajib disertakan eksplisit.
// Migrasi di lingkungan lain menunggu jawaban BaTII (PLAYBOOK Lampiran E #14).

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import pg from 'pg';

const DIR = dirname(fileURLToPath(import.meta.url));
const HOST_DEV_DIIZINKAN = new Set(['localhost', '127.0.0.1', 'postgres']);
// Port 5433 — lihat catatan di docker-compose.yml (bentrok dengan Postgres native Windows).
const DATABASE_URL_DEFAULT = 'postgres://sigap_app:sigap_password@localhost:5433/sigap_dev';

/**
 * Satu-satunya tabel yang boleh sudah ada sebelum 32 tabel dipasang: "KantorBmn" dibuat
 * seeder P3.5 lebih dulu, saat tabel "Unit" belum ada. Tabel lain yang sudah ada tanpa
 * "Unit" berarti keadaan setengah jadi, dan skrip berhenti alih-alih menebak.
 */
const BOLEH_SUDAH_ADA = new Set(['KantorBmn']);

function periksaPenjaga(databaseUrl) {
  if (process.env.NODE_ENV === 'production') {
    throw new Error('Ditolak: NODE_ENV=production. Skrip ini hanya untuk database development.');
  }
  const host = new URL(databaseUrl).hostname;
  if (!HOST_DEV_DIIZINKAN.has(host)) {
    throw new Error(
      `Ditolak: host database "${host}" bukan host development yang dikenal ` +
        `(${[...HOST_DEV_DIIZINKAN].join(', ')}).`,
    );
  }
  if (!process.argv.includes('--yes-development')) {
    throw new Error('Ditolak: sertakan flag --yes-development untuk mengonfirmasi target ini database development.');
  }
}

const baca = (nama) => readFileSync(join(DIR, nama), 'utf8');

/** Memecah DDL Prisma menjadi pernyataan. Berkas itu hanya berisi komentar satu baris utuh. */
function pecahPernyataan(sql) {
  return sql
    .split('\n')
    .filter((b) => !b.trimStart().startsWith('--'))
    .join('\n')
    .split(';')
    .map((s) => s.trim())
    .filter(Boolean);
}

async function adaTabel(klien, nama) {
  const { rows } = await klien.query(
    "select 1 from information_schema.tables where table_schema = 'public' and table_name = $1",
    [nama],
  );
  return rows.length > 0;
}

async function adaIndeks(klien, nama) {
  const { rows } = await klien.query("select 1 from pg_indexes where schemaname = 'public' and indexname = $1", [nama]);
  return rows.length > 0;
}

/**
 * "KantorBmn" buatan seeder P3.5 memakai timestamptz, padahal Prisma — dan karena itu seluruh
 * 31 tabel lain — memakai TIMESTAMP(3) tanpa zona waktu berisi waktu UTC. Penyimpangan itu
 * tidak pernah disetujui; komentar seeder sendiri menyatakan tipenya "mengikuti model Prisma".
 * Di sini dikembalikan ke bentuk Prisma tanpa menyentuh isi barisnya: nilai timestamptz
 * dikonversi ke jam UTC, persis konvensi Prisma.
 */
async function luruskanKantorBmn(klien, log) {
  const { rows } = await klien.query(
    `select column_name from information_schema.columns
      where table_name = 'KantorBmn' and data_type = 'timestamp with time zone'`,
  );
  const kolom = rows.map((r) => r.column_name);
  if (kolom.length === 0) return;

  const ubah = kolom.map((k) => `ALTER COLUMN "${k}" TYPE TIMESTAMP(3) USING ("${k}" AT TIME ZONE 'UTC')`);
  if (kolom.includes('createdAt')) ubah.push('ALTER COLUMN "createdAt" SET DEFAULT CURRENT_TIMESTAMP');
  await klien.query(`ALTER TABLE "KantorBmn" ${ubah.join(', ')}`);
  log(`  "KantorBmn": ${kolom.join(', ')} dikembalikan ke TIMESTAMP(3) sesuai Prisma`);
}

async function pasang32Tabel(klien, log) {
  if (await adaTabel(klien, 'Unit')) {
    log('00 32 tabel prototipe — sudah terpasang, dilewati');
    return;
  }

  let dilewati = 0;
  for (const s of pecahPernyataan(baca('00-prototipe-32-tabel.sql'))) {
    const tabel = /^CREATE TABLE "([^"]+)"/.exec(s)?.[1];
    if (tabel && (await adaTabel(klien, tabel))) {
      if (!BOLEH_SUDAH_ADA.has(tabel)) {
        throw new Error(
          `Tabel "${tabel}" sudah ada padahal "Unit" belum — database dalam keadaan setengah jadi. ` +
            'Periksa manual; skrip tidak menebak.',
        );
      }
      dilewati++;
      continue;
    }
    const indeks = /^CREATE (?:UNIQUE )?INDEX "([^"]+)"/.exec(s)?.[1];
    if (indeks && (await adaIndeks(klien, indeks))) {
      dilewati++;
      continue;
    }
    await klien.query(s);
  }

  if (dilewati > 0) {
    log(`00 32 tabel prototipe — terpasang (${dilewati} pernyataan "KantorBmn" dari seeder P3.5 dilewati)`);
    await luruskanKantorBmn(klien, log);
  } else {
    log('00 32 tabel prototipe — terpasang');
  }
}

async function main() {
  const databaseUrl = process.env.DATABASE_URL ?? DATABASE_URL_DEFAULT;
  periksaPenjaga(databaseUrl);

  const klien = new pg.Client({ connectionString: databaseUrl });
  await klien.connect();
  const log = (p) => console.log(p);

  try {
    await klien.query('BEGIN');

    await pasang32Tabel(klien, log);

    await klien.query(baca('10-disetujui-kantorbmn-iskoordinatdummy.sql'));
    log('10 "KantorBmn"."isKoordinatDummy" — terpasang');

    if (await adaTabel(klien, 'BroadcastSasaranUnit')) {
      log('11 "BroadcastSasaranUnit" — sudah terpasang, dilewati');
    } else {
      await klien.query(baca('11-disetujui-broadcast-sasaran-unit.sql'));
      log('11 "BroadcastSasaranUnit" — terpasang');
    }

    await klien.query('COMMIT');

    const { rows } = await klien.query(
      "select count(*)::int as n from information_schema.tables where table_schema = 'public'",
    );
    log(`Selesai: ${rows[0].n} tabel di skema public (harapan: 33).`);
  } catch (e) {
    await klien.query('ROLLBACK');
    throw e;
  } finally {
    await klien.end();
  }
}

main().catch((e) => {
  console.error(e.message);
  process.exit(1);
});
