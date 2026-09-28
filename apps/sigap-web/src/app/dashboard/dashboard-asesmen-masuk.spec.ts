import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AsesmenMasuk } from './monitor.model';
import { MonitorService } from './monitor.service';
import { DashboardAsesmenMasuk } from './dashboard-asesmen-masuk';

const BARIS: AsesmenMasuk = {
  asesmenId: 'as-1',
  unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
  jenisBencana: 'Gempa Bumi',
  urutan: 2,
  dikirimPada: '2026-09-20T00:00:00Z',
  statusPersetujuan: 'MENUNGGU_PIMPINAN',
  tanggapDarurat: null,
};

function tombolHalaman(fixture: { nativeElement: unknown }, teks: string): HTMLButtonElement {
  const semua = [
    ...(fixture.nativeElement as HTMLElement).querySelectorAll('keu-pagination button'),
  ];
  return semua.find((b) => b.textContent?.trim() === teks) as HTMLButtonElement;
}

describe('DashboardAsesmenMasuk', () => {
  it('menampilkan asesmen masuk dengan tautan ke detail unit', async () => {
    const service = {
      asesmenMasukAsync: vi
        .fn()
        .mockResolvedValue({ data: [BARIS], halaman: 1, ukuran: 20, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DashboardAsesmenMasuk);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Unit A');
    expect(teks).toContain('#2');
  });

  it('halaman berikutnya meminta halaman 2', async () => {
    const asesmenMasukAsync = vi
      .fn()
      .mockResolvedValue({ data: [BARIS], halaman: 1, ukuran: 20, total: 45 });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: { asesmenMasukAsync } }],
    });
    const fixture = TestBed.createComponent(DashboardAsesmenMasuk);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    tombolHalaman(fixture, 'Berikutnya').click();
    await flushAsync();

    expect(asesmenMasukAsync).toHaveBeenLastCalledWith(2);
  });

  it('semua data muat di satu halaman: tidak ada tombol halaman berikutnya yang dapat ditekan', async () => {
    const asesmenMasukAsync = vi
      .fn()
      .mockResolvedValue({ data: [BARIS], halaman: 1, ukuran: 20, total: 1 });
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: { asesmenMasukAsync } }],
    });
    const fixture = TestBed.createComponent(DashboardAsesmenMasuk);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(tombolHalaman(fixture, 'Berikutnya').disabled).toBe(true);
    expect(tombolHalaman(fixture, 'Sebelumnya').disabled).toBe(true);
  });
});
