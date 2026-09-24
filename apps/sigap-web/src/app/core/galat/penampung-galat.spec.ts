import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TokenProvider } from '../auth/token-provider';
import { PenampungGalat, pesanGalatMuat } from './penampung-galat';

describe('PenampungGalat', () => {
  function siapkan() {
    const router = { navigate: vi.fn().mockResolvedValue(true) };
    const token = { keluar: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        PenampungGalat,
        { provide: Router, useValue: router },
        { provide: TokenProvider, useValue: token },
      ],
    });
    return { penampung: TestBed.inject(PenampungGalat), router, token };
  }

  it('mengembalikan hasil dan tanpa pesan bila berhasil', async () => {
    const { penampung } = siapkan();
    expect(await penampung.jalankanAsync(() => Promise.resolve(7))).toBe(7);
    expect(penampung.pesan()).toBeNull();
  });

  it.each([
    [404, 'Data tidak ditemukan.'],
    [403, 'Anda tidak memiliki izin untuk membuka halaman ini.'],
    [500, 'Terjadi kesalahan saat memuat data. Coba lagi beberapa saat lagi.'],
  ])('status %i menjadi pesan Indonesia', async (status, pesan) => {
    const { penampung } = siapkan();
    const hasil = await penampung.jalankanAsync(() =>
      Promise.reject(new HttpErrorResponse({ status })),
    );
    expect(hasil).toBeNull();
    expect(penampung.pesan()).toBe(pesan);
  });

  it('404 tidak menyebut lingkup atau izin (tidak membocorkan keberadaan data)', () => {
    expect(pesanGalatMuat(new HttpErrorResponse({ status: 404 }))).not.toMatch(/lingkup|izin/i);
  });

  it('401 mengakhiri sesi dan mengarahkan ke masuk tanpa pesan', async () => {
    const { penampung, router, token } = siapkan();
    await penampung.jalankanAsync(() => Promise.reject(new HttpErrorResponse({ status: 401 })));
    expect(token.keluar).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['masuk']);
    expect(penampung.pesan()).toBeNull();
  });

  it('memulihkan pesan pada percobaan berikutnya yang berhasil', async () => {
    const { penampung } = siapkan();
    await penampung.jalankanAsync(() => Promise.reject(new HttpErrorResponse({ status: 500 })));
    await penampung.jalankanAsync(() => Promise.resolve(1));
    expect(penampung.pesan()).toBeNull();
  });
});
