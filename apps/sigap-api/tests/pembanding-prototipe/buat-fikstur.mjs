// Pembuat fikstur pembanding porting P4.4.
//
// Menjalankan fungsi ASLI src/logic/*.ts prototipe (tanpa build, lewat muat-prototipe.mjs) atas
// skenario data uji, lalu merekam masukan dan keluarannya ke
// ../Sigap.Domain.Tests/Pembanding/fikstur/*.json. Tes C# membaca berkas itu dan mewajibkan port
// menghasilkan keluaran yang sama, kecuali kasus yang ditandai selisih disengaja (API_CONTRACT
// bagian 6) di sisi tes.
//
// Fikstur ikut di-commit, jadi CI tidak memerlukan repo prototipe. Jalankan ulang hanya bila
// skenario ditambah atau baseline prototipe berubah:
//
//   node apps/sigap-api/tests/pembanding-prototipe/buat-fikstur.mjs
//
// Lokasi prototipe bawaan: C:/dev/MKB APPS/App (MIGRATION_NOTES 5.1), atau SIGAP_PROTOTIPE.
// Skrip menolak berjalan bila prototipe bukan baseline porting 1b1487a atau src/logic/ berubah,
// sebab fikstur dari prototipe lain bukan lagi pembanding yang disepakati.

import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { register } from 'node:module';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const DI_SINI = path.dirname(fileURLToPath(import.meta.url));
const PROTOTIPE = process.env.SIGAP_PROTOTIPE ?? 'C:/dev/MKB APPS/App';
const BASELINE = '1b1487a';
const KELUARAN = path.join(DI_SINI, '..', 'Sigap.Domain.Tests', 'Pembanding', 'fikstur');

// ── Penjaga baseline ─────────────────────────────────────────────────────────────────────────
const git = (...args) => execFileSync('git', ['-C', PROTOTIPE, ...args], { encoding: 'utf8' }).trim();
const commit = git('rev-parse', 'HEAD');
if (!commit.startsWith(BASELINE)) {
  console.error(`Prototipe di ${PROTOTIPE} berada di ${commit.slice(0, 7)}, bukan baseline ${BASELINE}.`);
  process.exit(1);
}
if (git('status', '--porcelain', '--', 'src/logic')) {
  console.error('src/logic/ prototipe punya perubahan yang belum di-commit. Fikstur harus dari baseline bersih.');
  process.exit(1);
}

register(pathToFileURL(path.join(DI_SINI, 'muat-prototipe.mjs')), {
  data: { akarSrc: path.join(PROTOTIPE, 'src') }
});

const muat = (nama, versi) =>
  import(pathToFileURL(path.join(PROTOTIPE, 'src', 'logic', `${nama}.ts`)).href + (versi ? `?v=${versi}` : ''));

// ── Pembantu ─────────────────────────────────────────────────────────────────────────────────
const T0 = Date.parse('2026-09-18T03:00:00.000Z');
const dateNowAsli = Date.now;
const denganJam = async (ms, fn) => {
  Date.now = () => ms;
  try {
    return await fn();
  } finally {
    Date.now = dateNowAsli;
  }
};

// Karakter tak terlihat dibangun dari kodenya supaya tidak ada yang tersembunyi di berkas ini.
const NEL = String.fromCharCode(0x85); // bukan spasi bagi JavaScript, spasi bagi .NET
const BOM = String.fromCharCode(0xfeff); // spasi bagi JavaScript, bukan spasi bagi .NET
const NBSP = String.fromCharCode(0xa0);
const CR = String.fromCharCode(0x0d);
const LS = String.fromCharCode(0x2028);
const ulang = (n, c = 'a') => c.repeat(n);

// NaN dan Infinity tidak ada di JSON; masukan menuliskannya sebagai teks.
const angka = (v) => (v === 'NaN' ? NaN : v === 'Infinity' ? Infinity : v === '-Infinity' ? -Infinity : v);

const berkas = {};
function rekam(modul, fungsi, masukan, keluaran) {
  berkas[modul] ??= { prototipe: { repo: 'atepagung/sigap-prototipe', commit, berkas: `src/logic/${modul}.ts` }, kasus: {} };
  (berkas[modul].kasus[fungsi] ??= []).push({ masukan, keluaran: keluaran === undefined ? null : keluaran });
}

// ── safety-check.ts ──────────────────────────────────────────────────────────────────────────
{
  const m = await muat('safety-check');
  for (const [status, opsi] of [
    ['AMAN', undefined],
    ['AMAN', { lat: -0.5071, lng: 101.4478 }],
    ['BUTUH_BANTUAN', { lat: 90, lng: -180 }],
    ['AMAN', { lat: 90.0001, lng: 180.0001 }],
    ['AMAN', { lat: -90.5, lng: 12 }],
    ['AMAN', { lat: 'NaN', lng: 'Infinity' }],
    ['AMAN', { lat: null, lng: null }],
    ['AMAN', { lat: 0, lng: 0, kehadiran: 'WFO' }],
    ['BUTUH_BANTUAN', { kehadiran: 'LIBUR' }]
  ]) {
    const o = opsi && { ...opsi, lat: angka(opsi.lat), lng: angka(opsi.lng) };
    rekam('safety-check', 'bentukJawabanSafetyCheck', { status, opsi: opsi ?? null }, m.bentukJawabanSafetyCheck(status, o));
  }
  for (const alasan of [
    '', '    ', 'abcd', 'abcde', '  abcd  ', ` abcde `, `${BOM}abcd${BOM}`, `${NEL}abcd`, `${NBSP}abcd${NBSP}`,
    ulang(300), ulang(301), `  ${ulang(300)}  `, 'Dihubungi lewat telepon oleh Satgas pukul 10.12'
  ]) {
    rekam('safety-check', 'validasiAlasanCatatan', { alasan }, m.validasiAlasanCatatan(alasan));
  }
}

// ── lapor-verifikasi.ts ──────────────────────────────────────────────────────────────────────
{
  const m = await muat('lapor-verifikasi');
  for (const input of [
    { jenisBencana: 'Banjir', lokasi: 'Lantai 1 Gedung A', deskripsi: 'Air masuk setinggi 20 cm' },
    { jenisBencana: '', lokasi: 'Lantai 1', deskripsi: '' },
    { jenisBencana: 'Banjir', lokasi: '', deskripsi: '' },
    { jenisBencana: 'Banjir', lokasi: '   ', deskripsi: '' },
    { jenisBencana: 'Banjir', lokasi: ulang(200), deskripsi: '' },
    { jenisBencana: 'Banjir', lokasi: ulang(201), deskripsi: '' },
    { jenisBencana: 'Banjir', lokasi: 'Lobi', deskripsi: ulang(2000) },
    { jenisBencana: 'Banjir', lokasi: 'Lobi', deskripsi: ulang(2001) },
    { jenisBencana: 'Gempa', lokasi: 'Lobi', deskripsi: '' },
    { jenisBencana: 'gempa bumi', lokasi: ulang(201), deskripsi: '' }
  ]) {
    rekam('lapor-verifikasi', 'validasiLaporanBencana', input, m.validasiLaporanBencana(input));
  }
  const MB = 1_048_576;
  for (const file of [
    { size: 482113, type: 'image/jpeg' },
    { size: 10 * MB, type: 'image/png' },
    { size: 10 * MB + 1, type: 'image/png' },
    { size: 15.75 * MB, type: 'image/jpeg' },
    { size: 10.25 * MB, type: 'application/pdf' },
    { size: 1000, type: 'video/mp4' },
    { size: 1000, type: 'image/gif' },
    { size: 1000, type: 'video/webm' },
    { size: 1000, type: 'audio/mpeg' },
    { size: 1000, type: 'audio/webm' },
    { size: 1000, type: 'application/pdf' },
    { size: 1000, type: '' },
    { size: 1000, type: 'IMAGE/JPEG' }
  ]) {
    rekam('lapor-verifikasi', 'validasiLampiranBencana', file, m.validasiLampiranBencana(file));
  }
  for (const status of ['MENUNGGU', 'TERVERIFIKASI', 'DITOLAK']) {
    rekam('lapor-verifikasi', 'bisaDiverifikasi', { status }, m.bisaDiverifikasi(status));
  }
  for (const [approve, catatan] of [
    [true, undefined], [false, undefined], [false, '   '], [false, 'Getaran dari proyek konstruksi'],
    [true, '  catatan  '], [true, ulang(400)], [true, ulang(401)], [false, ulang(401)], [false, `${BOM}${BOM}`], [false, NEL]
  ]) {
    rekam('lapor-verifikasi', 'validasiVerifikasiAlert', { approve, catatan: catatan ?? null }, m.validasiVerifikasiAlert(approve, catatan));
  }
  rekam('lapor-verifikasi', 'konstanta', {}, { BATAS_LAMPIRAN_BYTES: m.BATAS_LAMPIRAN_BYTES, JENDELA_DEDUP_MS: m.JENDELA_DEDUP_MS });
}

// ── asesmen-terpadu.ts ───────────────────────────────────────────────────────────────────────
{
  const m = await muat('asesmen-terpadu');
  for (const [j, k] of [['', ''], ['Banjir', ''], ['', 'Minor'], ['Banjir', 'Minor'], ['  ', '  ']]) {
    rekam('asesmen-terpadu', 'validasiAsesmenDasar', { jenisBencana: j, kondisiFisik: k }, m.validasiAsesmenDasar(j, k));
  }
  const iso = (ms) => new Date(ms).toISOString();
  for (const teks of ['', iso(T0), iso(T0 + 60_000), iso(T0 + 60_001), iso(T0 + 3_600_000), iso(T0 - 86_400_000), 'bukan tanggal']) {
    const h = await denganJam(T0, () => m.validasiWaktuKejadian(teks));
    rekam('asesmen-terpadu', 'validasiWaktuKejadian', { teks, sekarang: iso(T0) }, { ...h, waktu: h.waktu ? h.waktu.toISOString() : null });
  }
  const unit = [
    { id: 'lk1', nama: 'Layanan SP2D', rtoJam: 24 },
    { id: 'lk2', nama: 'Layanan Penerimaan Negara', rtoJam: 1 },
    { id: 'lk3', nama: 'Layanan Lelang', rtoJam: 168 }
  ];
  for (const [layananUnit, status] of [
    [unit, { lk1: 'TERGANGGU', lk2: 'NORMAL', lk3: 'BERHENTI_TOTAL' }],
    [unit, { lk1: 'TERGANGGU', lk3: 'NORMAL' }],
    [unit, { lk1: '', lk2: 'NORMAL', lk3: 'NORMAL' }],
    [unit, { lk1: 'RUSAK', lk2: 'NORMAL', lk3: 'NORMAL' }],
    [unit, { lk1: 'NORMAL', lk2: 'NORMAL', lk3: 'NORMAL', bukanMilikUnit: 'TERGANGGU' }],
    [[], { lk1: 'TERGANGGU' }]
  ]) {
    const hasil = m.bentukPenilaianLayanan(layananUnit, (id) => status[id] ?? '');
    rekam('asesmen-terpadu', 'bentukPenilaianLayanan', { layananUnit, status }, hasil);
    rekam('asesmen-terpadu', 'layananTerdampak', { seluruhLayanan: hasil }, m.layananTerdampak(hasil));
  }
  for (const nama of ['', '   ', 'Layanan SP2D', ulang(120), ulang(121), `  ${ulang(120)}  `, `${BOM}x`, NEL]) {
    rekam('asesmen-terpadu', 'validasiNamaLayananManual', { nama }, m.validasiNamaLayananManual(nama));
  }
  for (const rtoJam of [1, 24, 48, 96, 168, 192, 0, 2, 72, 200, -1]) {
    rekam('asesmen-terpadu', 'rtoJamDikenali', { rtoJam }, m.rtoJamDikenali(rtoJam));
  }
  rekam('asesmen-terpadu', 'konstanta', {}, { JENDELA_DEDUP_ASESMEN_MS: m.JENDELA_DEDUP_ASESMEN_MS });
}

// ── trigger-sasaran.ts ───────────────────────────────────────────────────────────────────────
{
  const m = await muat('trigger-sasaran');
  const unitRiau = { id: 'u-kanwil-riau', nama: 'Kanwil DJP Riau', provinsi: 'Riau', eselonIKey: 'djp' };
  const unitTanpaEselon = { id: 'u-x', nama: 'Unit Tanpa Eselon', provinsi: null, eselonIKey: null };
  const unitEselonKosong = { id: 'u-y', nama: 'Unit Eselon Kosong', provinsi: 'Riau', eselonIKey: '' };
  for (const u of [unitRiau, unitTanpaEselon]) {
    rekam('trigger-sasaran', 'sasaranUnit', { unit: u }, m.sasaranUnit(u));
  }
  for (const [p, k, e] of [
    ['Riau', null, null], ['Riau', 'Kota Pekanbaru', null], ['Riau', null, 'djp'], ['Riau', 'Kota Pekanbaru', 'djbc'],
    ['Riau', '', ''], ['Riau', '  ', null]
  ]) {
    rekam('trigger-sasaran', 'lokasiWilayahProvinsi', { provinsi: p, kota: k, eselon: e }, m.lokasiWilayahProvinsi(p, k, e));
  }
  for (const [u, p, k] of [
    [unitRiau, '', ''], [unitRiau, 'Riau', ''], [unitRiau, 'Riau', 'Kota Pekanbaru'], [unitRiau, '', 'Kota Pekanbaru'],
    [unitTanpaEselon, '', ''], [unitTanpaEselon, 'Aceh', ''], [unitEselonKosong, '', '']
  ]) {
    rekam('trigger-sasaran', 'sasaranEselonI', { unit: u, provinsi: p, kota: k }, m.sasaranEselonI(u, p, k));
  }
  for (const [p, k, e] of [
    ['', '', ''], ['Riau', '', ''], ['Riau', 'Kota Pekanbaru', ''], ['Riau', 'Kota Pekanbaru', 'djp'],
    ['', '', 'djp'], ['', 'Kota Pekanbaru', ''], ['', 'Kota Pekanbaru', 'djp']
  ]) {
    rekam('trigger-sasaran', 'sasaranNasional', { provinsi: p, kota: k, eselon: e }, m.sasaranNasional(p, k, e));
  }
  for (const [j, pesan] of [['', ''], ['Banjir', ''], ['Banjir', ulang(500)], ['Banjir', ulang(501)], ['', ulang(501)]]) {
    rekam('trigger-sasaran', 'validasiTriggerDasar', { jenisBencana: j, pesan }, m.validasiTriggerDasar(j, pesan));
  }
}

// ── bencana.ts, adb.ts, rto.ts ───────────────────────────────────────────────────────────────
{
  const b = await muat('bencana');
  rekam('bencana', 'JENIS_BENCANA', {}, b.JENIS_BENCANA);
  rekam('bencana', 'KATEGORI_LABEL', {}, b.KATEGORI_LABEL);
  rekam('bencana', 'SEMUA_JENIS', {}, b.SEMUA_JENIS);
  rekam('bencana', 'LEVEL_KEPARAHAN', {}, b.LEVEL_KEPARAHAN);
  for (const jenis of [...b.SEMUA_JENIS, 'gempa bumi', 'Gempa Bumi ', '', 'Kebakaran', 'Ancaman Bom']) {
    rekam('bencana', 'kategoriDari', { jenis }, b.kategoriDari(jenis));
    rekam('bencana', 'labelKategoriDari', { jenis }, b.labelKategoriDari(jenis));
  }

  const a = await muat('adb');
  rekam('adb', 'PERIODE_ADB', {}, a.PERIODE_ADB);
  for (const jam of [null, 1, 24, 48, 96, 168, 192, 5, 23, 25, 36, 60, 100]) {
    rekam('adb', 'labelJam', { jam }, a.labelJam(jam));
  }

  const r = await muat('rto');
  const mulai = '2026-09-18T00:00:00.000Z';
  const jam = (h) => new Date(Date.parse(mulai) + h * 3_600_000).toISOString();
  for (const [rtoJam, h] of [
    [24, 0], [24, 12], [24, 17.999], [24, 18], [24, 23.5], [24, 24], [24, 30], [24, -2],
    [1, 0.5], [1, 0.76], [0, 0.5], [-5, 3], [168, 100.25], [192, 500]
  ]) {
    rekam('rto', 'hitungRto', { mulai, rtoJam, sekarang: jam(h) }, r.hitungRto(new Date(mulai), rtoJam, new Date(jam(h))));
  }
  for (const j of [0, 0.001, 0.5, 0.9917, 0.99999, 1, 1.5, 1.999, 2.25, 23.99, 24, 25, 36.5, 47.6, 48, 100.5, -1]) {
    rekam('rto', 'labelDurasi', { jam: j }, r.labelDurasi(j));
  }
}

// ── terdampak.ts & picu-otomatis.ts ──────────────────────────────────────────────────────────
// Baris gedung diambil dari data/kantor-bmn.json prototipe (nama satker, gedung, kab/kota,
// provinsi, kondisi apa adanya). Pengenal dan alamat dibuat sendiri; koordinat buatan hanya
// untuk skenario radius.
const G = (id, namaSatker, namaGedung, kabkota, provinsi, kondisi, lain = {}) => ({
  id, namaSatker, namaGedung, alamat: null, kabkota, provinsi, kondisi, lintang: null, bujur: null, ...lain
});
const GEDUNG = [
  G('k01', 'KPP Pratama Padang Sidempuan', null, 'Kota Padangsidimpuan', 'Sumatera Utara', 'Baik'),
  G('k02', 'KPP Pratama Padang Dua', 'Bangunan Gedung Utama', 'Kota Padang', 'Sumatera Barat', 'Rusak Ringan', { alamat: '' }),
  G('k03', 'KPP Pratama Padang Dua', 'Bangunan Gedung Kantor Permanen', 'Kota Padang', 'Sumatera Barat', 'Baik', { alamat: 'Jl. Contoh No. 3' }),
  G('k04', 'KPP Pratama Padang Dua', 'Bangun Gedung Kantor Permanen', 'Kota Padang', 'Sumatera Barat', 'Rusak Berat', { alamat: 'Jl. Contoh No. 4' }),
  G('k05', 'KPP Pratama Padang Satu', null, 'Kota Padang', 'Sumatera Barat', 'Rusak Ringan'),
  G('k06', 'KP2KP Padang Panjang', 'Gedung KP2KP Padang Panjang', 'Kota Padang Panjang', 'Sumatera Barat', 'Baik'),
  G('k07', 'KP2KP Padang Panjang', 'Gudang KP2KP Padang Panjang', 'Kota Padang Panjang', 'Sumatera Barat', 'Rusak Ringan'),
  G('k08', 'Kanwil DJP Sumatera Barat dan Jambi', '-', 'Kota Padang', 'Sumatera Barat', 'Baik'),
  G('k09', 'KPPN Padang', 'Gudang Arsip KPPN Padang', 'Kota Padang', 'Sumatera Barat', 'Baik'),
  G('k10', 'KPPN Padang', 'Gedung Kantor untuk Layanan', 'Kota Padang', 'Sumatera Barat', 'Rusak Ringan'),
  G('k11', 'GKN Jayapura', 'Gedung Keuangan Negara Jayapura', 'Kota Jayapura', 'P A P U A', 'Baik'),
  G('k12', 'KP2KP Wamena', 'Bangunan Kantor', 'Kab. Jayawijaya', 'P A P U A', 'Baik'),
  G('k13', 'KPPBC Tmp C Jayapura', 'Tanah Bangunan Rumah Negara Dalam Proses Penggolongan', 'Kab. Sarmi', 'P A P U A', 'Rusak Berat'),
  G('k14', 'KPP Pratama Sukabumi', 'Bangunan Gedung Kantor Permanen', 'Kota Sukabumi', 'Jawa Barat', 'Baik'),
  G('k15', 'KPP Pratama Sukabumi', 'Bangunan Gedung Pos Pelayanan Pajak', 'Kab. Sukabumi', 'Jawa Barat', 'Baik'),
  G('k16', 'KP2KP Pelabuhan Ratu', '-', 'Kab. Sukabumi', 'Jawa Barat', 'Baik'),
  G('k17', 'KPP Pratama Cianjur', 'Gedung Kantor Utama', 'Kab. Cianjur', 'Jawa Barat', 'Baik'),
  G('k18', 'KPP Pratama Cianjur', 'Pos Pelayanan Pajak Cipanas', 'Kab. Cianjur', 'Jawa Barat', 'Rusak Ringan'),
  G('k19', 'Kantor Wilayah DJP Riau', 'Gedung Kantor', 'Kota Pekanbaru', 'Riau', 'Baik'),
  G('k20', null, null, 'Kota Pekanbaru', ' Riau ', 'Rusak Sedang'),
  G('k21', 'Kanwil DJBC Riau', 'Gedung Utama', 'Kota Adm. Pekanbaru', 'Riau', 'Rusak Ringan'),
  G('k22', 'KPPN Tanpa Kota', 'Gedung Kantor', null, 'Aceh', 'Baik'),
  G('k23', 'Kantor Kosong', 'Gedung', 'Kab.', 'Aceh', 'Baik'),
  ...Array.from({ length: 35 }, (_, i) => G(`m${String(i).padStart(2, '0')}`, `Satker Uji ${i}`, 'Gedung Kantor', 'Kab. Mimika', 'P A P U A', 'Baik'))
];
const GEDUNG_BERKOORDINAT = [
  G('r01', 'KPP Pratama Padang Satu', 'Gedung A', 'Kota Padang', 'Sumatera Barat', 'Baik', { lintang: -0.95, bujur: 100.354 }),
  G('r02', 'KPP Pratama Padang Satu', 'Gedung B', 'Kota Padang', 'Sumatera Barat', 'Rusak Berat', { lintang: -0.93, bujur: 100.36 }),
  G('r03', 'KPPN Padang', 'Gedung Kantor', 'Kota Padang', 'Sumatera Barat', 'Baik', { lintang: -0.9, bujur: 100.4 }),
  G('r04', 'KP2KP Padang Panjang', 'Gedung', 'Kota Padang Panjang', 'Sumatera Barat', 'Baik', { lintang: -0.46, bujur: 100.4 }),
  G('r05', 'KPP Pratama Padang Dua', 'Gudang', 'Kota Padang', 'Sumatera Barat', 'Baik', { lintang: -0.951, bujur: 100.355 }),
  G('r06', 'Kantor Tepi Radius', 'Gedung', 'Kota Padang', 'Sumatera Barat', 'Baik', { lintang: -0.95 + 10 / 111.195, bujur: 100.354 }),
  G('r07', 'Kantor Tanpa Bujur', 'Gedung', 'Kota Padang', 'Sumatera Barat', 'Baik', { lintang: -0.95 })
];

const prismaKantor = (baris) => ({
  kantorBmn: {
    findMany: async ({ where, take }) => {
      let r = baris;
      if (where?.lintang) r = r.filter((g) => g.lintang !== null && g.bujur !== null);
      if (where?.kabkota) r = r.filter((g) => g.kabkota !== null);
      return r.slice(0, take ?? r.length);
    }
  }
});

const gempa = (lain) => ({
  tanggal: '18 Sep 2026', jam: '09:40:15 WIB', waktu: '2026-09-18T02:40:15+00:00', magnitudo: '5.6',
  kedalaman: '10 km', wilayah: 'Pusat gempa berada di darat 10 km BaratLaut Kab. Cianjur', lintang: '6.84 LS',
  bujur: '107.05 BT', koordinat: null, potensi: 'Tidak berpotensi tsunami', dirasakan: null, shakemap: null, ...lain
});

{
  const m = await muat('terdampak');
  for (const teks of [
    'III-IV Cianjur, II-III Kota Sukabumi', 'II-III Jayapura, II-III Wamena, II Yakuhimo', null, '', 'Cianjur',
    'IV  Kab. Garut', 'II - III Padang', 'ab', 'V Ab', 'IIIX Foo', 'iv Padang', 'V Padang,', ' , ,V Padang',
    `V Padang${CR}Kota`, `${NBSP}V Padang`, `V${NEL}Padang`, `${BOM}V Padang`, `V Padang${LS}x`, 'XII Banda Aceh, I Sabang'
  ]) {
    rekam('terdampak', 'uraiDirasakan', { teks }, m.uraiDirasakan(teks));
  }
  for (const [a, b, c, d] of [
    [-0.95, 100.354, -0.95, 100.354], [-0.95, 100.354, -0.93, 100.36], [-6.2, 106.8, -2.53, 140.72],
    [0, 0, 0, 180], [89.9, 0, -89.9, 180], [-0.95, 100.354, -0.95 + 10 / 111.195, 100.354]
  ]) {
    rekam('terdampak', 'jarakKm', { aLat: a, aLng: b, bLat: c, bLng: d }, m.jarakKm(a, b, c, d));
  }
  const skenario = [
    ['tanpa koordinat dan tanpa Dirasakan', gempa({}), ['GEDUNG']],
    ['Padang tidak menarik Padang Panjang maupun Padangsidimpuan', gempa({ dirasakan: 'III-IV Padang' }), ['GEDUNG']],
    ['Wamena lewat akhiran nama satker, Papua dirapikan', gempa({ dirasakan: 'II-III Jayapura, II-III Wamena' }), ['GEDUNG']],
    ['Kab. dan Kota Sukabumi sama-sama cocok', gempa({ dirasakan: 'III Kab. Cianjur, II Sukabumi' }), ['GEDUNG']],
    ['nama pendek dilewati, kota tanpa MMI', gempa({ dirasakan: 'V Abc, Pekanbaru' }), ['GEDUNG']],
    ['lebih dari 30 kantor dipotong', gempa({ dirasakan: 'IV Mimika' }), ['GEDUNG']],
    ['satu gedung tertarik dua kota hanya sekali', gempa({ dirasakan: 'III Padang, IV Kota Padang' }), ['GEDUNG']],
    ['berkoordinat: radius 10 km, gudang dikeluarkan', gempa({ dirasakan: 'V Padang', koordinat: { lat: -0.95, lng: 100.354 } }), ['GEDUNG_BERKOORDINAT', 'GEDUNG']],
    ['berkoordinat tetapi tidak ada yang dalam radius', gempa({ dirasakan: 'V Padang', koordinat: { lat: -6.2, lng: 106.8 } }), ['GEDUNG_BERKOORDINAT', 'GEDUNG']],
    ['gempa berkoordinat, gedung belum: jatuh ke cara kota', gempa({ dirasakan: 'V Padang', koordinat: { lat: -0.95, lng: 100.354 } }), ['GEDUNG']],
    ['gempa berkoordinat tanpa Dirasakan, gedung belum', gempa({ koordinat: { lat: -0.95, lng: 100.354 } }), ['GEDUNG']]
  ];
  // Himpunan gedung ditulis sekali di bagian "data" fikstur; skenario hanya menyebut namanya
  // (disambung berurutan), supaya fikstur tidak mengulang puluhan baris yang sama per skenario.
  const HIMPUNAN = { GEDUNG, GEDUNG_BERKOORDINAT };
  for (const [nama, g, himpunan] of skenario) {
    globalThis.__prismaPembanding = prismaKantor(himpunan.flatMap((h) => HIMPUNAN[h]));
    const hasil = await m.gedungTerdampak(g);
    delete globalThis.__prismaPembanding;
    rekam('terdampak', 'gedungTerdampak', { nama, gempa: g, gedung: himpunan }, hasil);
  }
  berkas.terdampak.data = HIMPUNAN;
}

{
  const m = await muat('picu-otomatis');
  for (const mmi of ['', 'V', 'II-III', 'iv', ' IV - V ', 'X-II', 'XIII', 'IIII', 'V-', '-', 'VI-VII-IX', null]) {
    rekam('picu-otomatis', 'angkaMmi', { mmi }, m.angkaMmi(mmi));
  }
  for (let n = 0; n <= 13; n++) rekam('picu-otomatis', 'romawiMmi', { n }, m.romawiMmi(n));
  rekam('picu-otomatis', 'ARTI_MMI', {}, m.ARTI_MMI);

  // AMBANG_MMI dan PICU_OTOMATIS dibaca sekali saat modul dimuat, jadi tiap nilai memuat ulang modulnya.
  let versi = 0;
  for (const isi of [undefined, '', '5', '6', ' 6 ', '0', '13', '12', '1', '4.5', '6.0', 'abc', '0x6', '1e1', '-3']) {
    if (isi === undefined) delete process.env.AMBANG_MMI;
    else process.env.AMBANG_MMI = isi;
    const mm = await muat('picu-otomatis', ++versi);
    rekam('picu-otomatis', 'AMBANG_MMI', { teks: isi ?? null }, mm.AMBANG_MMI);
  }
  delete process.env.AMBANG_MMI;
  for (const isi of [undefined, '0', '1', '', 'false', ' 0']) {
    if (isi === undefined) delete process.env.PICU_OTOMATIS;
    else process.env.PICU_OTOMATIS = isi;
    const mm = await muat('picu-otomatis', ++versi);
    rekam('picu-otomatis', 'PICU_OTOMATIS_AKTIF', { teks: isi ?? null }, mm.PICU_OTOMATIS_AKTIF);
  }
  delete process.env.PICU_OTOMATIS;

  // periksaPicuOtomatis dijalankan utuh dengan BMKG dan database tiruan. Yang dibandingkan hanya
  // bagian yang diporting: pemilihan gempa, keterangan, penanda kejadian, dan pesan broadcast.
  const bmkg = await muat('bmkg');
  const BMKG = (g) => ({
    Tanggal: g.tanggal, Jam: g.jam, DateTime: g.waktu, Magnitude: g.magnitudo, Kedalaman: g.kedalaman,
    Wilayah: g.wilayah, Lintang: g.lintang, Bujur: g.bujur, Potensi: g.potensi ?? undefined,
    Dirasakan: g.dirasakan ?? undefined
  });
  const skenario = [
    ['tidak ada gempa dirasakan', gempa({}), []],
    ['di bawah ambang, "v" huruf kecil tidak terbaca', gempa({ dirasakan: 'III-IV Cianjur, II-III Kota Sukabumi' }), [gempa({ dirasakan: 'v Padang, II Garut' })]],
    ['yang pertama melampaui ambang dipilih, bukan yang tertinggi', gempa({ dirasakan: 'IV Padang' }),
      [gempa({ waktu: '2026-09-17T21:00:00+00:00', wilayah: 'Pusat gempa di laut 40 km BaratDaya Kota Padang', dirasakan: 'V Kota Padang, IV Padang Pariaman' }),
        gempa({ waktu: '2026-09-17T20:00:00+00:00', wilayah: 'Pusat gempa di darat Kab. Jayawijaya', dirasakan: 'VII Jayapura' })]],
    ['waktu kosong: penanda memakai tanggal dan jam', gempa({ waktu: '', magnitudo: '6.4', wilayah: 'Pusat gempa di darat 12 km Tenggara Kab. Jayawijaya', dirasakan: 'VI-VII Jayapura, V Wamena, III Sentani' }), []],
    ['MMI IX', gempa({ dirasakan: 'IX Kab. Cianjur, VIII Kota Sukabumi' }), []],
    ['wilayah tidak dikenali', gempa({ dirasakan: 'VI Kota Antah Berantah' }), []],
    ['wilayah BMKG panjang: penanda dipotong 190 karakter', gempa({ wilayah: ulang(200, 'w'), dirasakan: 'V Padang' }), []]
  ];
  for (const [nama, terbaru, dirasakan] of skenario) {
    globalThis.fetch = async (url) => {
      const isi = url.endsWith('autogempa.json') ? { Infogempa: { gempa: BMKG(terbaru) } }
        : url.endsWith('gempadirasakan.json') ? { Infogempa: { gempa: dirasakan.map(BMKG) } }
          : url.endsWith('gempaterkini.json') ? { Infogempa: { gempa: [] } } : null;
      return isi ? { ok: true, text: async () => JSON.stringify(isi) } : { ok: false, text: async () => '' };
    };
    let dibuat = null;
    globalThis.__prismaPembanding = {
      ...prismaKantor(GEDUNG),
      activeBroadcast: { findFirst: async () => null, create: async ({ data }) => { dibuat = data; return { id: 'bc-uji' }; } },
      unit: { findFirst: async () => ({ id: 'unit-sistem' }) },
      user: { findUnique: async () => ({ id: 'akun-sistem' }) }
    };
    const calon = await denganJam(T0, async () => { const d = await bmkg.ambilDataBmkg(); return [d.terbaru, ...d.dirasakan]; });
    const hasil = await denganJam(T0, () => m.periksaPicuOtomatis());
    delete globalThis.__prismaPembanding;
    rekam('picu-otomatis', 'periksaPicuOtomatis', { nama, calon }, {
      status: hasil.status, keterangan: hasil.keterangan, mmi: hasil.mmi ?? null,
      dibuat: dibuat && {
        jenisBencana: dibuat.jenisBencana, kategoriBencana: dibuat.kategoriBencana, pesan: dibuat.pesan,
        sumberKejadian: dibuat.sumberKejadian, mmiTertinggi: dibuat.mmiTertinggi
      }
    });
  }
}

// ── Tulis ────────────────────────────────────────────────────────────────────────────────────
// ASCII saja: karakter di luar ASCII ditulis \uXXXX supaya fikstur tidak memuat karakter tak terlihat.
mkdirSync(KELUARAN, { recursive: true });
const ascii = (teks) => teks.replace(/[^\x00-\x7e]/g, (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0'));
for (const [modul, isi] of Object.entries(berkas)) {
  writeFileSync(path.join(KELUARAN, `${modul}.json`), ascii(JSON.stringify(isi, null, 2)) + '\n');
  const jumlah = Object.values(isi.kasus).reduce((a, k) => a + k.length, 0);
  console.log(`${modul}.json  ${jumlah} kasus`);
}
