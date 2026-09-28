// Membangun image sigap-api dan sigap-web, menjalankannya di container Linux, dan melaporkan yang gagal.
//
//   node scripts/verifikasi-container.mjs [api|web|semua]     (atau: npm run verifikasi:container)
//
// Beda dengan scripts/verifikasi-linux.mjs (menjalankan pipeline CI: lint, tes, build di container),
// skrip ini memeriksa bahwa hasilnya BENAR-BENAR BERJALAN sebagai image: API hidup dan terhubung ke
// database, remote web disajikan lengkap dengan manifest federasi (PLAYBOOK P4.8).
//
// Konteks build = snapshot git (berkas yang dilacak, akhir baris LF), bukan folder kerja Windows, jadi
// hasilnya sama dengan checkout di CI Linux. sigap-api dibangun dengan target `dev` (image sigap-dev); target
// `production` harus GAGAL dengan SIGAP001 selama iam-dummy ter-resolve (aturan dummy #4).
//
// Bila build gagal, skrip menjalankan pemeriksa lintas platform dan mencocokkan log dengan tiga penyebab
// khas (kapitalisasi nama berkas/impor, CRLF di skrip, path ber-backslash) dan melaporkan temuannya.

import { spawn, spawnSync } from 'node:child_process';
import { AKAR, buatSnapshot, docker, dockerBerjalan, git, mulaiPostgres } from './lib/wadah.mjs';

const ID = process.pid;
const TARGET = {
  api: { dockerfile: 'apps/sigap-api/Dockerfile', image: 'sigap-api:verif', target: 'dev' },
  web: { dockerfile: 'apps/sigap-web/Dockerfile', image: 'sigap-web:verif' },
};

// ── Diagnosis kegagalan build ────────────────────────────────────────────────────────────

/** Tiga penyebab khas, diperiksa berurutan seperti diminta PLAYBOOK P4.8. */
const PENYEBAB = [
  {
    nama: '1. Kapitalisasi nama berkas atau impor tidak cocok',
    pola: /TS2307|TS1149|TS1261|NG\d+.*(?:not found|Could not resolve)|Could not resolve ["']\.{1,2}\/|Cannot find module|differs from already included file name|error CS0246|Could not find (?:a part of )?the path|could not be found/i,
    saran:
      'Samakan huruf besar/kecil nama berkas dengan impornya; ganti nama berkas lewat `git mv`.',
  },
  {
    nama: '2. Akhir baris CRLF pada berkas skrip',
    // eslint-disable-next-line no-control-regex
    pola: /bad interpreter|\r\n?: not found|\^M|\$'\r'|\\r: command not found|unexpected end of file/i,
    saran:
      'Ubah ke LF (.gitattributes menormalkan `* text=auto eol=lf`); jangan menyunting .gitattributes.',
  },
  {
    nama: '3. Path memakai backslash',
    pola: /No such file or directory.*\\|ENOENT.*\\|Could not find.*\\|cannot find the (?:path|file).*\\/i,
    saran:
      'Pakai garis miring `/` di tsconfig, angular.json, .csproj, dan skrip; di C# pakai Path.Combine.',
  },
];

function laporkanGagalBuild(nama, log) {
  console.log(`\n✖ Build ${nama} gagal. 30 baris terakhir:\n`);
  console.log(
    log
      .trimEnd()
      .split('\n')
      .slice(-30)
      .map((b) => '   ' + b)
      .join('\n'),
  );

  console.log('\n── Diagnosis (periksa berurutan) ──');
  let ada = false;
  for (const p of PENYEBAB) {
    const cocok = log.split('\n').filter((b) => p.pola.test(b));
    if (cocok.length > 0) {
      ada = true;
      console.log(`\n⚠ ${p.nama}\n   Saran: ${p.saran}`);
      for (const b of cocok.slice(0, 5)) console.log('   > ' + b.trim().slice(0, 200));
    }
  }
  if (!ada) console.log('   Log tidak cocok dengan tiga penyebab khas; baca 30 baris di atas.');

  // Pemeriksa yang sama dengan CI: menyebut berkas dan baris yang perlu diperbaiki.
  console.log('\n── Pemeriksa lintas platform (scripts/periksa-repo.mjs) ──');
  const periksa = spawnSync(process.execPath, ['scripts/periksa-repo.mjs'], {
    cwd: AKAR,
    encoding: 'utf8',
  });
  console.log((periksa.stdout + periksa.stderr).trim() || '(tanpa keluaran)');
}

// ── Build ────────────────────────────────────────────────────────────────────────────────

/** Membangun image dari arsip snapshot lewat stdin; -f menunjuk Dockerfile DI DALAM arsip. */
function bangun(tree, { dockerfile, image, target }) {
  return new Promise((selesai) => {
    const arsip = spawn('git', ['archive', tree], {
      cwd: AKAR,
      stdio: ['ignore', 'pipe', 'inherit'],
    });
    const build = spawn(
      'docker',
      [
        'build',
        '--progress=plain',
        '-f',
        dockerfile,
        ...(target ? ['--target', target] : []),
        '-t',
        image,
        '-',
      ],
      { stdio: ['pipe', 'pipe', 'pipe'] },
    );
    let log = '';
    build.stdout.on('data', (d) => (log += d));
    build.stderr.on('data', (d) => (log += d));
    arsip.stdout.pipe(build.stdin);
    build.stdin.on('error', () => {});
    build.on('close', (kode) => selesai({ kode: kode ?? 1, log }));
  });
}

// ── Menjalankan dan memeriksa ────────────────────────────────────────────────────────────

const tidur = (ms) => new Promise((r) => setTimeout(r, ms));

function portHost(wadah) {
  const keluaran = docker(['port', wadah, '8080/tcp']).stdout.trim().split('\n')[0] ?? '';
  const cocok = /:(\d+)$/.exec(keluaran);
  if (!cocok) throw new Error(`Port container ${wadah} tidak ditemukan: "${keluaran}"`);
  return cocok[1];
}

/** Menunggu sampai `uji` mengembalikan galat-kosong, atau waktu habis. */
async function tunggu(uji, detik) {
  let terakhir = 'belum dicoba';
  for (let i = 0; i < detik; i++) {
    try {
      const galat = await uji();
      if (!galat) return null;
      terakhir = galat;
    } catch (e) {
      terakhir = e instanceof Error ? e.message : String(e);
    }
    await tidur(1000);
  }
  return terakhir;
}

async function ambil(url) {
  const r = await fetch(url, { signal: AbortSignal.timeout(5000) });
  return {
    status: r.status,
    tipe: r.headers.get('content-type') ?? '',
    cors: r.headers.get('access-control-allow-origin'),
    teks: await r.text(),
  };
}

async function periksaApi(hasil, jaringan, pg) {
  const nama = `sigap-verif-api-${ID}`;
  const mulai = docker([
    'run',
    '-d',
    '--rm',
    '--name',
    nama,
    '--network',
    jaringan,
    '-p',
    '127.0.0.1::8080',
    // Tanpa nilai rahasia sungguhan: kredensial DB adalah milik container PostgreSQL sementara.
    '-e',
    `ConnectionStrings__Sigap=Host=${pg};Port=5432;Database=sigap_ci;Username=sigap_app;Password=sigap_password`,
    '-e',
    'Lampiran__Folder=/tmp/lampiran',
    // Authority hanya dibaca saat token pertama diperiksa; kedua endpoint health anonim.
    '-e',
    'Iam__Authority=http://keycloak.invalid/realms/kemenkeu',
    '-e',
    'Iam__RequireHttpsMetadata=false',
    '-e',
    'ASPNETCORE_ENVIRONMENT=Production',
    TARGET.api.image,
  ]);
  if (mulai.status !== 0) {
    hasil.push(['api: container berjalan', false, mulai.stderr.trim()]);
    return nama;
  }

  const port = portHost(nama);
  const dasar = `http://127.0.0.1:${port}`;

  const hidup = await tunggu(async () => {
    const r = await ambil(`${dasar}/health/live`);
    return r.status === 200 ? null : `status ${r.status}`;
  }, 40);
  hasil.push(['api: /health/live 200', hidup === null, hidup ?? '']);

  if (hidup === null) {
    const siap = await tunggu(async () => {
      const r = await ambil(`${dasar}/health/ready`);
      return r.status === 200 ? null : `status ${r.status}: ${r.teks.slice(0, 120)}`;
    }, 15);
    hasil.push(['api: /health/ready 200 (database terjangkau)', siap === null, siap ?? '']);

    // Endpoint bisnis tanpa token harus ditolak 401 — bukan 404 (rute hilang) atau 500.
    const tanpaToken = await ambil(`${dasar}/api/v1/laporan-bencana`);
    hasil.push([
      'api: endpoint bisnis tanpa token dijawab 401',
      tanpaToken.status === 401,
      tanpaToken.status === 401 ? '' : `status ${tanpaToken.status}`,
    ]);
  } else {
    console.log(
      `\n── log container ${nama} ──\n${docker(['logs', '--tail', '40', nama]).stdout}${docker(['logs', '--tail', '40', nama]).stderr}`,
    );
  }
  return nama;
}

async function periksaWeb(hasil) {
  // Tanpa API_BASE_URL container wajib menolak mulai, bukan menyajikan aplikasi yang memanggil localhost.
  const tanpaAlamat = docker(['run', '--rm', TARGET.web.image]);
  hasil.push([
    'web: tanpa API_BASE_URL container menolak mulai',
    tanpaAlamat.status !== 0 && /API_BASE_URL wajib/.test(tanpaAlamat.stderr + tanpaAlamat.stdout),
    `exit ${tanpaAlamat.status}`,
  ]);

  const nama = `sigap-verif-web-${ID}`;
  const alamatApi = 'https://api.sigap.invalid';
  const mulai = docker([
    'run',
    '-d',
    '--rm',
    '--name',
    nama,
    '-p',
    '127.0.0.1::8080',
    '-e',
    `API_BASE_URL=${alamatApi}`,
    TARGET.web.image,
  ]);
  if (mulai.status !== 0) {
    hasil.push(['web: container berjalan', false, mulai.stderr.trim()]);
    return nama;
  }

  const dasar = `http://127.0.0.1:${portHost(nama)}`;
  const sehat = await tunggu(async () => {
    const r = await ambil(`${dasar}/healthz`);
    return r.status === 200 ? null : `status ${r.status}`;
  }, 20);
  hasil.push(['web: /healthz 200', sehat === null, sehat ?? '']);

  const konfigurasi = await ambil(`${dasar}/config.json`);
  hasil.push([
    'web: config.json dari environment',
    konfigurasi.status === 200 && JSON.parse(konfigurasi.teks).apiBaseUrl === alamatApi,
    `status ${konfigurasi.status}`,
  ]);

  const rute = await ambil(`${dasar}/sigap-bencana/masuk`);
  const chunkHilang = await ambil(`${dasar}/chunk-tidak-ada.js`);
  hasil.push([
    'web: rute mode mandiri jatuh ke index.html, chunk yang hilang tetap 404',
    rute.status === 200 && /text\/html/.test(rute.tipe) && chunkHilang.status === 404,
    `rute ${rute.status}, chunk ${chunkHilang.status}`,
  ]);

  let manifest = null;
  const galat = await tunggu(async () => {
    const r = await ambil(`${dasar}/remoteEntry.json`);
    if (r.status !== 200) return `status ${r.status}`;
    manifest = JSON.parse(r.teks);
    return null;
  }, 20);
  hasil.push(['web: /remoteEntry.json 200 dan JSON sah', galat === null, galat ?? '']);
  if (galat !== null) return nama;

  const r = await ambil(`${dasar}/remoteEntry.json`);
  hasil.push([
    'web: remoteEntry.json boleh dimuat lintas origin (CORS)',
    r.cors === '*',
    `Access-Control-Allow-Origin = ${r.cors}`,
  ]);

  const terbuka = manifest.exposes ?? [];
  hasil.push([
    'web: manifest memuat modul yang diekspos',
    terbuka.length > 0,
    `${terbuka.length} modul`,
  ]);
  for (const e of terbuka) {
    const chunk = await ambil(`${dasar}/${e.outFileName}`);
    hasil.push([
      `web: ${e.key} (${e.outFileName}) tersaji sebagai JavaScript`,
      chunk.status === 200 && /javascript/i.test(chunk.tipe),
      `status ${chunk.status}, ${chunk.tipe}`,
    ]);
  }
  return nama;
}

// ── Utama ────────────────────────────────────────────────────────────────────────────────

const pilihan = process.argv[2] ?? 'semua';
const DAFTAR = { api: ['api'], web: ['web'], semua: ['api', 'web'] };
if (!DAFTAR[pilihan]) {
  console.error('Pemakaian: node scripts/verifikasi-container.mjs [api|web|semua]');
  process.exit(2);
}
if (!dockerBerjalan()) {
  console.error(
    'Docker tidak berjalan atau tidak terpasang. Nyalakan Docker Desktop, lalu ulangi.',
  );
  process.exit(2);
}

const tree = buatSnapshot();
console.log(
  `Snapshot ${tree.slice(0, 12)} (${git(['ls-tree', '-r', '--name-only', tree]).split('\n').length} berkas)\n`,
);

const hasil = []; // [nama, lulus, keterangan]
const wadahDibuat = [];
let pg = null;
let gagalBuild = false;

try {
  for (const t of DAFTAR[pilihan]) {
    console.log(`→ build ${TARGET[t].image} (${TARGET[t].dockerfile})`);
    const mulai = Date.now();
    const { kode, log } = await bangun(tree, TARGET[t]);
    const detik = Math.round((Date.now() - mulai) / 1000);
    hasil.push([`build ${TARGET[t].image}`, kode === 0, `${detik} dtk`]);
    if (kode !== 0) {
      gagalBuild = true;
      laporkanGagalBuild(TARGET[t].image, log);
    }
  }

  // Target production sigap-api HARUS gagal karena SIGAP001 selama iam-dummy ter-resolve.
  if (DAFTAR[pilihan].includes('api')) {
    const { kode, log } = await bangun(tree, {
      ...TARGET.api,
      target: 'production',
      image: 'sigap-api:verif-prod',
    });
    hasil.push([
      'api: target production gagal karena SIGAP001 (aturan dummy #4)',
      kode !== 0 && log.includes('SIGAP001'),
      kode === 0 ? 'publish berhasil padahal dummy masih ter-resolve' : '',
    ]);
  }

  if (!gagalBuild) {
    if (DAFTAR[pilihan].includes('api')) pg = await mulaiPostgres('sigap-verif-ctr');
    for (const t of DAFTAR[pilihan]) {
      console.log(`→ jalankan dan periksa ${t}`);
      wadahDibuat.push(
        t === 'api' ? await periksaApi(hasil, pg.jaringan, pg.pg) : await periksaWeb(hasil),
      );
    }
  }
} catch (galat) {
  hasil.push(['skrip verifikasi', false, galat instanceof Error ? galat.message : String(galat)]);
} finally {
  for (const w of wadahDibuat) docker(['rm', '-f', w], { stdio: 'ignore' });
  pg?.bersihkan();
}

console.log('\n━━━ ringkasan ━━━');
for (const [nama, lulus, ket] of hasil)
  console.log(
    `  ${lulus ? '✔' : '✖'} ${nama}${ket && !lulus ? `  — ${ket}` : ket ? `  (${ket})` : ''}`,
  );
const semuaLulus = hasil.every(([, lulus]) => lulus);
console.log(
  semuaLulus ? '\nOK: kedua image berjalan di Linux.' : '\nGAGAL: lihat butir ✖ di atas.',
);
process.exit(semuaLulus ? 0 : 1);
