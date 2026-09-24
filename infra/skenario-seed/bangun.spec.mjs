// Jalankan: node --test infra/skenario-seed/bangun.spec.mjs
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { bangunSkenario, ID, URUTAN_TABEL } from './bangun.mjs';

const OPSI = new URL('../../apps/sigap-api/src/Sigap.Domain/Asesmen/OpsiAsesmen.cs', import.meta.url);
const sekarang = new Date('2026-09-24T10:00:00.000Z');
const pegawaiIds = Array.from({ length: 13 }, (_, i) => `p${i}`);
const buat = () => bangunSkenario({ sekarang, unitId: 'U', satgasId: 'S', pegawaiIds });

test('setiap tabel di URUTAN_TABEL ada isinya dan semuanya isDemo', () => {
  const s = buat();
  for (const tabel of URUTAN_TABEL) {
    assert.ok(s[tabel].length > 0, `${tabel} kosong`);
    assert.ok(s[tabel].every((b) => b.isDemo === true), `${tabel} memuat baris non-demo`);
  }
  assert.deepEqual(Object.keys(s).sort(), [...URUTAN_TABEL].sort());
});

test('13 pegawai: 3 belum menjawab (yang pertama), 8 aman, 2 butuh bantuan; tak ada jawaban ganda', () => {
  const j = buat().SafetyCheckResponse;
  assert.equal(j.length, 10);
  assert.equal(j.filter((x) => x.status === 'AMAN').length, 8);
  assert.equal(j.filter((x) => x.status === 'BUTUH_BANTUAN').length, 2);
  assert.equal(new Set(j.map((x) => x.userId)).size, j.length);
  assert.ok(!j.some((x) => ['p0', 'p1', 'p2'].includes(x.userId)), 'tiga pertama harus belum menjawab');
});

test('keterangan hanya pada jawaban yang dicatatkan Satgas (keadaan yang benar-benar dihasilkan API)', () => {
  for (const j of buat().SafetyCheckResponse) {
    assert.equal(j.keterangan !== null, j.dicatatOlehId !== null, `${j.id}: keterangan dan pencatat harus sepaket`);
    if (j.dicatatOlehId) assert.equal(j.dicatatOlehId, 'S');
  }
});

test('jejak pemicu berbentuk "{peran}|{profil}|{unitId}" seperti yang dibaca BroadcastStore, dan menunjuk broadcast', () => {
  const s = buat();
  const [j] = s.JejakPerubahan;
  assert.equal(s.JejakPerubahan.length, 1);
  assert.equal(j.entitas, 'ActiveBroadcast');
  assert.equal(j.aksi, 'DIPICU');
  assert.equal(j.entitasId, s.ActiveBroadcast[0].id);
  assert.equal(j.olehId, s.ActiveBroadcast[0].dikirimOlehId);
  assert.deepEqual(j.alasan.split('|'), ['SATGAS', 'UNIT', s.ActiveBroadcast[0].targetUnitId]);
});

test('tiap versi asesmen berpasangan: createdAt SAMA PERSIS, unit dan pengirim sama (aturan S5)', () => {
  const s = buat();
  for (const n of [1, 2]) {
    const d = s.DamageAssessment.find((x) => x.id === `skenario-asesmen-v${n}`);
    const c = s.ChecklistKondisiLapangan.find((x) => x.id === `skenario-checklist-v${n}`);
    assert.equal(d.createdAt.getTime(), c.createdAt.getTime());
    assert.equal(d.unitId, c.unitId);
    assert.equal(d.submittedById, c.submittedById);
  }
});

test('asesmen dikirim selagi broadcast berjalan, sehingga masuk satu seri; tidak ada yang di masa depan', () => {
  const s = buat();
  const mulai = s.ActiveBroadcast[0].createdAt;
  for (const d of s.DamageAssessment) {
    assert.ok(d.createdAt > mulai && d.createdAt < sekarang);
    assert.ok(d.waktuKejadian <= d.createdAt);
  }
  assert.ok(s.DamageAssessment[0].createdAt < s.DamageAssessment[1].createdAt);
  assert.equal(s.BroadcastSasaranUnit[0].status, 'DISASAR');
  assert.equal(s.BroadcastSasaranUnit[0].createdAt.getTime(), mulai.getTime());
});

test('semua nilai berskala adalah label yang dikenal OpsiAsesmen.cs', () => {
  const label = new Set([...readFileSync(OPSI, 'utf8').matchAll(/new\("[A-Z0-9_]+",\s*"([^"]+)"\)/g)].map((m) => m[1]));
  assert.ok(label.size > 40, 'pembacaan OpsiAsesmen.cs gagal');
  const s = buat();
  const bukanSkala = new Set(['id', 'isDemo', 'unitId', 'submittedById', 'createdAt']);
  const catatan = /Catatan$/;
  for (const c of s.ChecklistKondisiLapangan) {
    for (const [k, v] of Object.entries(c)) {
      if (bukanSkala.has(k) || catatan.test(k) || v === null) continue;
      assert.ok(label.has(v), `${k}="${v}" bukan label di OpsiAsesmen.cs`);
    }
  }
  for (const d of s.DamageAssessment) assert.ok(label.has(d.kondisiFisik), `kondisiFisik="${d.kondisiFisik}"`);
});

test('layananTerdampak: status sah, id menunjuk LayananKritis skenario, gangguan cocok dengan versi terbaru', () => {
  const s = buat();
  const idLayanan = new Set(s.LayananKritis.map((l) => l.id));
  for (const d of s.DamageAssessment) {
    for (const l of JSON.parse(d.layananTerdampak)) {
      assert.ok(idLayanan.has(l.id));
      assert.ok(['NORMAL', 'TERGANGGU', 'BERHENTI_TOTAL'].includes(l.status));
    }
  }
  const terbaru = JSON.parse(s.DamageAssessment[1].layananTerdampak).find((l) => l.id === ID.layananSistem);
  assert.equal(terbaru.status, 'TERGANGGU');
  assert.equal(s.GangguanLayanan[0].layananId, ID.layananSistem);
});

test('waktu bergeser mengikuti `sekarang` (seeding ulang menyegarkan skenario)', () => {
  const a = buat();
  const b = bangunSkenario({ sekarang: new Date(sekarang.getTime() + 3_600_000), unitId: 'U', satgasId: 'S', pegawaiIds });
  assert.equal(b.ActiveBroadcast[0].createdAt - a.ActiveBroadcast[0].createdAt, 3_600_000);
});

test('pegawai terlalu sedikit ditolak, bukan diam-diam menghasilkan rekap tak bermakna', () => {
  assert.throws(() => bangunSkenario({ sekarang, unitId: 'U', satgasId: 'S', pegawaiIds: ['a', 'b'] }), /minimal 6/);
});
