// Pembantu bersama skrip verifikasi Linux: snapshot git, Docker, dan PostgreSQL sementara.
// Dipakai scripts/verifikasi-linux.mjs (pipeline CI) dan scripts/verifikasi-container.mjs (image + run).

import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const AKAR = join(dirname(fileURLToPath(import.meta.url)), '..', '..');

export function git(argumen, env = process.env) {
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
export function buatSnapshot() {
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

export function docker(argumen, opsi = {}) {
  return spawnSync('docker', argumen, { encoding: 'utf8', ...opsi });
}

export function dockerBerjalan() {
  return docker(['info'], { stdio: 'ignore' }).status === 0;
}

/**
 * PostgreSQL sementara di jaringan Docker sendiri, dengan skema 33 tabel terpasang (meniru service
 * container CI). Mengembalikan nama jaringan dan container, serta fungsi pembersih.
 */
export async function mulaiPostgres(awalan) {
  const id = process.pid;
  const jaringan = `${awalan}-${id}`;
  const pg = `${awalan}-pg-${id}`;
  const bersihkan = () => {
    docker(['stop', pg], { stdio: 'ignore' });
    docker(['network', 'rm', jaringan], { stdio: 'ignore' });
  };

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
      'postgres:16',
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

    return { jaringan, pg, bersihkan };
  } catch (galat) {
    bersihkan();
    throw galat;
  }
}
