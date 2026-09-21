// Tes untuk pemeriksa lintas platform. Jalankan:  npm run test:skrip
//
// Setiap pemeriksa diuji dua arah: yang salah HARUS tertangkap, dan yang benar TIDAK BOLEH
// ikut tertangkap. Pemeriksa yang tidak pernah ditunjukkan bisa menolak sesuatu bisa saja
// tidak berbuat apa-apa.

import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  indeksBukanLf,
  masalahPathCsharp,
  masalahSkripNpm,
  tabrakanKapitalisasi,
} from './periksa-repo.mjs';

const eol = (...baris) => baris.join('\0');

test('akhir baris: CRLF di index ditangkap, CRLF di folder kerja tidak', () => {
  const keluaran = eol(
    'i/crlf   w/crlf   attr/text eol=lf      \tsrc/a.ts',
    'i/mixed  w/lf     attr/text=auto eol=lf \tsrc/b.ts',
    'i/lf     w/crlf   attr/text eol=lf      \ttsconfig.json', // scaffold Windows — aman
    'i/lf     w/lf     attr/text eol=lf      \tsrc/c.ts',
  );
  assert.deepEqual(
    indeksBukanLf(keluaran).map((m) => `${m.path}:${m.keadaan}`),
    ['src/a.ts:crlf', 'src/b.ts:mixed'],
  );
});

test('akhir baris: .cmd dan .ps1 memang CRLF (sesuai .gitattributes)', () => {
  const keluaran = eol(
    'i/crlf   w/crlf   attr/text eol=crlf    \trun.cmd',
    'i/crlf   w/crlf   attr/text eol=crlf    \tsetup.ps1',
  );
  assert.deepEqual(indeksBukanLf(keluaran), []);
});

test('kapitalisasi: dua berkas yang hanya beda huruf besar/kecil ditangkap', () => {
  const hasil = tabrakanKapitalisasi(['src/App.ts', 'src/app.ts', 'src/lain.ts', 'README.md']);
  assert.equal(hasil.length, 1);
  assert.match(hasil[0], /src\/App\.ts/);
});

test('kapitalisasi: nama berbeda sungguhan tidak dianggap tabrakan', () => {
  assert.deepEqual(tabrakanKapitalisasi(['src/a.ts', 'src/b.ts', 'src/A/b.ts']), []);
});

const skrip = (nilai) => masalahSkripNpm({ scripts: { x: nilai } });

test('skrip npm: yang hanya jalan di bash ditangkap', () => {
  for (const salah of [
    'rm -rf dist',
    'NODE_ENV=production ng build',
    'npm run lint && NODE_ENV=test vitest',
    'mkdir -p dist/a',
    'cp a b',
    'export FOO=bar && node x.js',
    'node scripts\\build.mjs',
  ]) {
    assert.ok(skrip(salah).length > 0, `seharusnya ditangkap: ${salah}`);
  }
});

test('skrip npm: yang lintas platform tidak ikut ditangkap', () => {
  for (const benar of [
    'rimraf dist .angular',
    'cross-env NODE_ENV=production ng build',
    'node scripts/generate.mjs',
    'eslint . && prettier --check .',
    'stylelint "src/**/*.scss"',
    'ng build --configuration production',
  ]) {
    assert.deepEqual(skrip(benar), [], `seharusnya lolos: ${benar}`);
  }
});

const cs = (baris) => masalahPathCsharp(baris);

test('path C#: gabung string manual dan path Windows literal ditangkap', () => {
  assert.equal(cs('var p = dir + "/" + nama;').length, 1);
  assert.equal(cs('var p = dir + "\\\\" + nama;').length, 1);
  assert.equal(cs('var p = "wwwroot\\\\css\\\\site.css";').length, 1);
  assert.equal(cs('var p = @"C:\\dev\\sigap";').length, 1);
});

test('path C#: Path.Combine, regex, escape biasa, dan komentar tidak ikut ditangkap', () => {
  assert.deepEqual(cs('var p = Path.Combine(dir, nama);'), []);
  assert.deepEqual(cs('[GeneratedRegex(@"\\s+")]'), []);
  assert.deepEqual(cs('var r = new Regex("\\\\d+\\\\s*");'), []);
  assert.deepEqual(cs('var s = "baris\\nbaru";'), []);
  assert.deepEqual(cs('// dir + "/" + nama, hanya contoh'), []);
  assert.deepEqual(cs('var u = "/api/v1/" + id;'), []); // URL, bukan path berkas
});

test('path C#: pengecualian eksplisit dihormati', () => {
  assert.deepEqual(cs('var p = a + "/" + b; // periksa-repo: izinkan URL, bukan path berkas'), []);
});
