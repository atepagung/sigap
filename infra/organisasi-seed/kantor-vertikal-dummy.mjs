// Membuat unit vertikal DUMMY dari "KantorBmn" dan mengisi "KantorBmn"."unitId".
//
// Bawaan DRY-RUN: hanya melaporkan (termasuk satker yang gedungnya tersebar di lebih dari satu kabupaten/kota).
// Menulis hanya dengan --yes-development, dengan penjaga "tidak pernah di production" seperti seed.mjs.
// Jalankan seed.mjs (OTK) dan kantor-bmn-seed lebih dulu. Semua unit hasilnya isDemo = true.
//
// Pemakaian: node infra/organisasi-seed/kantor-vertikal-dummy.mjs [--yes-development]
import pg from 'pg';
import { bangunUnitKantor } from './bangun-kantor.mjs';

const HOST_DEV_DIIZINKAN = new Set(['localhost', '127.0.0.1', 'postgres']);
const DATABASE_URL_DEFAULT = 'postgres://sigap_app:sigap_password@localhost:5433/sigap_dev';

function pastikanDevelopment(databaseUrl) {
  if (process.env.NODE_ENV === 'production') throw new Error('Ditolak: NODE_ENV=production.');
  const host = new URL(databaseUrl).hostname;
  if (!HOST_DEV_DIIZINKAN.has(host)) {
    throw new Error(`Ditolak: host database "${host}" bukan host development yang dikenal.`);
  }
}

async function main() {
  const databaseUrl = process.env.DATABASE_URL ?? DATABASE_URL_DEFAULT;
  pastikanDevelopment(databaseUrl);
  const tulis = process.argv.includes('--yes-development');

  const client = new pg.Client({ connectionString: databaseUrl });
  await client.connect();
  try {
    const { rows: gedung } = await client.query(
      'SELECT "id","kodeSatker","namaSatker","eselon1","provinsi","kabkota" FROM "KantorBmn" ORDER BY "id"',
    );
    const { unit, tautan, laporan } = bangunUnitKantor(gedung);
    const kodeInduk = [...new Set(unit.map((u) => u.parentUnitId))];
    const { rows: induk } = await client.query('SELECT "id" FROM "Unit" WHERE "id" = ANY($1)', [kodeInduk]);
    const adaInduk = new Set(induk.map((r) => r.id));
    const tanpaInduk = kodeInduk.filter((id) => !adaInduk.has(id));
    if (tanpaInduk.length) throw new Error(`Induk OTK belum ada (jalankan seed.mjs dulu): ${tanpaInduk.join(', ')}`);

    console.log(
      `${tulis ? 'TULIS' : 'DRY-RUN'}: ${gedung.length} gedung -> ${unit.length} unit dummy, ${tautan.length} gedung tertaut, ` +
        `${laporan.tanpaData.length} dilewati (kodeSatker/namaSatker/kabkota kosong atau Eselon I tak dikenal).`,
    );
    if (laporan.eselonTakDikenal.length) console.log('Eselon I tak dikenal:', JSON.stringify(laporan.eselonTakDikenal));
    console.log(`${laporan.lebihDariSatuKabkota.length} satker tersebar di lebih dari satu kabupaten/kota (unit memakai yang terbanyak):`);
    for (const s of laporan.lebihDariSatuKabkota.slice(0, 10)) console.log(`  ${s.kodeSatker}: ${s.kabkota.join(' | ')}`);
    if (!tulis) {
      console.log('Tidak ada yang ditulis. Tambahkan --yes-development untuk menulis.');
      return;
    }

    await client.query('BEGIN');
    for (const u of unit) {
      await client.query(
        `INSERT INTO "Unit" ("id","isDemo","nama","kode","tipe","tingkat","eselonIKey","provinsi","kabkota","parentUnitId","updatedAt")
         VALUES ($1,true,$2,$3,$4,$5::"TingkatUnit",$6,$7,$8,$9,now())
         ON CONFLICT ("kode") DO UPDATE SET "nama"=EXCLUDED."nama","tipe"=EXCLUDED."tipe","eselonIKey"=EXCLUDED."eselonIKey",
           "provinsi"=EXCLUDED."provinsi","kabkota"=EXCLUDED."kabkota","parentUnitId"=EXCLUDED."parentUnitId","updatedAt"=now()`,
        [u.id, u.nama, u.kode, u.tipe, u.tingkat, u.eselonIKey, u.provinsi, u.kabkota, u.parentUnitId],
      );
    }
    await client.query(
      `UPDATE "KantorBmn" k SET "unitId" = u."id"
       FROM unnest($1::text[], $2::text[]) AS t("kantorId","unitKode")
       JOIN "Unit" u ON u."kode" = t."unitKode"
       WHERE k."id" = t."kantorId"`,
      [tautan.map((t) => t.kantorId), tautan.map((t) => t.unitKode)],
    );
    await client.query('COMMIT');
    const { rows } = await client.query('SELECT count(*)::int n, count("unitId")::int tertaut FROM "KantorBmn"');
    console.log(`Selesai. "KantorBmn": ${rows[0].n} baris, ${rows[0].tertaut} bertaut ke unit.`);
  } catch (err) {
    await client.query('ROLLBACK').catch(() => {});
    throw err;
  } finally {
    await client.end();
  }
}

await main();
