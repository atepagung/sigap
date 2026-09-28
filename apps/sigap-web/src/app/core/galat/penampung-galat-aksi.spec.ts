import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TokenProvider } from '../auth/token-provider';
import { PenampungGalat, pesanGalatAksi } from './penampung-galat';

const galat = (status: number, error?: unknown) => new HttpErrorResponse({ status, error });

describe('pesanGalatAksi', () => {
  it.each([400, 409, 422])('status %i menampilkan `detail` dari API apa adanya', (status) => {
    expect(
      pesanGalatAksi(galat(status, { detail: 'Sebutkan alasannya, minimal lima huruf.' })),
    ).toBe('Sebutkan alasannya, minimal lima huruf.');
  });

  it.each([
    ['tanpa badan', undefined],
    ['badan bukan objek', 'teks polos'],
    ['detail bukan string', { detail: { dalam: 1 } }],
    ['detail kosong', { detail: '   ' }],
  ])('400 %s memakai pesan umum, bukan kosong', (_nama, error) => {
    expect(pesanGalatAksi(galat(400, error))).toBe(
      'Isian tidak dapat diproses. Periksa kembali lalu coba lagi.',
    );
  });

  it.each([
    [0, 'Tidak dapat terhubung ke server. Periksa koneksi Anda lalu coba lagi.'],
    [403, 'Anda tidak memiliki izin untuk melakukan aksi ini.'],
    [404, 'Data tidak ditemukan.'],
    [500, 'Aksi gagal diproses. Coba lagi beberapa saat lagi.'],
    [503, 'Aksi gagal diproses. Coba lagi beberapa saat lagi.'],
  ])('status %i menjadi pesan tetap', (status, pesan) => {
    expect(pesanGalatAksi(galat(status))).toBe(pesan);
  });

  it('status di luar 400/409/422 tidak pernah meneruskan `detail` (tanpa rincian teknis ke layar)', () => {
    for (const status of [403, 404, 500, 502]) {
      const pesan = pesanGalatAksi(
        galat(status, { detail: 'NullReferenceException di Store.cs:88' }),
      );
      expect(pesan).not.toContain('NullReference');
    }
  });

  it('404 tidak menyebut lingkup atau izin (tidak membocorkan keberadaan data)', () => {
    expect(pesanGalatAksi(galat(404)).toLowerCase()).not.toMatch(/lingkup|izin|scope|unit/);
  });

  it('galat bukan HTTP (mis. kesalahan pemrograman) memakai pesan umum', () => {
    expect(pesanGalatAksi(new Error('boom'))).toBe(
      'Aksi gagal diproses. Coba lagi beberapa saat lagi.',
    );
  });
});

describe('PenampungGalat.jalankanAksiAsync', () => {
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
    expect(await penampung.jalankanAksiAsync(() => Promise.resolve('ok'))).toBe('ok');
    expect(penampung.pesanAksi()).toBeNull();
  });

  it('gagal: mengembalikan null, mengisi pesanAksi, dan TIDAK melempar (bukan galat tak tertangani)', async () => {
    const { penampung } = siapkan();
    const hasil = await penampung.jalankanAksiAsync(() =>
      Promise.reject(galat(400, { detail: 'Alasan wajib diisi.' })),
    );
    expect(hasil).toBeNull();
    expect(penampung.pesanAksi()).toBe('Alasan wajib diisi.');
  });

  it('percobaan baru menghapus pesan percobaan sebelumnya, termasuk saat percobaan baru berhasil', async () => {
    const { penampung } = siapkan();
    await penampung.jalankanAksiAsync(() => Promise.reject(galat(500)));
    expect(penampung.pesanAksi()).not.toBeNull();

    await penampung.jalankanAksiAsync(() => Promise.resolve(1));
    expect(penampung.pesanAksi()).toBeNull();
  });

  it('hapusPesanAksi mengosongkan pesan (dipakai saat dialog dibuka ulang atau ditutup)', async () => {
    const { penampung } = siapkan();
    await penampung.jalankanAksiAsync(() => Promise.reject(galat(403)));
    penampung.hapusPesanAksi();
    expect(penampung.pesanAksi()).toBeNull();
  });

  it('galat aksi tidak menimpa galat memuat, dan sebaliknya (dua pesan terpisah)', async () => {
    const { penampung } = siapkan();
    await penampung.jalankanAsync(() => Promise.reject(galat(404)));
    await penampung.jalankanAksiAsync(() => Promise.reject(galat(403)));

    expect(penampung.pesan()).toBe('Data tidak ditemukan.');
    expect(penampung.pesanAksi()).toBe('Anda tidak memiliki izin untuk melakukan aksi ini.');
  });

  it('401 mengakhiri sesi dan mengarahkan ke masuk, tanpa pesan', async () => {
    const { penampung, router, token } = siapkan();
    const hasil = await penampung.jalankanAksiAsync(() => Promise.reject(galat(401)));

    expect(hasil).toBeNull();
    expect(token.keluar).toHaveBeenCalledOnce();
    expect(router.navigate).toHaveBeenCalledWith(['masuk']);
    expect(penampung.pesanAksi()).toBeNull();
  });
});
