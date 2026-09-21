// Pemeriksa lintas platform untuk seluruh repo. Development di Windows, production di container
// Linux; empat hal di bawah ini lolos begitu saja di Windows lalu meledak di Linux:
//
//   1. Akhir baris CRLF di INDEX git.            (yang di-checkout Linux; folder kerja tidak dihitung)
//   2. Dua berkas yang beda hanya kapitalisasi.  (Windows: satu berkas; Linux: dua berkas)
//   3. Skrip npm yang hanya jalan di satu shell. (`rm -rf`, `NODE_ENV=x cmd`, path berlawanan arah)
//   4. Path C# digabung dengan string manual.    (pemisah `\` vs `/`; pakai Path.Combine())
//
// Tanpa dependensi, supaya jalan sebelum `npm ci` selesai dan di dalam hook Git.
//
//   node scripts/periksa-repo.mjs
//
// Baris C# yang memang sengaja boleh dikecualikan dengan komentar:  // periksa-repo: izinkan <alasan>

import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AKAR = join(dirname(fileURLToPath(import.meta.url)), '..');

/** Berkas yang MEMANG harus CRLF — dicatat di .gitattributes (*.cmd, *.ps1). */
const BOLEH_CRLF = /\.(cmd|ps1|bat)$/i;

const ABAIKAN = /(^|\/)(node_modules|bin|obj|dist|\.angular|\.git|output)\//;

// ── Pemeriksa murni (dapat diuji) ──────────────────────────────────────────────────────────

/**
 * Membaca keluaran `git ls-files -z --eol` dan mengembalikan berkas yang INDEX-nya bukan LF.
 *
 * Yang diperiksa index, bukan folder kerja. `.gitattributes` (`* text=auto eol=lf`) menormalkan
 * CRLF menjadi LF saat `git add`, jadi CRLF di folder kerja Windows (mis. berkas scaffold hasil
 * Angular CLI) tidak pernah sampai ke Linux dan bukan masalah. Yang berbahaya adalah CRLF yang
 * lolos ke index — pemeriksaan ini menangkapnya.
 *
 * @returns {{ path: string, keadaan: string }[]}
 */
export function indeksBukanLf(keluaranEol) {
  const hasil = [];
  for (const baris of keluaranEol.split('\0')) {
    const m = /^i\/(\S+)\s+w\/(\S+)\s+(.*?)\t(.+)$/.exec(baris);
    if (!m) continue;
    const [, indeks, , , path] = m;
    if ((indeks === 'crlf' || indeks === 'mixed') && !BOLEH_CRLF.test(path)) {
      hasil.push({ path, keadaan: indeks });
    }
  }
  return hasil;
}

/** @returns {string[]} kelompok path yang bertabrakan bila kapitalisasi diabaikan. */
export function tabrakanKapitalisasi(daftarPath) {
  const kelompok = new Map();
  for (const p of daftarPath) {
    const kunci = p.toLowerCase();
    kelompok.set(kunci, [...(kelompok.get(kunci) ?? []), p]);
  }
  return [...kelompok.values()]
    .filter((g) => new Set(g).size > 1)
    .map((g) => [...new Set(g)].join('  ≠  '));
}

/** Pola pada NILAI skrip npm yang hanya berjalan di sebagian shell, beserta pengganti yang benar. */
const POLA_SKRIP = [
  { pola: /(^|[;&|]\s*)rm\s+-/, saran: 'pakai `rimraf <path>` (PowerShell tidak punya `rm -rf`)' },
  {
    pola: /(^|[;&|]\s*)(cp|mv|cat|touch|ln)\s/,
    saran: 'pakai skrip Node (.mjs) atau paket lintas platform',
  },
  {
    pola: /(^|[;&|]\s*)mkdir\s+-p/,
    saran: 'pakai skrip .mjs; `mkdir -p` tidak ada di PowerShell',
  },
  {
    pola: /(^|&&\s*|;\s*)[A-Z][A-Z0-9_]*=\S*\s+\S/,
    saran: 'pakai `cross-env VAR=nilai perintah` (PowerShell tidak mengenal `VAR=x cmd`)',
  },
  { pola: /(^|&&\s*|;\s*)(export|set)\s+[A-Z_]+/, saran: 'pakai `cross-env`' },
  { pola: /\\/, saran: 'pakai garis miring `/` untuk path; `\\` hanya jalan di Windows' },
];

/** @returns {{ nama: string, nilai: string, saran: string }[]} */
export function masalahSkripNpm(paketJson) {
  const hasil = [];
  for (const [nama, nilai] of Object.entries(paketJson.scripts ?? {})) {
    for (const { pola, saran } of POLA_SKRIP) {
      if (pola.test(nilai)) hasil.push({ nama, nilai, saran });
    }
  }
  return hasil;
}

/** @returns {{ baris: number, teks: string, saran: string }[]} */
export function masalahPathCsharp(teks) {
  const hasil = [];
  teks.split('\n').forEach((teksBaris, i) => {
    const baris = teksBaris.trim();
    if (
      baris.startsWith('//') ||
      baris.startsWith('*') ||
      baris.includes('periksa-repo: izinkan')
    ) {
      return;
    }
    const lapor = (saran) => hasil.push({ baris: i + 1, teks: baris.slice(0, 100), saran });

    // Menggabung path dengan operator + dan pemisah literal.
    if (/\+\s*["']\/["']\s*\+|\+\s*["']\\{1,2}["']\s*\+/.test(baris)) {
      lapor('gabung path dengan Path.Combine(), bukan `+ "/" +`');
    }

    // Path Windows literal: "C:\\dev\\x" atau @"C:\dev\x". Regex ("\\d+") tidak ikut terjaring
    // karena yang dicari huruf/angka di kedua sisi pemisah, dan baris Regex dikecualikan.
    if (/Regex/.test(baris)) return;
    const biasa = /"[^"\n]*[A-Za-z0-9_.]\\\\[A-Za-z0-9_.][^"\n]*"/;
    const verbatim = /@"[^"\n]*[A-Za-z0-9_.]\\[A-Za-z0-9_.][^"\n]*"/;
    if (biasa.test(baris) || verbatim.test(baris)) {
      lapor('path dengan pemisah `\\` hanya jalan di Windows; pakai Path.Combine()');
    }
  });
  return hasil;
}

// ── Pengumpul berkas ───────────────────────────────────────────────────────────────────────

function git(...argumen) {
  return execFileSync('git', argumen, {
    cwd: AKAR,
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
  });
}

function bacaTeks(path) {
  try {
    const buf = readFileSync(join(AKAR, path));
    return buf.includes(0) ? null : buf.toString('utf8');
  } catch {
    return null; // terhapus di folder kerja tetapi belum di-stage
  }
}

function main() {
  const berkas = git('ls-files', '-z', '--cached', '--others', '--exclude-standard')
    .split('\0')
    .filter((p) => p && !ABAIKAN.test(p));
  const masalah = [];

  // 1. Akhir baris (index)
  for (const { path, keadaan } of indeksBukanLf(git('ls-files', '-z', '--eol'))) {
    masalah.push(
      `[akhir baris] ${path}: index berisi ${keadaan.toUpperCase()}. Wajib LF — perbaiki: git add --renormalize ${path}`,
    );
  }

  // 2. Kapitalisasi
  for (const g of tabrakanKapitalisasi(berkas)) {
    masalah.push(
      `[kapitalisasi] berkas hanya berbeda huruf besar/kecil: ${g}. Linux menganggapnya dua berkas, Windows satu.`,
    );
  }

  // 3. Skrip npm
  for (const p of berkas.filter((x) => x.endsWith('package.json'))) {
    const teks = bacaTeks(p);
    if (teks === null) continue;
    for (const m of masalahSkripNpm(JSON.parse(teks))) {
      masalah.push(`[skrip npm] ${p} → "${m.nama}": ${m.nilai}\n      ${m.saran}`);
    }
  }

  // 4. Path C#
  for (const p of berkas.filter((x) => x.endsWith('.cs'))) {
    const teks = bacaTeks(p);
    if (teks === null) continue;
    for (const m of masalahPathCsharp(teks)) {
      masalah.push(`[path C#] ${p}:${m.baris}: ${m.teks}\n      ${m.saran}`);
    }
  }

  if (masalah.length) {
    console.error(`periksa-repo: ${masalah.length} masalah lintas platform\n`);
    for (const m of masalah) console.error(`  ✖ ${m}\n`);
    console.error('Aturannya ada di AGENTS.md bagian "Lintas platform".');
    process.exit(1);
  }
  console.log(
    `periksa-repo: ${berkas.length} berkas diperiksa, tidak ada masalah lintas platform.`,
  );
}

if (process.argv[1] && fileURLToPath(import.meta.url) === join(process.argv[1])) main();
