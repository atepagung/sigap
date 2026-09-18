// Memuat data KantorBmn (dengan koordinat dummy bila perlu) ke PostgreSQL development.
//
// PENJAGA "TIDAK PERNAH DI PRODUCTION" (tiga lapis, semua harus lulus):
//   1. NODE_ENV tidak boleh "production".
//   2. Host di DATABASE_URL harus ada di daftar host dev yang diizinkan (localhost/docker).
//   3. Flag --yes-development wajib disertakan eksplisit di command line.
// Ini seeder standalone (bukan bagian iam-dummy/keu-ui-dummy) karena KantorBmn adalah data
// referensi BMN asli, bukan tiruan platform — lihat README.md di folder ini.
import { readFileSync } from 'node:fs';
import pg from 'pg';
import { main as generate, OUTPUT_DEFAULT } from './generate.mjs';

const HOST_DEV_DIIZINKAN = new Set(['localhost', '127.0.0.1', 'postgres']);
// Port 5433, bukan 5432 standar — lihat catatan di docker-compose.yml (bentrok dengan
// Postgres native Windows di laptop pengembangan).
const DATABASE_URL_DEFAULT = 'postgres://sigap_app:sigap_password@localhost:5433/sigap_dev';

function pastikanEnvironmentDevelopment(databaseUrl) {
  if (process.env.NODE_ENV === 'production') {
    throw new Error('Ditolak: NODE_ENV=production. Seeder ini hanya untuk data development.');
  }
  const host = new URL(databaseUrl).hostname;
  if (!HOST_DEV_DIIZINKAN.has(host)) {
    throw new Error(
      `Ditolak: host database "${host}" bukan host development yang dikenal (${[...HOST_DEV_DIIZINKAN].join(', ')}). ` +
        'Seeder ini tidak pernah dijalankan terhadap database lain.',
    );
  }
  if (!process.argv.includes('--yes-development')) {
    throw new Error('Ditolak: sertakan flag --yes-development untuk mengonfirmasi target ini adalah database development.');
  }
}

async function main() {
  const databaseUrl = process.env.DATABASE_URL ?? DATABASE_URL_DEFAULT;
  pastikanEnvironmentDevelopment(databaseUrl);

  await generate();
  const { gedung } = JSON.parse(readFileSync(OUTPUT_DEFAULT, 'utf8'));

  const client = new pg.Client({ connectionString: databaseUrl });
  await client.connect();
  try {
    await client.query(readFileSync(new URL('./schema.sql', import.meta.url), 'utf8'));

    await client.query('BEGIN');
    for (const g of gedung) {
      await client.query(
        `INSERT INTO "KantorBmn" (
           "id", "isDemo", "namaGedung", "namaSatker", "kodeSatker", "eselon1", "kondisi",
           "umurTahun", "jumlahLantai", "luasBangunan", "luasTanah", "nilaiBuku", "nilaiPerolehan",
           "statusSertifikat", "statusPenggunaan", "alamat", "kelurahan", "kecamatan", "kabkota",
           "kodeKabkota", "provinsi", "provinsiDiturunkan", "kodeProvinsi", "kodePos",
           "lintang", "bujur", "unitId", "sumber", "ditarikPada", "isKoordinatDummy"
         ) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28,$29,$30)
         ON CONFLICT ("id") DO UPDATE SET
           "namaGedung" = EXCLUDED."namaGedung", "namaSatker" = EXCLUDED."namaSatker",
           "kodeSatker" = EXCLUDED."kodeSatker", "eselon1" = EXCLUDED."eselon1",
           "kondisi" = EXCLUDED."kondisi", "umurTahun" = EXCLUDED."umurTahun",
           "jumlahLantai" = EXCLUDED."jumlahLantai", "luasBangunan" = EXCLUDED."luasBangunan",
           "luasTanah" = EXCLUDED."luasTanah", "nilaiBuku" = EXCLUDED."nilaiBuku",
           "nilaiPerolehan" = EXCLUDED."nilaiPerolehan", "statusSertifikat" = EXCLUDED."statusSertifikat",
           "statusPenggunaan" = EXCLUDED."statusPenggunaan", "alamat" = EXCLUDED."alamat",
           "kelurahan" = EXCLUDED."kelurahan", "kecamatan" = EXCLUDED."kecamatan",
           "kabkota" = EXCLUDED."kabkota", "kodeKabkota" = EXCLUDED."kodeKabkota",
           "provinsi" = EXCLUDED."provinsi", "provinsiDiturunkan" = EXCLUDED."provinsiDiturunkan",
           "kodeProvinsi" = EXCLUDED."kodeProvinsi", "kodePos" = EXCLUDED."kodePos",
           "lintang" = EXCLUDED."lintang", "bujur" = EXCLUDED."bujur",
           "sumber" = EXCLUDED."sumber", "ditarikPada" = EXCLUDED."ditarikPada",
           "isKoordinatDummy" = EXCLUDED."isKoordinatDummy"`,
        [
          g.register, false, g.namaGedung, g.namaSatker, g.kodeSatker, g.eselon1, g.kondisi,
          g.umurTahun, g.jumlahLantai, g.luasBangunan, g.luasTanah, g.nilaiBuku, g.nilaiPerolehan,
          g.statusSertifikat, g.statusPenggunaan, g.alamat, g.kelurahan, g.kecamatan, g.kabkota,
          g.kodeKabkota, g.provinsi, g.provinsiDiturunkan, g.kodeProvinsi, g.kodePos,
          g.lintang, g.bujur, null, g.sumber ?? 'Master Aset BMN Kementerian Keuangan', g.ditarikPada ?? new Date().toISOString(),
          g.isKoordinatDummy,
        ],
      );
    }
    await client.query('COMMIT');

    const { rows } = await client.query('SELECT count(*)::int AS total, count(*) FILTER (WHERE "isKoordinatDummy") ::int AS dummy FROM "KantorBmn"');
    console.log(`Selesai. "KantorBmn" berisi ${rows[0].total} baris, ${rows[0].dummy} di antaranya berkoordinat dummy.`);
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    await client.end();
  }
}

await main();
