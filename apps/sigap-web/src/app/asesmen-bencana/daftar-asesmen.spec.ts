import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AsesmenRingkas } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { DaftarAsesmen } from './daftar-asesmen';

const RINGKAS: AsesmenRingkas = {
  id: 'as-1',
  unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
  jenisBencana: 'Gempa Bumi',
  urutan: 1,
  dikirimPada: '2026-09-20T00:00:00Z',
  dikirimOleh: { id: 'p1', nama: 'Satgas A', nip: null, jabatan: null },
  statusPersetujuan: 'MENUNGGU_PIMPINAN',
};

describe('DaftarAsesmen', () => {
  it('menampilkan daftar asesmen dalam lingkup', async () => {
    const service = {
      daftarAsync: vi.fn().mockResolvedValue({ data: [RINGKAS], halaman: 1, ukuran: 20, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AsesmenService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DaftarAsesmen);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Unit A');
    expect(teks).toContain('MENUNGGU_PIMPINAN');
  });
});
