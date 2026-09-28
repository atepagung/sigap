// Memuat "Unit", "User", dan "UserRole" ke PostgreSQL development.
//
// PENJAGA "TIDAK PERNAH DI PRODUCTION" (sama dengan kantor-bmn-seed, tiga lapis):
//   1. NODE_ENV tidak boleh "production".
//   2. Host DATABASE_URL harus host development yang dikenal.
//   3. Flag --yes-development wajib.
//
// Pemakaian: node infra/organisasi-seed/seed.mjs --yes-development [--otk=<path otk_bundle.json>]
import { readFileSync } from 'node:fs';
import pg from 'pg';
import { bangunSemua } from './bangun.mjs';

const HOST_DEV_DIIZINKAN = new Set(['localhost', '127.0.0.1', 'postgres']);
const DATABASE_URL_DEFAULT = 'postgres://sigap_app:sigap_password@localhost:5433/sigap_dev';
const OTK_DEFAULT = 'C:/dev/MKB APPS/App/data/otk_bundle.json';
const REALM = new URL('../keycloak/import/kemenkeu-realm.json', import.meta.url);

function pastikanDevelopment(databaseUrl) {
  if (process.env.NODE_ENV === 'production') throw new Error('Ditolak: NODE_ENV=production.');
  const host = new URL(databaseUrl).hostname;
  if (!HOST_DEV_DIIZINKAN.has(host)) {
    throw new Error(`Ditolak: host database "${host}" bukan host development yang dikenal.`);
  }
  if (!process.argv.includes('--yes-development')) {
    throw new Error('Ditolak: sertakan --yes-development untuk mengonfirmasi target ini database development.');
  }
}

function jalurOtk() {
  const arg = process.argv.find((a) => a.startsWith('--otk='));
  return arg ? arg.slice('--otk='.length) : (process.env.OTK_BUNDLE ?? OTK_DEFAULT);
}

async function main() {
  const databaseUrl = process.env.DATABASE_URL ?? DATABASE_URL_DEFAULT;
  pastikanDevelopment(databaseUrl);

  const jalur = jalurOtk();
  let bundle;
  try {
    bundle = JSON.parse(readFileSync(jalur, 'utf8'));
  } catch (err) {
    throw new Error(`Tidak bisa membaca OTK di "${jalur}" (${err.code ?? err.message}). Beri --otk=<path> atau OTK_BUNDLE.`);
  }
  const { unit, pengguna, layanan } = bangunSemua(bundle, JSON.parse(readFileSync(REALM, 'utf8')));

  const client = new pg.Client({ connectionString: databaseUrl });
  await client.connect();
  try {
    await client.query('BEGIN');
    // Tahap 1: unit tanpa induk (induk mungkin belum tersimpan), tahap 2: pasang induk.
    for (const u of unit) {
      await client.query(
        `INSERT INTO "Unit" ("id","isDemo","nama","kode","tipe","tingkat","eselonIKey","provinsi","kabkota","updatedAt")
         VALUES ($1,$2,$3,$4,$5,$6::"TingkatUnit",$7,$8,$9,now())
         ON CONFLICT ("kode") DO UPDATE SET "nama"=EXCLUDED."nama","tipe"=EXCLUDED."tipe",
           "tingkat"=EXCLUDED."tingkat","eselonIKey"=EXCLUDED."eselonIKey","provinsi"=EXCLUDED."provinsi",
           "kabkota"=EXCLUDED."kabkota","isDemo"=EXCLUDED."isDemo","updatedAt"=now()`,
        [u.id, u.isDemo, u.nama, u.kode, u.tipe, u.tingkat, u.eselonIKey, u.provinsi, u.kabkota ?? null],
      );
    }
    for (const u of unit.filter((x) => x.parentUnitId)) {
      await client.query('UPDATE "Unit" SET "parentUnitId"=$2 WHERE "kode"=$1', [u.kode, u.parentUnitId]);
    }
    // passwordHash dan email sengaja tidak diisi: login lewat SSO, dan keduanya tak pernah diproyeksikan.
    for (const p of pengguna) {
      const { rows } = await client.query(
        `INSERT INTO "User" ("id","isDemo","nip","nama","aktif","unitId","updatedAt")
         VALUES ($1,true,$2,$3,true,$4,now())
         ON CONFLICT ("nip") DO UPDATE SET "nama"=EXCLUDED."nama","unitId"=EXCLUDED."unitId","aktif"=true,
           "isDemo"=true,"updatedAt"=now()
         RETURNING "id"`,
        [p.id, p.nip, p.nama, p.unitId],
      );
      for (const peran of p.peran) {
        await client.query(
          `INSERT INTO "UserRole" ("id","userId","role") VALUES ($1,$2,$3::"RoleKey")
           ON CONFLICT ("userId","role") DO NOTHING`,
          [`ur-${p.nip}-${peran}`, rows[0].id, peran],
        );
      }
    }
    // Akun layanan BMKG: tanpa "UserRole", passwordHash, dan email; bukan data demo.
    await client.query(
      `INSERT INTO "User" ("id","isDemo","nip","nama","aktif","unitId","updatedAt")
       VALUES ($1,false,$2,$3,true,$4,now())
       ON CONFLICT ("nip") DO UPDATE SET "nama"=EXCLUDED."nama","unitId"=EXCLUDED."unitId","aktif"=true,"updatedAt"=now()`,
      [layanan.id, layanan.nip, layanan.nama, layanan.unitId],
    );
    await client.query('COMMIT');

    const hitung = async (sql) => (await client.query(sql)).rows[0].n;
    console.log(
      `Selesai. "Unit": ${await hitung('SELECT count(*)::int n FROM "Unit"')} ` +
        `(${await hitung('SELECT count(*)::int n FROM "Unit" WHERE "isDemo"')} demo), ` +
        `"User": ${await hitung('SELECT count(*)::int n FROM "User"')}, ` +
        `"UserRole": ${await hitung('SELECT count(*)::int n FROM "UserRole"')}.`,
    );
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    await client.end();
  }
}

await main();
