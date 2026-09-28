// Tes untuk pemarsing status dummy. Jalankan:  npm run test:skrip

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { daftarDummy, ringkasStatus } from './status-dummy.mjs';

const CONTOH = `# DUMMY_REGISTRY

## 1. Registry

### 1.1 \`libs/keu-ui-dummy\`

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | \`libs/keu-ui-dummy\` |
| **Status** | **Aktif** (dibuat 18 Sep 2026, P3.1) |

### 1.2 \`libs/notifikasi-dummy\`

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | \`libs/notifikasi-dummy\` |
| **Status** | **Sebagian sudah ditukar (27 Sep 2026, P5.3).** Ketiga port diisi implementasi sungguhan |

## 2. Asumsi lain

### 1.9 Bukan entri registry, seharusnya tidak ikut terhitung
`;

test('daftarDummy: menemukan setiap entri 1.x dan kolom Status-nya', () => {
  const hasil = daftarDummy(CONTOH);
  assert.equal(hasil.length, 2);
  assert.equal(hasil[0].nomor, '1');
  assert.equal(hasil[0].nama, '`libs/keu-ui-dummy`');
  assert.match(hasil[0].status, /^\*\*Aktif\*\*/);
  assert.equal(hasil[1].nomor, '2');
  assert.match(hasil[1].status, /^\*\*Sebagian sudah ditukar/);
});

test('daftarDummy: berhenti di luar bagian "1. Registry"', () => {
  const hasil = daftarDummy(CONTOH);
  assert.ok(!hasil.some((d) => d.nama.includes('Bukan entri registry')));
});

test('daftarDummy: entri tanpa baris Status tidak melempar, statusnya null', () => {
  const teks = '## 1. Registry\n\n### 1.1 `x`\n\ntidak ada tabel di sini\n';
  const hasil = daftarDummy(teks);
  assert.equal(hasil.length, 1);
  assert.equal(hasil[0].status, null);
});

test('ringkasStatus: mengenali pola Aktif, Sebagian, dan tak dikenal', () => {
  assert.equal(ringkasStatus('**Aktif** (dibuat 18 Sep 2026)'), 'AKTIF');
  assert.equal(ringkasStatus('**Sebagian sudah ditukar** (27 Sep 2026)'), 'SEBAGIAN DITUKAR');
  assert.equal(ringkasStatus(null), 'TIDAK DIKETAHUI');
  assert.equal(ringkasStatus('Ditukar penuh, lihat catatan'), 'LIHAT DETAIL');
});
