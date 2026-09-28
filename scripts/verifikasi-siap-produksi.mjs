// Gerbang kesiapan produksi: gagal (exit 1) selama ada satu saja dummy yang masih ter-resolve
// di sisi web ATAU sisi api. Dijalankan MANUAL saat memeriksa progres penukaran dummy — bukan
// bagian dari `check:web` / `verifikasi-linux` harian, karena skrip ini SENGAJA gagal hari ini
// (semua dummy memang masih aktif) dan akan terus gagal sampai penukaran selesai.
//
//   node scripts/verifikasi-siap-produksi.mjs
//
// Sisi web: `compilerOptions.paths` di tsconfig.base.json adalah SATU-SATUNYA tempat yang tahu
// design system/iam-web masih dummy (lihat komentar di berkas itu) — cukup periksa isinya.
//
// Sisi web juga: seam login pengembangan (password grant langsung ke Keycloak lokal, DUMMY_REGISTRY
// butir 51) sengaja masih ikut bundel produksi karena UAT lewat container memakainya. Ia wajib hilang saat
// mekanisme token shell asli dipasang (P7.2), jadi keberadaannya berarti belum siap produksi.
//
// Sisi api: `dotnet publish` Sigap.Api sudah punya penjaga MSBuild (`LarangDummyDiPublish` di
// Sigap.Api.csproj) yang menggagalkan publish dengan kode SIGAP001 selama ada ProjectReference
// dummy yang ter-resolve (iam-dummy hari ini selalu; notifikasi-dummy hanya di konfigurasi
// Debug). Skrip ini menjalankannya dan membaca hasilnya, bukan menduplikasi logikanya.

import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AKAR = join(dirname(fileURLToPath(import.meta.url)), '..');
const CSPROJ_API = join(AKAR, 'apps/sigap-api/src/Sigap.Api/Sigap.Api.csproj');

/**
 * Entri `compilerOptions.paths` di tsconfig.base.json yang masih menunjuk ke `libs/*-dummy*`.
 *
 * @param {object} tsconfigBase  tsconfig.base.json yang sudah di-parse
 * @returns {{ alias: string, target: string }[]}
 */
export function aliasDummyWeb(tsconfigBase) {
  const paths = tsconfigBase.compilerOptions?.paths ?? {};
  const hasil = [];
  for (const [alias, target] of Object.entries(paths)) {
    for (const t of target) {
      if (/dummy/i.test(t)) hasil.push({ alias, target: t });
    }
  }
  return hasil;
}

/**
 * Menafsirkan keluaran `dotnet publish` Sigap.Api.
 *
 * @param {number} kode
 * @param {string} keluaran
 * @returns {{ siap: boolean, dummyTerpasang: string[] | null, galatLain: string | null }}
 *   `dummyTerpasang` terisi kalau publish gagal karena SIGAP001 (dummy terdeteksi — ini kegagalan
 *   yang DIHARAPKAN hari ini). `galatLain` terisi kalau publish gagal karena sebab lain (mis. DLL
 *   API dev masih terkunci, atau kegagalan build sungguhan) — bukan sinyal status dummy.
 */
export function tafsirkanPublishApi(kode, keluaran) {
  if (kode === 0) return { siap: true, dummyTerpasang: null, galatLain: null };
  const m = /SIGAP001.*?—\s*(.+?)\.\s*Ganti ProjectReference/s.exec(keluaran);
  if (m) {
    return {
      siap: false,
      dummyTerpasang: m[1].split(',').map((s) => s.trim()),
      galatLain: null,
    };
  }
  return { siap: false, dummyTerpasang: null, galatLain: keluaran.trim().slice(-2000) };
}

/** Penanda seam login pengembangan di kode sigap-web. */
const PENANDA_SEAM = [
  { pola: /grant_type['"]?\s*[:=,]\s*['"]password['"]/, alasan: 'password grant langsung ke SSO' },
  { pola: /\bKEYCLOAK_DEV_CLIENT_ID\b/, alasan: 'client Keycloak khusus pengembangan' },
];

/**
 * Berkas sigap-web (bukan tes) yang masih memuat seam login pengembangan.
 *
 * @param {{ path: string, teks: string }[]} berkas
 * @returns {{ path: string, alasan: string }[]}
 */
export function seamLoginDev(berkas) {
  const hasil = [];
  for (const { path, teks } of berkas) {
    if (/\.spec\.ts$/.test(path)) continue;
    for (const { pola, alasan } of PENANDA_SEAM) {
      if (pola.test(teks)) hasil.push({ path, alasan });
    }
  }
  return hasil;
}

function berkasWeb() {
  return execFileSync('git', ['ls-files', '-z', 'apps/sigap-web/src'], {
    cwd: AKAR,
    encoding: 'utf8',
  })
    .split('\0')
    .filter((p) => p.endsWith('.ts'))
    .map((path) => ({ path, teks: readFileSync(join(AKAR, path), 'utf8') }));
}

function jalankanPublish() {
  const out = mkdtempSync(join(tmpdir(), 'sigap-siap-produksi-'));
  try {
    const keluaran = execFileSync('dotnet', ['publish', CSPROJ_API, '-c', 'Release', '-o', out], {
      cwd: AKAR,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    return { kode: 0, keluaran };
  } catch (e) {
    return { kode: e.status ?? 1, keluaran: `${e.stdout ?? ''}${e.stderr ?? ''}` };
  } finally {
    rmSync(out, { recursive: true, force: true });
  }
}

function main() {
  let siap = true;

  console.log('━━━ Web: alias tsconfig.base.json ━━━');
  const tsconfigBase = JSON.parse(readFileSync(join(AKAR, 'tsconfig.base.json'), 'utf8'));
  const aliasWeb = aliasDummyWeb(tsconfigBase);
  if (aliasWeb.length === 0) {
    console.log('✔ Tidak ada alias yang menunjuk ke dummy.');
  } else {
    siap = false;
    for (const a of aliasWeb) console.log(`✖ "${a.alias}" → "${a.target}"`);
    console.log('  Kunci penukaran ada di komentar tsconfig.base.json.');
  }

  console.log('\n━━━ Web: seam login pengembangan ━━━');
  const seam = seamLoginDev(berkasWeb());
  if (seam.length === 0) {
    console.log('✔ Tidak ada seam login pengembangan.');
  } else {
    siap = false;
    for (const s of seam) console.log(`✖ ${s.path}: ${s.alasan}`);
    console.log('  Ganti dengan mekanisme token shell asli (P7.2, DUMMY_REGISTRY butir 51).');
  }

  console.log('\n━━━ Api: dotnet publish Sigap.Api (Release) ━━━');
  const { kode, keluaran } = jalankanPublish();
  const tafsir = tafsirkanPublishApi(kode, keluaran);
  if (tafsir.siap) {
    console.log('✔ Publish berhasil, tidak ada dummy yang ter-resolve.');
  } else if (tafsir.dummyTerpasang) {
    siap = false;
    for (const d of tafsir.dummyTerpasang) console.log(`✖ ${d}`);
    console.log('  Lihat DUMMY_REGISTRY.md bagian 7, "Urutan penukaran".');
  } else {
    siap = false;
    console.log(
      '⚠ Publish gagal karena sebab lain (bukan penjaga dummy) — tidak bisa disimpulkan:',
    );
    console.log(tafsir.galatLain);
    console.log(
      '  Periksa apakah proses "dotnet run" (API dev, port 5299) masih hidup dan mengunci DLL.',
    );
  }

  console.log(
    `\n${siap ? 'SIAP PRODUKSI' : 'BELUM SIAP PRODUKSI'} — jalankan node scripts/status-dummy.mjs untuk detail tiap dummy.`,
  );
  process.exit(siap ? 0 : 1);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === join(process.argv[1])) main();
