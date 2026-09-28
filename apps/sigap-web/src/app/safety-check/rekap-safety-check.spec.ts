import { HttpErrorResponse } from '@angular/common/http';
import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideIamPermissions } from '@danarakca/iam';
import { RekapSafetyCheck } from './rekap-safety-check';
import { Rekap } from './safety-check.model';
import { SafetyCheckService } from './safety-check.service';

const UNIT = { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null };
const BROADCAST = {
  id: 'bc-1',
  kategoriBencana: 'ALAM',
  jenisBencana: 'Gempa Bumi',
  lokasi: 'Kota Uji',
  sumber: 'MANUAL' as const,
  lingkup: 'UNIT',
  dipicuPada: '2026-09-20T00:00:00Z',
  status: 'AKTIF' as const,
  pemicu: {
    pengguna: { id: 'u1', nama: 'Satgas A', nip: null, jabatan: null },
    peran: 'SATGAS',
    unit: UNIT,
  },
};

function pegawai(id: string, nama: string, status: 'AMAN' | 'BUTUH_BANTUAN' | 'BELUM') {
  return {
    pegawai: { id, nama, nip: null, jabatan: null },
    status,
    dijawabPada: null,
    dicatatkan: false,
    dicatatOleh: null,
    keterangan: null,
    lokasiTerakhir: null,
  };
}

const REKAP: Rekap = {
  broadcast: BROADCAST,
  broadcastLainAktif: [],
  unit: UNIT,
  data: [
    pegawai('p1', 'Pegawai Aman', 'AMAN'),
    pegawai('p2', 'Pegawai Butuh', 'BUTUH_BANTUAN'),
    pegawai('p3', 'Pegawai Belum', 'BELUM'),
  ],
  halaman: 1,
  ukuran: 200,
  total: 3,
};

async function render(rekap: Rekap | Error | HttpErrorResponse) {
  const service = {
    rekapAsync: vi
      .fn()
      .mockImplementation(() =>
        rekap instanceof Error || rekap instanceof HttpErrorResponse
          ? Promise.reject(rekap)
          : Promise.resolve(rekap),
      ),
    catatAsync: vi.fn().mockResolvedValue({}),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: SafetyCheckService, useValue: service },
      provideIamPermissions(() => Promise.resolve(['sigap:safety-check:record'])),
    ],
  });
  const fixture = TestBed.createComponent(RekapSafetyCheck);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, service };
}

describe('RekapSafetyCheck', () => {
  it('mengelompokkan pegawai ke tab Aman/Butuh Bantuan/Belum dengan hitungan benar', async () => {
    const { fixture } = await render(REKAP);
    const teks = fixture.nativeElement.textContent as string;

    expect(teks).toContain('Butuh Bantuan');
    expect(teks).toContain('Belum Merespons');
    expect(teks).toContain('Aman');
    // Tab count badge muncul untuk ketiga kelompok, masing-masing 1 pegawai.
    const counts = [...fixture.nativeElement.querySelectorAll('.tabs__count')].map(
      (el: HTMLElement) => el.textContent?.trim(),
    );
    expect(counts).toEqual(['1', '1', '1']);
  });

  it('menampilkan pesan tanpa broadcast aktif bila rekap gagal (404)', async () => {
    const { fixture } = await render(new HttpErrorResponse({ status: 404 }));
    expect(fixture.nativeElement.textContent).toContain('Tidak ada broadcast aktif');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('menampilkan pesan galat untuk kegagalan selain 404', async () => {
    const { fixture } = await render(new HttpErrorResponse({ status: 500 }));
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain(
      'Terjadi kesalahan',
    );
  });

  describe('galat saat menyimpan catatan', () => {
    const tombol = (el: HTMLElement, teks: string) =>
      [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === teks) as
        HTMLButtonElement | undefined;
    const alertModal = (el: HTMLElement) =>
      el.querySelector('keu-modal [role="alert"]')?.textContent?.trim();

    async function bukaModal() {
      const hasil = await render(REKAP);
      const el = hasil.fixture.nativeElement as HTMLElement;
      tombol(el, 'Catatkan')!.click();
      hasil.fixture.detectChanges();
      return { ...hasil, el };
    }

    async function simpan(fixture: { detectChanges: () => void }, el: HTMLElement) {
      tombol(el, 'Simpan')!.click();
      await flushAsync();
      fixture.detectChanges();
    }

    it('400 menampilkan pesan API DI DALAM modal, modal tetap terbuka, dan tidak memuat ulang', async () => {
      const { fixture, service, el } = await bukaModal();
      service.catatAsync.mockRejectedValueOnce(
        new HttpErrorResponse({
          status: 400,
          error: { detail: 'Sebutkan dari mana keadaan ini diketahui, minimal lima huruf.' },
        }),
      );

      await simpan(fixture, el);

      expect(alertModal(el)).toBe('Sebutkan dari mana keadaan ini diketahui, minimal lima huruf.');
      expect(el.querySelector('keu-modal')).not.toBeNull();
      expect(service.rekapAsync).toHaveBeenCalledTimes(1);
      expect(tombol(el, 'Simpan')!.disabled).toBe(false);
    });

    it('percobaan berikutnya yang berhasil menutup modal, menghapus pesan, dan memuat ulang rekap', async () => {
      const { fixture, service, el } = await bukaModal();
      service.catatAsync.mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
      await simpan(fixture, el);
      expect(alertModal(el)).toBeTruthy();

      await simpan(fixture, el);

      expect(el.querySelector('keu-modal')).toBeNull();
      expect(el.querySelector('[role="alert"]')).toBeNull();
      expect(service.catatAsync).toHaveBeenCalledTimes(2);
      expect(service.rekapAsync).toHaveBeenCalledTimes(2);
    });

    it('PUT berhasil dengan badan kosong (null) tetap dianggap berhasil, bukan gagal', async () => {
      const { fixture, service, el } = await bukaModal();
      service.catatAsync.mockResolvedValueOnce(null);

      await simpan(fixture, el);

      expect(el.querySelector('keu-modal')).toBeNull();
      expect(el.querySelector('[role="alert"]')).toBeNull();
    });

    it('Batal lalu buka ulang tidak membawa pesan galat lama', async () => {
      const { fixture, service, el } = await bukaModal();
      service.catatAsync.mockRejectedValueOnce(new HttpErrorResponse({ status: 403 }));
      await simpan(fixture, el);
      expect(alertModal(el)).toBeTruthy();

      tombol(el, 'Batal')!.click();
      fixture.detectChanges();
      tombol(el, 'Catatkan')!.click();
      fixture.detectChanges();

      expect(el.querySelector('keu-modal')).not.toBeNull();
      expect(alertModal(el)).toBeUndefined();
    });
  });
});
