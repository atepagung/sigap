import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DaftarLaporan } from './daftar-laporan';
import { Laporan } from './laporan.model';
import { LaporanService } from './laporan.service';

const LAPORAN: Laporan = {
  id: 'lap-1',
  unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
  pelapor: { id: 'p1', nama: 'Pegawai A', nip: null, jabatan: null },
  kategoriBencana: 'ALAM',
  jenisBencana: 'Gempa Bumi',
  level: 'BERAT',
  lokasi: 'Kantor Uji',
  deskripsi: null,
  status: 'MENUNGGU',
  verifikasi: null,
  lampiran: [],
  dilaporkanPada: '2026-09-20T00:00:00Z',
};

describe('DaftarLaporan', () => {
  it('menampilkan antrean verifikasi dengan nama pelapor', async () => {
    const service = {
      daftarAsync: vi.fn().mockResolvedValue({ data: [LAPORAN], halaman: 1, ukuran: 20, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: LaporanService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DaftarLaporan);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Pegawai A');
    expect(teks).toContain('Gempa Bumi');
  });

  it('menampilkan pesan kosong bila tidak ada laporan', async () => {
    const service = {
      daftarAsync: vi.fn().mockResolvedValue({ data: [], halaman: 1, ukuran: 20, total: 0 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: LaporanService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DaftarLaporan);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Tidak ada laporan');
  });
});
