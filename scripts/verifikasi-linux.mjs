// Menjalankan pipeline CI di container Linux, atas snapshot persis dari apa yang akan di-commit.
//
//   node scripts/verifikasi-linux.mjs web|api|repo|semua
//
// Mengapa ada: development di Windows (tidak membedakan huruf besar/kecil, kadang CRLF),
// production di Linux. Kegagalan khasnya baru muncul di Linux — salah kapitalisasi impor,
// akhir baris, pemisah path. Skrip ini menangkapnya sebelum push (PLAYBOOK P4.8).
//
// Snapshot dibuat lewat index git SEMENTARA (`git add -A` + `git write-tree`), jadi:
//   - perubahan yang belum di-commit ikut diuji;
//   - .gitignore dihormati (node_modules, bin, obj tidak ikut);
//   - .gitattributes menormalkan akhir baris menjadi LF, persis seperti checkout di CI;
//   - index dan folder kerja Anda tidak disentuh.
//
// Butuh Docker. Tidak menyentuh database dev: target `api` memakai PostgreSQL sementara.

import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AKAR = join(dirname(fileURLToPath(import.meta.url)), '..');
const IMG_NODE = 'node:24';
const IMG_DOTNET = 'mcr.microsoft.com/dotnet/sdk:10.0';
const IMG_PG = 'postgres:16';

const SKRIP_WEB = `
set -e
mkdir /w && tar -x -C /w && cd /w
echo "== $(uname -sr) | node $(node -v) =="
echo "== npm ci ==";        npm ci 2>&1 | tail -2
echo "== ESLint ==";        npm run lint --workspace apps/sigap-web
echo "== Stylelint ==";     npm run lint:styles --workspace apps/sigap-web
echo "== Prettier ==";      npm run format:check --workspace apps/sigap-web
echo "== Tes ==";           npm test --workspace apps/sigap-web
echo "== Build produksi =="; npm run build:prod --workspace apps/sigap-web
echo "OK: sigap-web lulus di Linux"
`;

const SKRIP_REPO = `
set -e
mkdir /w && tar -x -C /w && cd /w
git init -q && git config user.email ci@sigap && git config user.name ci && git add -A >/dev/null
echo "== $(uname -sr) | node $(node -v) =="
echo "== tes pemeriksa ==";  node --test "scripts/**/*.spec.mjs" 2>&1 | grep -E "^ℹ (tests|pass|fail)"
echo "== periksa-repo =="; node scripts/periksa-repo.mjs
echo "OK: pemeriksa lintas platform lulus di Linux"
`;

const SKRIP_API = `
set -e
mkdir /w && tar -x -C /w && cd /w
echo "== $(uname -sr) | dotnet $(dotnet --version) =="
echo "== restore ==";       dotnet restore apps/sigap-api/sigap-api.slnx 2>&1 | tail -1
echo "== dotnet format =="; dotnet format apps/sigap-api/sigap-api.slnx --verify-no-changes --no-restore
echo "== build ==";         dotnet build apps/sigap-api/sigap-api.slnx --no-restore 2>&1 | grep -E "Warn|Error\\(s\\)|Build succeeded"
echo "== tes sigap-api =="; dotnet test apps/sigap-api/sigap-api.slnx --no-build 2>&1 | grep -E "Passed!|Failed!|\\[FAIL\\]|\\[SKIP\\]"
echo "== tes library ==";   dotnet test libs/notifikasi/notifikasi.slnx 2>&1 | grep -E "Passed!|Failed!"
                            dotnet test libs/iam-dummy 2>&1 | grep -E "Passed!|Failed!"
echo "== build Release =="; dotnet build apps/sigap-api/sigap-api.slnx -c Release --no-restore 2>&1 | grep -E "Error\\(s\\)|Build succeeded"
echo "== aturan dummy 4: publish HARUS gagal via SIGAP001 =="
set +e; keluaran=$(dotnet publish apps/sigap-api/src/Sigap.Api/Sigap.Api.csproj -c Release --no-restore 2>&1); kode=$?; set -e
[ "$kode" -ne 0 ] || { echo "GAGAL: publish berhasil padahal dummy masih ter-resolve"; exit 1; }
echo "$keluaran" | grep -q SIGAP001 || { echo "$keluaran"; echo "GAGAL: publish gagal bukan karena SIGAP001"; exit 1; }
echo "OK: sigap-api lulus di Linux"
`;

// ── Pembantu ───────────────────────────────────────────────────────────────────────────────

function git(argumen, env = process.env) {
  // stderr ditangkap, bukan dicetak: `git add -A` menulis peringatan "CRLF will be replaced by LF"
  // untuk berkas scaffold di folder kerja Windows. Itu justru yang diharapkan (dinormalkan).
  return execFileSync('git', argumen, {
    cwd: AKAR,
    env,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  }).trim();
}

/** Snapshot dari HEAD + seluruh perubahan folder kerja, tanpa menyentuh index asli. */
function buatSnapshot() {
  const folder = mkdtempSync(join(tmpdir(), 'sigap-verif-'));
  const env = { ...process.env, GIT_INDEX_FILE: join(folder, 'index') };
  try {
    git(['read-tree', 'HEAD'], env);
    git(['add', '-A'], env);
    return git(['write-tree'], env);
  } finally {
    rmSync(folder, { recursive: true, force: true });
  }
}

function docker(argumen, opsi = {}) {
  return spawnSync('docker', argumen, { encoding: 'utf8', ...opsi });
}

/** Menyalurkan arsip snapshot ke stdin container, lalu menunggu container selesai. */
function jalankan(tree, image, skrip, argumenDocker = []) {
  return new Promise((selesai) => {
    const arsip = spawn('git', ['archive', tree], {
      cwd: AKAR,
      stdio: ['ignore', 'pipe', 'inherit'],
    });
    const wadah = spawn(
      'docker',
      ['run', '--rm', '-i', ...argumenDocker, image, 'bash', '-c', skrip],
      {
        stdio: ['pipe', 'inherit', 'inherit'],
      },
    );
    arsip.stdout.pipe(wadah.stdin);
    wadah.stdin.on('error', () => {}); // container berhenti lebih dulu = tidak masalah
    wadah.on('close', (kode) => selesai(kode ?? 1));
  });
}

async function targetApi(tree) {
  const id = process.pid;
  const jaringan = `sigap-verif-${id}`;
  const pg = `sigap-verif-pg-${id}`;
  const bersihkan = () => {
    docker(['stop', pg], { stdio: 'ignore' });
    docker(['network', 'rm', jaringan], { stdio: 'ignore' });
  };
  process.once('SIGINT', () => {
    bersihkan();
    process.exit(130);
  });

  try {
    console.log('→ PostgreSQL sementara (meniru service container CI)');
    docker(['network', 'create', jaringan], { stdio: 'ignore' });
    const mulai = docker([
      'run',
      '-d',
      '--rm',
      '--name',
      pg,
      '--network',
      jaringan,
      '-e',
      'POSTGRES_USER=sigap_app',
      '-e',
      'POSTGRES_PASSWORD=sigap_password',
      '-e',
      'POSTGRES_DB=sigap_ci',
      IMG_PG,
    ]);
    if (mulai.status !== 0) throw new Error(`Gagal memulai PostgreSQL: ${mulai.stderr}`);

    // TCP (-h 127.0.0.1): server sementara saat inisialisasi hanya mendengarkan di socket.
    let siap = false;
    for (let i = 0; i < 60 && !siap; i++) {
      siap =
        docker(['exec', pg, 'pg_isready', '-h', '127.0.0.1', '-U', 'sigap_app', '-d', 'sigap_ci'])
          .status === 0;
      if (!siap) await new Promise((r) => setTimeout(r, 1000));
    }
    if (!siap) throw new Error('PostgreSQL sementara tidak kunjung siap.');

    const berkasSkema = readdirSync(join(AKAR, 'infra', 'skema'))
      .filter((f) => /^(00|10|11)-.*\.sql$/.test(f))
      .sort();
    for (const f of berkasSkema) {
      const hasil = docker(
        [
          'exec',
          '-i',
          pg,
          'psql',
          '-U',
          'sigap_app',
          '-d',
          'sigap_ci',
          '-v',
          'ON_ERROR_STOP=1',
          '-q',
        ],
        { input: readFileSync(join(AKAR, 'infra', 'skema', f)) },
      );
      if (hasil.status !== 0) throw new Error(`Skema ${f} gagal dipasang:\n${hasil.stderr}`);
    }
    console.log(`→ skema terpasang (${berkasSkema.join(', ')})\n`);

    return await jalankan(tree, IMG_DOTNET, SKRIP_API, [
      '--network',
      jaringan,
      '-e',
      'DOTNET_NOLOGO=1',
      '-e',
      'DOTNET_CLI_TELEMETRY_OPTOUT=1',
      '-e',
      'SIGAP_DB_UJI=Host=' +
        pg +
        ';Port=5432;Database=sigap_ci;Username=sigap_app;Password=sigap_password;Timeout=5',
    ]);
  } finally {
    bersihkan();
  }
}

// ── Utama ──────────────────────────────────────────────────────────────────────────────────

const pilihan = process.argv[2] ?? 'semua';
const DAFTAR = { web: ['web'], api: ['api'], repo: ['repo'], semua: ['repo', 'web', 'api'] };
if (!DAFTAR[pilihan]) {
  console.error('Pemakaian: node scripts/verifikasi-linux.mjs web|api|repo|semua');
  process.exit(2);
}
if (docker(['info'], { stdio: 'ignore' }).status !== 0) {
  console.error(
    'Docker tidak berjalan atau tidak terpasang. Nyalakan Docker Desktop, lalu ulangi.',
  );
  process.exit(2);
}

const tree = buatSnapshot();
console.log(
  `Snapshot ${tree.slice(0, 12)} (${git(['ls-tree', '-r', '--name-only', tree]).split('\n').length} berkas)\n`,
);

const hasil = [];
for (const t of DAFTAR[pilihan]) {
  console.log(`\n━━━ ${t} ━━━`);
  const kode =
    t === 'web'
      ? await jalankan(tree, IMG_NODE, SKRIP_WEB, ['-e', 'HUSKY=0', '-e', 'CI=true'])
      : t === 'repo'
        ? await jalankan(tree, IMG_NODE, SKRIP_REPO)
        : await targetApi(tree);
  hasil.push([t, kode]);
}

console.log('\n━━━ ringkasan ━━━');
for (const [t, kode] of hasil) console.log(`  ${kode === 0 ? '✔' : '✖'} ${t}`);
process.exit(hasil.every(([, k]) => k === 0) ? 0 : 1);
