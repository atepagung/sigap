// Jalankan: node --test infra/organisasi-seed/bangun.spec.mjs
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { test } from 'node:test';
import { AKUN_LAYANAN_BMKG, UNIT_DEMO, bangunAkunUji, bangunSemua, bangunUnitOtk } from './bangun.mjs';

const OTK_ASLI = 'C:/dev/MKB APPS/App/data/otk_bundle.json';
const REALM = new URL('../keycloak/import/kemenkeu-realm.json', import.meta.url);

const unit = (id, parent, level, jenis, unit_key) => ({ id, parent, level, jenis, unit_key, nama: unit_key });
const kecil = {
  otk: {
    unit: [
      unit(0, null, 0, 'Kementerian', 'kemenkeu'),
      unit(1, 0, 1, 'Eselon I', 'djp'),
      unit(2, 1, 2, 'Eselon II', 'djp.sekretariat'),
      unit(3, 2, 3, 'Eselon III', 'djp.sekretariat.bagian'),
      unit(4, 3, 4, 'Eselon IV', 'djp.sekretariat.bagian.subbagian'),
      unit(5, 0, 1, 'Staf Ahli', 'staf-ahli'),
    ],
  },
};

test('unit OTK dimuat sampai Eselon III, Eselon IV tidak', () => {
  const hasil = bangunUnitOtk(kecil);
  assert.deepEqual(hasil.map((u) => u.kode), ['kemenkeu', 'djp', 'djp.sekretariat', 'djp.sekretariat.bagian', 'staf-ahli']);
});

test('eselonIKey menelusuri induk sampai level 1; Kementerian tidak punya', () => {
  const per = Object.fromEntries(bangunUnitOtk(kecil).map((u) => [u.kode, u]));
  assert.equal(per.kemenkeu.eselonIKey, null);
  assert.equal(per.djp.eselonIKey, 'djp');
  assert.equal(per['djp.sekretariat.bagian'].eselonIKey, 'djp');
  assert.equal(per['staf-ahli'].eselonIKey, 'staf-ahli');
  assert.equal(per['staf-ahli'].tingkat, 'STAF_AHLI');
});

test('relasi induk memakai id turunan kode, dan unit OTK bukan demo', () => {
  const per = Object.fromEntries(bangunUnitOtk(kecil).map((u) => [u.kode, u]));
  assert.equal(per.kemenkeu.parentUnitId, null);
  assert.equal(per['djp.sekretariat'].parentUnitId, per.djp.id);
  assert.ok(bangunUnitOtk(kecil).every((u) => u.isDemo === false));
});

test('unit_key ganda ditolak karena kolom kode harus unik', () => {
  const ganda = { otk: { unit: [unit(0, null, 0, 'Kementerian', 'x'), unit(1, 0, 1, 'Eselon I', 'x')] } };
  assert.throws(() => bangunUnitOtk(ganda), /tidak unik/);
});

test('akun uji dengan unit tak dikenal ditolak, bukan dibuatkan unit diam-diam', () => {
  const realm = { users: [{ username: 'a', attributes: { nip: ['9'], kode_satker: ['tidak-ada'] }, groups: ['/sigap-pegawai'] }] };
  assert.throws(() => bangunAkunUji(realm, new Map()), /tidak ada di OTK/);
});

test('grup SSO tak dikenal ditolak', () => {
  const realm = { users: [{ username: 'a', attributes: { nip: ['9'], kode_satker: ['k'] }, groups: ['/aneh'] }] };
  assert.throws(() => bangunAkunUji(realm, new Map([['k', { id: 'u' }]])), /tidak dikenal/);
});

test('sepuluh akun realm menjadi sepuluh pengguna, satu peran tiap akun, semuanya demo', {
  skip: !existsSync(OTK_ASLI) && 'otk_bundle.json tidak ada di jalur default',
}, () => {
  const { unit: semua, pengguna } = bangunSemua(JSON.parse(readFileSync(OTK_ASLI, 'utf8')), JSON.parse(readFileSync(REALM, 'utf8')));
  const idUnit = new Set(semua.map((u) => u.id));
  assert.equal(new Set(semua.map((u) => u.kode)).size, semua.length);
  assert.ok(pengguna.every((p) => idUnit.has(p.unitId)), 'setiap pengguna menunjuk unit yang ada');
  assert.equal(new Set(pengguna.map((p) => p.nip)).size, pengguna.length, 'NIP unik');
  const akun = pengguna.filter((p) => p.nip < '900000000000000100');
  assert.equal(akun.length, 10);
  assert.ok(akun.every((p) => p.peran.length === 1));
  // Subkoordinator, Koordinator, dan Sekjen bergantung pada unit OTK asli.
  const per = Object.fromEntries(pengguna.map((p) => [p.nip, p]));
  assert.equal(per['900000000000000005'].unitId, 'otk-djp');
  assert.equal(per['900000000000000007'].unitId, 'otk-setjen');
});

const APPSETTINGS = new URL('../../apps/sigap-api/src/Sigap.Api/appsettings.json', import.meta.url);

test('NIP akun layanan sama persis dengan Bmkg:NipLayanan di konfigurasi API', () => {
  // appsettings.json memuat komentar (bukan JSON murni), jadi dibaca dengan pola. Bila keduanya berbeda,
  // worker tidak menemukan akunnya dan pemicu otomatis diam-diam tidak berjalan.
  const teks = readFileSync(APPSETTINGS, 'utf8');
  const nip = teks.match(/"Bmkg"\s*:\s*\{[^}]*?"NipLayanan"\s*:\s*"([^"]+)"/s)?.[1];
  assert.ok(nip, 'Bmkg:NipLayanan tidak ditemukan di appsettings.json');
  assert.equal(AKUN_LAYANAN_BMKG.nip, nip);
});

test('akun layanan terbaca dari builder: di unit akar, bukan pegawai, dan tanpa satu pun peran', {
  skip: !existsSync(OTK_ASLI) && 'otk_bundle.json tidak ada di jalur default',
}, () => {
  const { unit, pengguna, layanan } = bangunSemua(JSON.parse(readFileSync(OTK_ASLI, 'utf8')), JSON.parse(readFileSync(REALM, 'utf8')));
  assert.equal(layanan.nip, AKUN_LAYANAN_BMKG.nip);
  assert.equal(layanan.unitId, unit.find((u) => u.kode === 'kemenkeu').id);
  assert.ok(!pengguna.some((p) => p.nip === layanan.nip), 'akun layanan tidak boleh ikut daftar pengguna berperan');
  assert.ok(!('peran' in layanan), 'akun layanan tidak punya peran (penyebut rekap membaca UserRole PEGAWAI)');
});

test('unit demo Pekanbaru berkabupaten/kota, unit OTK tidak (data OTK tidak memuatnya)', () => {
  assert.ok(UNIT_DEMO.every((u) => u.kabkota === 'Kota Pekanbaru'));
  assert.ok(bangunUnitOtk(kecil).every((u) => u.kabkota === null));
});
