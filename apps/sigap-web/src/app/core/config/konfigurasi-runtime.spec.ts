import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { API_BASE_URL, KEYCLOAK_ISSUER_URL, VAPID_PUBLIC_KEY } from './api-config';
import { konfigurasiRuntime, muatKonfigurasiRuntime } from './konfigurasi-runtime';

const alamat = new URL('http://remote.uji/config.json');
const jawab = (status: number, isi?: unknown) =>
  vi
    .fn()
    .mockResolvedValue(new Response(isi === undefined ? null : JSON.stringify(isi), { status }));

describe('konfigurasi runtime', () => {
  afterEach(async () => {
    await muatKonfigurasiRuntime(alamat, jawab(404));
    TestBed.resetTestingModule();
  });

  it('tanpa config.json (404) memakai nilai bawaan pengembangan', async () => {
    await muatKonfigurasiRuntime(alamat, jawab(404));

    expect(konfigurasiRuntime()).toEqual({});
    expect(TestBed.inject(API_BASE_URL)).toBe('http://localhost:5299');
  });

  it('nilai dari config.json dipakai token, string kosong dianggap tidak diisi', async () => {
    await muatKonfigurasiRuntime(
      alamat,
      jawab(200, {
        apiBaseUrl: 'https://api.contoh',
        ssoIssuerUrl: '',
        vapidPublicKey: 'BPk',
        lain: 'x',
      }),
    );

    expect(konfigurasiRuntime()).toEqual({
      apiBaseUrl: 'https://api.contoh',
      vapidPublicKey: 'BPk',
    });
    expect(TestBed.inject(API_BASE_URL)).toBe('https://api.contoh');
    expect(TestBed.inject(KEYCLOAK_ISSUER_URL)).toBe('http://localhost:8081/realms/kemenkeu');
    expect(TestBed.inject(VAPID_PUBLIC_KEY)).toBe('BPk');
  });

  it('config.json yang ada tetapi gagal dibaca menghentikan aplikasi, bukan jatuh ke nilai bawaan', async () => {
    await expect(muatKonfigurasiRuntime(alamat, jawab(500))).rejects.toThrow(/status 500/);
    await expect(muatKonfigurasiRuntime(alamat, jawab(200, ['bukan', 'objek']))).rejects.toThrow(
      /objek/,
    );
    await expect(muatKonfigurasiRuntime(alamat, jawab(200, { apiBaseUrl: 5299 }))).rejects.toThrow(
      /apiBaseUrl/,
    );
  });

  it('dibaca tanpa cache', async () => {
    const ambil = jawab(404);

    await muatKonfigurasiRuntime(alamat, ambil);

    expect(ambil).toHaveBeenCalledWith(alamat, { cache: 'no-store' });
  });
});
