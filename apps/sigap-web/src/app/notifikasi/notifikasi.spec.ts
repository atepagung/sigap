import { HttpErrorResponse } from '@angular/common/http';
import { flushAsync } from '../shared/testing/flush-async';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideIamPermissions } from '@danarakca/iam';
import { Peringatan } from './notifikasi.model';
import { LanggananPush, StatusPush } from './langganan-push.service';
import { NotifikasiService } from './notifikasi.service';
import { NotifikasiPage } from './notifikasi';

const PERINGATAN: Peringatan = {
  kode: 'SC_BELUM_DIJAWAB',
  tingkat: 'GENTING',
  judul: 'Anda belum mengonfirmasi keselamatan',
  pesan: 'Broadcast safety check sedang berjalan.',
  terkait: { jenis: 'BROADCAST', id: 'bc-1' },
};

const IZIN_LANGGANAN = 'sigap:notifikasi:subscribe';

function siapkanPanel(status: StatusPush, izin: string[]) {
  const nilai = signal<StatusPush>(status);
  const push = {
    status: nilai.asReadonly(),
    periksaAsync: vi.fn().mockResolvedValue(status),
    aktifkanAsync: vi.fn().mockImplementation(() => {
      nilai.set('aktif');
      return Promise.resolve('aktif');
    }),
    matikanAsync: vi.fn().mockImplementation(() => {
      nilai.set('mati');
      return Promise.resolve('mati');
    }),
  };
  TestBed.configureTestingModule({
    providers: [
      {
        provide: NotifikasiService,
        useValue: { peringatanAsync: vi.fn().mockResolvedValue({ data: [] }) },
      },
      { provide: LanggananPush, useValue: push },
      provideIamPermissions(() => Promise.resolve(izin)),
    ],
  });
  return push;
}

async function buka() {
  const fixture = TestBed.createComponent(NotifikasiPage);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return fixture;
}

function tombol(fixture: Awaited<ReturnType<typeof buka>>): HTMLButtonElement | null {
  return (fixture.nativeElement as HTMLElement).querySelector('section button');
}

describe('NotifikasiPage: langganan Web Push', () => {
  it('tanpa izin subscribe, panel tidak tampil sama sekali', async () => {
    const push = siapkanPanel('mati', []);

    const fixture = await buka();

    expect(fixture.nativeElement.textContent).not.toContain('Notifikasi di perangkat ini');
    expect(tombol(fixture)).toBeNull();
    expect(push.periksaAsync).toHaveBeenCalledOnce();
  });

  it('status mati: menawarkan Aktifkan, dan menekannya memanggil aktifkanAsync lalu berubah jadi Matikan', async () => {
    const push = siapkanPanel('mati', [IZIN_LANGGANAN]);
    const fixture = await buka();
    expect(tombol(fixture)?.textContent).toContain('Aktifkan notifikasi');

    tombol(fixture)?.click();
    await flushAsync();
    fixture.detectChanges();

    expect(push.aktifkanAsync).toHaveBeenCalledOnce();
    expect(tombol(fixture)?.textContent).toContain('Matikan notifikasi');
    expect(fixture.nativeElement.textContent).toContain('Aktif:');
  });

  it('status aktif: menekan tombol memanggil matikanAsync', async () => {
    const push = siapkanPanel('aktif', [IZIN_LANGGANAN]);
    const fixture = await buka();

    tombol(fixture)?.click();
    await flushAsync();
    fixture.detectChanges();

    expect(push.matikanAsync).toHaveBeenCalledOnce();
    expect(tombol(fixture)?.textContent).toContain('Aktifkan notifikasi');
  });

  it.each([
    ['tidak-didukung', 'tidak mendukung'],
    ['tidak-dikonfigurasi', 'belum diaktifkan'],
    ['tanpa-service-worker', 'service worker'],
    ['ditolak', 'Diblokir'],
  ] as const)('status %s: menjelaskan keadaannya tanpa tombol', async (status, kata) => {
    siapkanPanel(status, [IZIN_LANGGANAN]);

    const fixture = await buka();

    expect(fixture.nativeElement.textContent).toContain(kata);
    expect(tombol(fixture)).toBeNull();
  });

  it('galat saat mengaktifkan tampil sebagai pesan, bukan galat tak tertangani', async () => {
    const push = siapkanPanel('mati', [IZIN_LANGGANAN]);
    push.aktifkanAsync.mockRejectedValue(new HttpErrorResponse({ status: 500 }));
    const fixture = await buka();

    tombol(fixture)?.click();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain(
      'Aksi gagal',
    );
    expect(tombol(fixture)?.disabled).toBe(false);
  });
});

describe('NotifikasiPage', () => {
  it('menampilkan peringatan dari service', async () => {
    const service = { peringatanAsync: vi.fn().mockResolvedValue({ data: [PERINGATAN] }) };
    TestBed.configureTestingModule({
      providers: [{ provide: NotifikasiService, useValue: service }],
    });

    const fixture = TestBed.createComponent(NotifikasiPage);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('GENTING');
    expect(teks).toContain('Anda belum mengonfirmasi keselamatan');
  });

  it('menampilkan pesan kosong bila tidak ada peringatan', async () => {
    const service = { peringatanAsync: vi.fn().mockResolvedValue({ data: [] }) };
    TestBed.configureTestingModule({
      providers: [{ provide: NotifikasiService, useValue: service }],
    });

    const fixture = TestBed.createComponent(NotifikasiPage);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Tidak ada peringatan');
  });
});
