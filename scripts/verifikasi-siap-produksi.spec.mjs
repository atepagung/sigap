// Tes untuk gerbang kesiapan produksi. Jalankan:  npm run test:skrip

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { aliasDummyWeb, seamLoginDev, tafsirkanPublishApi } from './verifikasi-siap-produksi.mjs';

test('aliasDummyWeb: menangkap alias yang menunjuk ke dummy, mengabaikan yang tidak', () => {
  const hasil = aliasDummyWeb({
    compilerOptions: {
      paths: {
        '@danarakca/keu-ui': ['./libs/keu-ui-dummy/index.ts'],
        '@danarakca/iam': ['./libs/iam-dummy-web/index.ts'],
        '@shared/utils': ['./libs/shared/utils.ts'],
      },
    },
  });
  assert.deepEqual(
    hasil.map((h) => h.alias),
    ['@danarakca/keu-ui', '@danarakca/iam'],
  );
});

test('aliasDummyWeb: tsconfig tanpa paths dummy dianggap siap', () => {
  const hasil = aliasDummyWeb({
    compilerOptions: { paths: { '@danarakca/keu-ui': ['./node_modules/@danarakca/keu-ui'] } },
  });
  assert.deepEqual(hasil, []);
});

test('tafsirkanPublishApi: publish sukses berarti siap', () => {
  assert.deepEqual(tafsirkanPublishApi(0, 'Build succeeded.'), {
    siap: true,
    dummyTerpasang: null,
    galatLain: null,
  });
});

test('tafsirkanPublishApi: SIGAP001 dibaca sebagai dummy terpasang, bukan galat lain', () => {
  const keluaran =
    'error SIGAP001: Publish dibatalkan: masih ada dummy yang ter-resolve — Kemenkeu.Iam.Dummy, Sigap.Notifikasi.Dummy. Ganti ProjectReference dummy dengan paket platform yang asli lebih dulu (lihat DUMMY_REGISTRY.md bagian 7, Urutan penukaran).';
  const hasil = tafsirkanPublishApi(1, keluaran);
  assert.equal(hasil.siap, false);
  assert.deepEqual(hasil.dummyTerpasang, ['Kemenkeu.Iam.Dummy', 'Sigap.Notifikasi.Dummy']);
  assert.equal(hasil.galatLain, null);
});

test('tafsirkanPublishApi: kegagalan tanpa SIGAP001 dianggap galat lain, bukan status dummy', () => {
  const hasil = tafsirkanPublishApi(1, 'error MSB3021: Unable to copy file, file locked.');
  assert.equal(hasil.siap, false);
  assert.equal(hasil.dummyTerpasang, null);
  assert.match(hasil.galatLain, /MSB3021/);
});

test('seamLoginDev: password grant dan client dev ditangkap, berkas tes diabaikan', () => {
  const hasil = seamLoginDev([
    {
      path: 'apps/sigap-web/src/app/core/auth/token-provider.ts',
      teks: "const body = new URLSearchParams({ grant_type: 'password', client_id: this.clientId });",
    },
    {
      path: 'apps/sigap-web/src/app/core/config/api-config.ts',
      teks: "export const KEYCLOAK_DEV_CLIENT_ID = new InjectionToken<string>('x');",
    },
    {
      path: 'apps/sigap-web/src/app/core/auth/token-provider.spec.ts',
      teks: "expect(body.get('grant_type')).toBe('password'); KEYCLOAK_DEV_CLIENT_ID",
    },
  ]);
  assert.deepEqual(
    hasil.map((h) => h.path),
    [
      'apps/sigap-web/src/app/core/auth/token-provider.ts',
      'apps/sigap-web/src/app/core/config/api-config.ts',
    ],
  );
});

test('seamLoginDev: kode tanpa seam (mis. token dari shell) dianggap siap', () => {
  const hasil = seamLoginDev([
    {
      path: 'apps/sigap-web/src/app/core/auth/auth.interceptor.ts',
      teks: "req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }); // grant_type: 'authorization_code'",
    },
  ]);
  assert.deepEqual(hasil, []);
});
