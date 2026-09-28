// Mencetak status setiap komponen dummy yang terdaftar di DUMMY_REGISTRY.md, tanpa perlu
// membuka dan menggulir berkasnya. Dipakai untuk pengecekan cepat "dummy mana yang masih aktif"
// sebelum mengerjakan sesuatu yang menyentuhnya, dan sebagai bagian dari banner startup
// development (lihat Program.cs).
//
//   node scripts/status-dummy.mjs
//
// Sumber kebenarannya tetap DUMMY_REGISTRY.md (bagian 1, "Registry") — skrip ini hanya mem-parse
// tabel markdown-nya, bukan menyimpan daftar sendiri yang bisa basi.

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const AKAR = join(dirname(fileURLToPath(import.meta.url)), '..');

const POLA_JUDUL = /^### 1\.(\d+)\s+(.+)$/;
const POLA_STATUS = /^\|\s*\*\*Status\*\*\s*\|\s*(.*?)\s*\|\s*$/;

/**
 * @param {string} teks isi DUMMY_REGISTRY.md
 * @returns {{ nomor: string, nama: string, status: string | null }[]}
 */
export function daftarDummy(teks) {
  const baris = teks.split('\n');
  const hasil = [];
  let berjalan = null;
  let diRegistry = false;

  for (const b of baris) {
    if (/^##\s/.test(b)) {
      // Bagian level-2 baru. Hanya "## 1. Registry" yang berisi entri dummy 1.x.
      if (berjalan) hasil.push(berjalan);
      berjalan = null;
      diRegistry = /^##\s+1\.\s/.test(b);
      continue;
    }
    if (!diRegistry) continue;

    const judul = POLA_JUDUL.exec(b);
    if (judul) {
      if (berjalan) hasil.push(berjalan);
      berjalan = { nomor: judul[1], nama: judul[2].trim(), status: null };
      continue;
    }
    if (berjalan && berjalan.status === null) {
      const status = POLA_STATUS.exec(b);
      if (status) berjalan.status = status[1];
    }
  }
  if (berjalan) hasil.push(berjalan);
  return hasil;
}

/** Ringkasan satu kata dari kolom Status, untuk tampilan cepat. */
export function ringkasStatus(status) {
  if (!status) return 'TIDAK DIKETAHUI';
  if (/^\*\*Sebagian sudah ditukar/i.test(status)) return 'SEBAGIAN DITUKAR';
  if (/^\*\*Aktif\*\*/i.test(status)) return 'AKTIF';
  return 'LIHAT DETAIL';
}

function main() {
  const teks = readFileSync(join(AKAR, 'DUMMY_REGISTRY.md'), 'utf8');
  const daftar = daftarDummy(teks);

  console.log(`DUMMY_REGISTRY.md — ${daftar.length} komponen dummy terdaftar\n`);
  for (const d of daftar) {
    console.log(`1.${d.nomor} ${d.nama}`);
    console.log(
      `     [${ringkasStatus(d.status)}] ${d.status ?? '(kolom Status tidak ditemukan)'}`,
    );
  }
  console.log('\nDetail lengkap dan asumsi tiap dummy: DUMMY_REGISTRY.md');
}

if (process.argv[1] && fileURLToPath(import.meta.url) === join(process.argv[1])) main();
