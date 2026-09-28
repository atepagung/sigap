// Tes generator koordinat dummy. Jalankan: node --test infra/kantor-bmn-seed/generate.spec.mjs
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { generateKoordinat } from './generate.mjs';
import { PROVINSI_BBOX } from './provinsi-bbox.mjs';

const contoh = (over = {}) => ({
  register: 'REG-1', provinsi: 'DKI Jakarta', lintang: null, bujur: null, ...over,
});

test('baris tanpa koordinat asli ditandai dummy dan jatuh di dalam bbox provinsinya', () => {
  const { baris } = generateKoordinat([contoh()]);
  const [b] = baris;
  assert.equal(b.isKoordinatDummy, true);
  const [utara, selatan, barat, timur] = PROVINSI_BBOX['DKI Jakarta'];
  assert.ok(b.lintang <= utara && b.lintang >= selatan, `lintang ${b.lintang} di luar bbox`);
  assert.ok(b.bujur >= barat && b.bujur <= timur, `bujur ${b.bujur} di luar bbox`);
});

test('baris yang SUDAH punya koordinat asli tidak diubah dan tidak ditandai dummy', () => {
  const { baris } = generateKoordinat([contoh({ lintang: -6.2, bujur: 106.8 })]);
  assert.deepEqual(baris[0].lintang, -6.2);
  assert.deepEqual(baris[0].bujur, 106.8);
  assert.equal(baris[0].isKoordinatDummy, false);
});

test('register yang sama selalu menghasilkan koordinat yang sama (deterministik)', () => {
  const a = generateKoordinat([contoh()]).baris[0];
  const b = generateKoordinat([contoh()]).baris[0];
  assert.deepEqual(a.lintang, b.lintang);
  assert.deepEqual(a.bujur, b.bujur);
});

test('register berbeda menghasilkan koordinat berbeda (tersebar, tidak menumpuk satu titik)', () => {
  const baris = generateKoordinat([contoh({ register: 'A' }), contoh({ register: 'B' }), contoh({ register: 'C' })]).baris;
  const unik = new Set(baris.map((b) => `${b.lintang},${b.bujur}`));
  assert.equal(unik.size, 3);
});

test('provinsi tanpa bbox dikenal: koordinat dibiarkan kosong, TIDAK mengarang titik', () => {
  const { baris, provinsiTakDikenal } = generateKoordinat([contoh({ provinsi: 'Provinsi Fiktif' })]);
  assert.equal(baris[0].lintang, null);
  assert.equal(baris[0].bujur, null);
  assert.equal(baris[0].isKoordinatDummy, false);
  assert.equal(baris[0].provinsiTakDikenalUntukDummy, true);
  assert.deepEqual(provinsiTakDikenal, ['Provinsi Fiktif']);
});

test('seluruh 34 provinsi di data prototipe (termasuk "P A P U A") punya bbox terdaftar', () => {
  const provinsiData = [
    'Aceh', 'Bali', 'Banten', 'Bengkulu', 'DKI Jakarta', 'Daerah Istimewa Yogyakarta',
    'Gorontalo', 'Jambi', 'Jawa Barat', 'Jawa Tengah', 'Jawa Timur', 'Kalimantan Barat',
    'Kalimantan Selatan', 'Kalimantan Tengah', 'Kalimantan Timur', 'Kalimantan Utara',
    'Kepulauan Bangka Belitung', 'Kepulauan Riau', 'Lampung', 'Maluku', 'Maluku Utara',
    'Nusa Tenggara Barat', 'Nusa Tenggara Timur', 'P A P U A', 'Papua Barat', 'Riau',
    'Sulawesi Barat', 'Sulawesi Selatan', 'Sulawesi Tengah', 'Sulawesi Tenggara',
    'Sulawesi Utara', 'Sumatera Barat', 'Sumatera Selatan', 'Sumatera Utara',
  ];
  assert.equal(provinsiData.length, 34);
  for (const p of provinsiData) {
    assert.ok(PROVINSI_BBOX[p], `bbox tidak ada untuk provinsi "${p}"`);
  }
});

test('setiap bbox valid: utara > selatan, timur > barat, dalam rentang koordinat Indonesia', () => {
  for (const [nama, [utara, selatan, barat, timur]] of Object.entries(PROVINSI_BBOX)) {
    assert.ok(utara > selatan, `${nama}: utara harus > selatan`);
    assert.ok(timur > barat, `${nama}: timur harus > barat`);
    assert.ok(lintangDalamIndonesia(utara) && lintangDalamIndonesia(selatan), `${nama}: lintang di luar rentang Indonesia`);
    assert.ok(bujurDalamIndonesia(barat) && bujurDalamIndonesia(timur), `${nama}: bujur di luar rentang Indonesia`);
  }
});

function lintangDalamIndonesia(l) { return l >= -11.5 && l <= 6.5; }
function bujurDalamIndonesia(b) { return b >= 94.5 && b <= 141.5; }
