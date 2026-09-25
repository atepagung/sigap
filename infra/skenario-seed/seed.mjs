// Memuat skenario UAT (gempa Pasaman, KPP Madya Pekanbaru) ke PostgreSQL development supaya
// dashboard punya isi. Prasyarat: infra/organisasi-seed sudah dijalankan.
//
// PENJAGA "TIDAK PERNAH DI PRODUCTION": NODE_ENV bukan production, host DATABASE_URL host dev, dan
// --yes-development wajib. Dijalankan ulang = skenario dibuang lalu dibuat ulang dengan waktu segar.
//
// Pemakaian: node infra/skenario-seed/seed.mjs --yes-development
import pg from 'pg';
import { bangunSkenario, ID, KODE_UNIT_TERDAMPAK, NIP_SATGAS, URUTAN_TABEL } from './bangun.mjs';

const HOST_DEV_DIIZINKAN = new Set(['localhost', '127.0.0.1', 'postgres']);
const DATABASE_URL_DEFAULT = 'postgres://sigap_app:sigap_password@localhost:5433/sigap_dev';

function pastikanDevelopment(databaseUrl) {
  if (process.env.NODE_ENV === 'production') throw new Error('Ditolak: NODE_ENV=production.');
  const host = new URL(databaseUrl).hostname;
  if (!HOST_DEV_DIIZINKAN.has(host)) throw new Error(`Ditolak: host database "${host}" bukan host development yang dikenal.`);
  if (!process.argv.includes('--yes-development')) {
    throw new Error('Ditolak: sertakan --yes-development untuk mengonfirmasi target ini database development.');
  }
}

async function sisipkan(client, tabel, baris) {
  for (const b of baris) {
    const kolom = Object.keys(b);
    await client.query(
      `INSERT INTO "${tabel}" (${kolom.map((k) => `"${k}"`).join(',')}) VALUES (${kolom.map((_, i) => `$${i + 1}`).join(',')})`,
      // Kolom TIMESTAMP(3) tanpa zona menyimpan UTC; pg akan mengirim Date dalam zona lokal, jadi kirim ISO UTC.
      kolom.map((k) => (b[k] instanceof Date ? b[k].toISOString() : b[k])),
    );
  }
}

async function main() {
  const databaseUrl = process.env.DATABASE_URL ?? DATABASE_URL_DEFAULT;
  pastikanDevelopment(databaseUrl);

  const client = new pg.Client({ connectionString: databaseUrl });
  await client.connect();
  try {
    const unit = (await client.query('SELECT "id" FROM "Unit" WHERE "kode"=$1', [KODE_UNIT_TERDAMPAK])).rows[0];
    const satgas = (await client.query('SELECT "id" FROM "User" WHERE "nip"=$1', [NIP_SATGAS])).rows[0];
    if (!unit || !satgas) throw new Error('Unit/akun uji belum ada. Jalankan dulu: node infra/organisasi-seed/seed.mjs --yes-development');
    const pegawai = (await client.query(
      `SELECT u."id" FROM "User" u JOIN "UserRole" r ON r."userId"=u."id"
       WHERE u."unitId"=$1 AND u."aktif" AND r."role"='PEGAWAI' ORDER BY u."nip"`, [unit.id])).rows.map((r) => r.id);

    const skenario = bangunSkenario({ sekarang: new Date(), unitId: unit.id, satgasId: satgas.id, pegawaiIds: pegawai });

    await client.query('BEGIN');
    // Buang skenario lama beserta deklarasi yang dibuat lewat API untuk unit ini (id acak, bukan skenario-*),
    // kalau tidak persetujuan lama akan "menempel" pada seri asesmen yang baru.
    await client.query('DELETE FROM "SafetyCheckResponse" WHERE "broadcastId" = $1', [ID.broadcast]);
    await client.query('DELETE FROM "BroadcastSasaranUnit" WHERE "broadcastId" = $1', [ID.broadcast]);
    await client.query('DELETE FROM "GangguanLayanan" WHERE "id" LIKE \'skenario-%\'');
    await client.query('DELETE FROM "LayananKritis" WHERE "id" LIKE \'skenario-%\'');
    await client.query('DELETE FROM "DisasterDeclaration" WHERE "unitId" = $1', [unit.id]);
    await client.query('DELETE FROM "DamageAssessment" WHERE "unitId" = $1', [unit.id]);
    await client.query('DELETE FROM "ChecklistKondisiLapangan" WHERE "unitId" = $1', [unit.id]);
    await client.query('DELETE FROM "ActiveBroadcast" WHERE "id" = $1', [ID.broadcast]);
    await client.query(`DELETE FROM "JejakPerubahan" WHERE "id" LIKE 'skenario-%'`);

    for (const tabel of URUTAN_TABEL) await sisipkan(client, tabel, skenario[tabel]);
    await client.query('COMMIT');

    const n = async (sql) => (await client.query(sql)).rows[0].n;
    console.log(
      `Selesai. Broadcast: ${await n('SELECT count(*)::int n FROM "ActiveBroadcast"')}, ` +
        `jawaban safety check: ${await n('SELECT count(*)::int n FROM "SafetyCheckResponse"')} ` +
        `(pegawai di unit: ${pegawai.length}), versi asesmen: ${await n('SELECT count(*)::int n FROM "DamageAssessment"')}, ` +
        `layanan kritis: ${await n('SELECT count(*)::int n FROM "LayananKritis"')}.`,
    );
  } catch (err) {
    await client.query('ROLLBACK').catch(() => {});
    throw err;
  } finally {
    await client.end();
  }
}

await main();
