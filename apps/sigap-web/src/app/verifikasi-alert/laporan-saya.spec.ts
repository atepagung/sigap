import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LaporanSaya } from './laporan-saya';
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

describe('LaporanSaya', () => {
  it('menampilkan laporan milik pemanggil', async () => {
    const service = {
      sayaAsync: vi.fn().mockResolvedValue({ data: [LAPORAN], halaman: 1, ukuran: 20, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: LaporanService, useValue: service }],
    });

    const fixture = TestBed.createComponent(LaporanSaya);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Gempa Bumi');
    expect(teks).toContain('MENUNGGU');
  });
});
