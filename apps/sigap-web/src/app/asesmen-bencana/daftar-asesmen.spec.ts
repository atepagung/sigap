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

function tombolHalaman(fixture: { nativeElement: unknown }, teks: string): HTMLButtonElement {
  const semua = [
    ...(fixture.nativeElement as HTMLElement).querySelectorAll('keu-pagination button'),
  ];
  return semua.find((b) => b.textContent?.trim() === teks) as HTMLButtonElement;
}

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

  it('halaman berikutnya meminta halaman 2 dan menampilkan isinya, bukan berhenti di halaman pertama', async () => {
    const kedua = { ...RINGKAS, id: 'as-2', unit: { ...RINGKAS.unit, nama: 'Unit Dua' } };
    const daftarAsync = vi
      .fn()
      .mockResolvedValueOnce({ data: [RINGKAS], halaman: 1, ukuran: 20, total: 45 })
      .mockResolvedValueOnce({ data: [kedua], halaman: 2, ukuran: 20, total: 45 });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AsesmenService, useValue: { daftarAsync } }],
    });
    const fixture = TestBed.createComponent(DaftarAsesmen);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Halaman 1 dari 3');

    tombolHalaman(fixture, 'Berikutnya').click();
    await flushAsync();
    fixture.detectChanges();

    expect(daftarAsync).toHaveBeenLastCalledWith(undefined, undefined, 2);
    expect(fixture.nativeElement.textContent).toContain('Unit Dua');
    expect(fixture.nativeElement.textContent).toContain('Halaman 2 dari 3');
  });
});
