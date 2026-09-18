// Menghasilkan data seed KantorBmn dengan koordinat dummy untuk baris yang lintang/bujur-nya
// kosong dari sumber. TIDAK menyentuh database — murni transformasi data, mudah diuji sendiri.
//
// Deterministik: koordinat yang sama dihasilkan tiap kali dijalankan (RNG di-seed dari id BMN),
// supaya seeding ulang tidak membuat titik "melompat" tanpa alasan.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { PROVINSI_BBOX } from './provinsi-bbox.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
export const SUMBER_DEFAULT = 'C:/dev/MKB APPS/App/data/kantor-bmn.json';
export const OUTPUT_DEFAULT = join(HERE, 'output', 'kantor-bmn.seed.json');

// Hash string -> integer 32-bit, lalu PRNG mulberry32. Tidak butuh keacakan kriptografis;
// yang penting deterministik dan tersebar rata.
function seededRandom(kunci) {
  let h = 0x811c9dc5;
  for (let i = 0; i < kunci.length; i++) {
    h ^= kunci.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  let state = h >>> 0;
  return () => {
    state |= 0;
    state = (state + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Titik acak di dalam bbox, ditarik 8% ke arah tengah supaya tidak nangkring persis di
 * sudut/garis batas kotak (yang seringkali laut, bukan daratan provinsinya).
 */
function titikDalamBbox([utara, selatan, barat, timur], acak) {
  const susut = 0.08;
  const tinggi = utara - selatan;
  const lebar = timur - barat;
  const lat = selatan + tinggi * susut + acak() * tinggi * (1 - 2 * susut);
  const lon = barat + lebar * susut + acak() * lebar * (1 - 2 * susut);
  return { lintang: Number(lat.toFixed(6)), bujur: Number(lon.toFixed(6)) };
}

/**
 * @param {{register:string, provinsi:string|null, lintang:number|null, bujur:number|null}[]} gedung
 * @returns baris siap-seed, plus daftar provinsi yang tidak dikenali bbox (tidak digenerate).
 */
export function generateKoordinat(gedung) {
  const takDikenal = new Set();
  const hasil = gedung.map((g) => {
    if (g.lintang !== null && g.bujur !== null) {
      return { ...g, isKoordinatDummy: false };
    }
    const bbox = g.provinsi ? PROVINSI_BBOX[g.provinsi] : undefined;
    if (!bbox) {
      if (g.provinsi) takDikenal.add(g.provinsi);
      // Tanpa bbox yang dikenal, JANGAN mengarang koordinat (bisa menyesatkan lebih parah
      // daripada tidak ada titik sama sekali) — biarkan kosong, tetap tertandai butuh perhatian.
      return { ...g, lintang: null, bujur: null, isKoordinatDummy: false, provinsiTakDikenalUntukDummy: true };
    }
    const titik = titikDalamBbox(bbox, seededRandom(g.register));
    return { ...g, ...titik, isKoordinatDummy: true };
  });
  return { baris: hasil, provinsiTakDikenal: [...takDikenal] };
}

export async function main({ sumber = SUMBER_DEFAULT, output = OUTPUT_DEFAULT } = {}) {
  const mentah = JSON.parse(readFileSync(sumber, 'utf8'));
  // "sumber" dan "ditarikPada" ada sekali di level berkas (bukan per baris gedung) —
  // disalin ke tiap baris karena kolom KantorBmn.sumber/ditarikPada NOT NULL per baris.
  const gedungDenganAsalUsul = mentah.gedung.map((g) => ({
    ...g,
    sumber: mentah.sumber,
    ditarikPada: mentah.ditarikPada,
  }));
  const { baris, provinsiTakDikenal } = generateKoordinat(gedungDenganAsalUsul);
  const jumlahDummy = baris.filter((b) => b.isKoordinatDummy).length;

  mkdirSync(dirname(output), { recursive: true });
  writeFileSync(
    output,
    JSON.stringify(
      {
        _peringatan: `${jumlahDummy} dari ${baris.length} baris memakai koordinat DUMMY (is_koordinat_dummy = true). Lihat README.md.`,
        sumberAsli: mentah.sumber,
        ditarikPada: mentah.ditarikPada,
        digenerateePada: new Date().toISOString(),
        jumlah: baris.length,
        jumlahKoordinatDummy: jumlahDummy,
        provinsiTakDikenal,
        gedung: baris,
      },
      null,
      2,
    ) + '\n',
  );

  console.log(`${baris.length} baris ditulis ke ${output}`);
  console.log(`${jumlahDummy} baris memakai koordinat dummy (provinsi dikenal, bbox tersedia).`);
  if (provinsiTakDikenal.length > 0) {
    console.warn(`PERINGATAN: provinsi tanpa bbox, koordinat dibiarkan kosong: ${provinsiTakDikenal.join(', ')}`);
  }
  return { baris, jumlahDummy, provinsiTakDikenal };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  await main();
}
