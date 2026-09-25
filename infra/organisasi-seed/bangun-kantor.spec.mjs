// Jalankan: node --test infra/organisasi-seed/bangun-kantor.spec.mjs
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { PETA_ESELON1, bangunUnitKantor } from './bangun-kantor.mjs';

const g = (id, kodeSatker, kabkota, extra = {}) => ({
  id, kodeSatker, namaSatker: 'KPP Pratama Luwuk', eselon1: 'Direktorat Jenderal Pajak', provinsi: 'Sulawesi Tengah', kabkota, ...extra,
});

test('satu unit per kodeSatker, tertaut ke semua gedungnya, induk = Eselon I OTK', () => {
  const { unit, tautan } = bangunUnitKantor([
    g('a', 'S1', 'Kab. Banggai'),
    g('b', 'S1', 'Kab. Banggai'),
    g('c', 'S2', 'Kab. Banggai', { namaSatker: 'KPPN Luwuk', eselon1: 'Direktorat Jenderal Perbendaharaan' }),
  ]);
  assert.equal(unit.length, 2);
  assert.deepEqual(unit.map((u) => [u.kode, u.parentUnitId, u.isDemo, u.tingkat]), [
    ['DEMO-SATKER-S1', 'otk-djp', true, 'INSTANSI_VERTIKAL'],
    ['DEMO-SATKER-S2', 'otk-djpb', true, 'INSTANSI_VERTIKAL'],
  ]);
  assert.deepEqual(tautan.filter((t) => t.unitKode === 'DEMO-SATKER-S1').map((t) => t.kantorId), ['a', 'b']);
  assert.equal(unit[0].kabkota, 'Kab. Banggai');
  assert.equal(unit[0].tipe, 'KPP');
});

test('satker di dua kabkota: yang terbanyak dipakai dan dilaporkan; seri deterministik menurut abjad', () => {
  const { unit, laporan } = bangunUnitKantor([g('a', 'S1', 'Kota B'), g('b', 'S1', 'Kota A'), g('c', 'S1', 'Kota B')]);
  assert.equal(unit[0].kabkota, 'Kota B');
  assert.deepEqual(laporan.lebihDariSatuKabkota, [{ kodeSatker: 'S1', kabkota: ['Kota A', 'Kota B'] }]);
  const seri = bangunUnitKantor([g('a', 'S1', 'Kota B'), g('b', 'S1', 'Kota A')]);
  assert.equal(seri.unit[0].kabkota, 'Kota A');
});

test('baris tak lengkap atau Eselon I tak dikenal dilewati dan dilaporkan, bukan ditebak', () => {
  const { unit, tautan, laporan } = bangunUnitKantor([
    g('a', null, 'Kab. X'),
    g('b', 'S1', null),
    g('c', 'S2', 'Kab. X', { namaSatker: null }),
    g('d', 'S3', 'Kab. X', { eselon1: 'Badan Misterius' }),
    g('e', 'S4', 'Kab. X'),
  ]);
  assert.deepEqual(unit.map((u) => u.kode), ['DEMO-SATKER-S4']);
  assert.deepEqual(tautan.map((t) => t.kantorId), ['e']);
  assert.deepEqual(laporan.tanpaData, ['a', 'b', 'c', 'd']);
  assert.deepEqual(laporan.eselonTakDikenal, [['Badan Misterius', 1]]);
});

test('hasil tidak bergantung urutan baris masukan', () => {
  const baris = [g('a', 'S2', 'Kota B'), g('b', 'S1', 'Kota A'), g('c', 'S2', 'Kota A')];
  assert.deepEqual(bangunUnitKantor(baris).unit, bangunUnitKantor([...baris].reverse()).unit);
});

test('delapan Eselon I data BMN terpetakan', () => {
  assert.equal(Object.keys(PETA_ESELON1).length, 8);
});
