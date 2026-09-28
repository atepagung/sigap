import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { VAPID_PUBLIC_KEY } from '../core/config/api-config';
import { LanggananPush } from './langganan-push.service';
import { NotifikasiService } from './notifikasi.service';
import { PushPeramban } from './push-peramban';
import { KUNCI_UJI } from '../shared/testing/kunci-vapid-uji';

const ENDPOINT = 'https://push.uji.invalid/abc';

function langgananTiruan() {
  return {
    endpoint: ENDPOINT,
    toJSON: () => ({ endpoint: ENDPOINT, keys: { p256dh: 'kunci-p256dh', auth: 'kunci-auth' } }),
    unsubscribe: vi.fn().mockResolvedValue(true),
  };
}

interface Opsi {
  didukung?: boolean;
  izin?: NotificationPermission;
  izinSetelahDiminta?: NotificationPermission;
  registrasi?: boolean;
  langganan?: ReturnType<typeof langgananTiruan> | null;
  kunci?: string;
}

function siapkan(opsi: Opsi = {}) {
  const langganan = { nilai: opsi.langganan ?? null };
  const dibuat = langgananTiruan();
  const pushManager = {
    getSubscription: vi.fn().mockImplementation(() => Promise.resolve(langganan.nilai)),
    subscribe: vi.fn().mockImplementation(() => {
      langganan.nilai = dibuat;
      return Promise.resolve(dibuat);
    }),
  };
  const peramban = {
    didukung: vi.fn().mockReturnValue(opsi.didukung ?? true),
    izin: vi.fn().mockReturnValue(opsi.izin ?? 'default'),
    mintaIzinAsync: vi.fn().mockResolvedValue(opsi.izinSetelahDiminta ?? 'granted'),
    registrasiAsync: vi
      .fn()
      .mockResolvedValue(opsi.registrasi === false ? undefined : { pushManager }),
  };
  const notifikasi = {
    langgananAsync: vi.fn().mockResolvedValue({}),
    hapusLanggananAsync: vi.fn().mockResolvedValue({}),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: PushPeramban, useValue: peramban },
      { provide: NotifikasiService, useValue: notifikasi },
      { provide: VAPID_PUBLIC_KEY, useValue: opsi.kunci ?? KUNCI_UJI },
    ],
  });
  return { layanan: TestBed.inject(LanggananPush), peramban, notifikasi, pushManager, dibuat };
}

describe('LanggananPush.periksaAsync', () => {
  it.each([
    ['peramban tanpa dukungan', { didukung: false }, 'tidak-didukung'],
    ['kunci VAPID kosong', { kunci: '  ' }, 'tidak-dikonfigurasi'],
    ['service worker belum didaftarkan shell', { registrasi: false }, 'tanpa-service-worker'],
    ['izin diblokir', { izin: 'denied' as const }, 'ditolak'],
    ['belum berlangganan', {}, 'mati'],
    ['sudah berlangganan', { langganan: langgananTiruan() }, 'aktif'],
  ] as const)('%s -> %s', async (_nama, opsi, diharapkan) => {
    const { layanan } = siapkan(opsi);

    expect(await layanan.periksaAsync()).toBe(diharapkan);
    expect(layanan.status()).toBe(diharapkan);
  });

  it('tidak meminta izin saat hanya memeriksa', async () => {
    const { layanan, peramban } = siapkan();

    await layanan.periksaAsync();

    expect(peramban.mintaIzinAsync).not.toHaveBeenCalled();
  });
});

describe('LanggananPush.aktifkanAsync', () => {
  it('meminta izin, berlangganan dengan kunci aplikasi, lalu mendaftarkan ke server', async () => {
    const { layanan, notifikasi, pushManager } = siapkan();

    expect(await layanan.aktifkanAsync()).toBe('aktif');

    const argumen = pushManager.subscribe.mock.calls[0][0] as PushSubscriptionOptionsInit;
    expect(argumen.userVisibleOnly).toBe(true);
    expect((argumen.applicationServerKey as Uint8Array).length).toBe(65);
    expect(notifikasi.langgananAsync).toHaveBeenCalledWith(ENDPOINT, 'kunci-p256dh', 'kunci-auth');
    expect(layanan.status()).toBe('aktif');
  });

  it('izin ditolak: tidak berlangganan dan tidak menghubungi server', async () => {
    const { layanan, notifikasi, pushManager } = siapkan({ izinSetelahDiminta: 'denied' });

    expect(await layanan.aktifkanAsync()).toBe('ditolak');

    expect(pushManager.subscribe).not.toHaveBeenCalled();
    expect(notifikasi.langgananAsync).not.toHaveBeenCalled();
  });

  it('dialog izin ditutup tanpa memilih: tetap mati, bukan ditolak', async () => {
    const { layanan } = siapkan({ izinSetelahDiminta: 'default' });

    expect(await layanan.aktifkanAsync()).toBe('mati');
  });

  it('server menolak: langganan di peramban dilepas lagi dan galat sampai ke pemanggil', async () => {
    const { layanan, notifikasi, dibuat } = siapkan();
    const galat = new HttpErrorResponse({ status: 500 });
    notifikasi.langgananAsync.mockRejectedValue(galat);

    await expect(layanan.aktifkanAsync()).rejects.toBe(galat);

    expect(dibuat.unsubscribe).toHaveBeenCalledOnce();
  });

  it('kunci VAPID tidak sah: galat jelas, tanpa memanggil server', async () => {
    const { layanan, notifikasi } = siapkan({ kunci: 'AAAA' });

    await expect(layanan.aktifkanAsync()).rejects.toThrow('65 byte');
    expect(notifikasi.langgananAsync).not.toHaveBeenCalled();
  });

  it.each([
    ['tidak-didukung', { didukung: false }],
    ['tidak-dikonfigurasi', { kunci: '' }],
    ['tanpa-service-worker', { registrasi: false }],
    ['ditolak', { izin: 'denied' as const }],
  ] as const)('keadaan %s: tidak meminta izin', async (status, opsi) => {
    const { layanan, peramban } = siapkan(opsi);

    expect(await layanan.aktifkanAsync()).toBe(status);
    expect(peramban.mintaIzinAsync).not.toHaveBeenCalled();
  });

  it('sudah aktif: tidak berlangganan ulang', async () => {
    const { layanan, pushManager } = siapkan({ langganan: langgananTiruan() });

    expect(await layanan.aktifkanAsync()).toBe('aktif');
    expect(pushManager.subscribe).not.toHaveBeenCalled();
  });
});

describe('LanggananPush.matikanAsync', () => {
  it('menghapus di server dulu, baru melepas di peramban', async () => {
    const langganan = langgananTiruan();
    const { layanan, notifikasi } = siapkan({ langganan });
    const urutan: string[] = [];
    notifikasi.hapusLanggananAsync.mockImplementation(() => {
      urutan.push('server');
      return Promise.resolve({});
    });
    langganan.unsubscribe.mockImplementation(() => {
      urutan.push('peramban');
      return Promise.resolve(true);
    });

    expect(await layanan.matikanAsync()).toBe('mati');

    expect(notifikasi.hapusLanggananAsync).toHaveBeenCalledWith(ENDPOINT);
    expect(urutan).toEqual(['server', 'peramban']);
  });

  it('server gagal menghapus: langganan peramban dipertahankan dan status tidak berubah', async () => {
    const langganan = langgananTiruan();
    const { layanan, notifikasi } = siapkan({ langganan });
    await layanan.periksaAsync();
    const galat = new HttpErrorResponse({ status: 500 });
    notifikasi.hapusLanggananAsync.mockRejectedValue(galat);

    await expect(layanan.matikanAsync()).rejects.toBe(galat);

    expect(langganan.unsubscribe).not.toHaveBeenCalled();
    expect(layanan.status()).toBe('aktif');
  });

  it('tidak ada langganan: tidak memanggil server', async () => {
    const { layanan, notifikasi } = siapkan();

    expect(await layanan.matikanAsync()).toBe('mati');
    expect(notifikasi.hapusLanggananAsync).not.toHaveBeenCalled();
  });
});
