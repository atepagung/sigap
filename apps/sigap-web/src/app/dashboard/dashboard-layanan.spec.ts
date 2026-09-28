import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LayananGangguan } from './monitor.model';
import { MonitorService } from './monitor.service';
import { DashboardLayanan } from './dashboard-layanan';

const GANGGUAN: LayananGangguan = {
  layanan: { id: 'lk-1', nama: 'Layanan SP2D', rtoJam: 24 },
  unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
  status: 'TERGANGGU',
  sejak: '2026-09-20T00:00:00Z',
  sisaRtoJam: 6.5,
};

function tombolHalaman(fixture: { nativeElement: unknown }, teks: string): HTMLButtonElement {
  const semua = [
    ...(fixture.nativeElement as HTMLElement).querySelectorAll('keu-pagination button'),
  ];
  return semua.find((b) => b.textContent?.trim() === teks) as HTMLButtonElement;
}

describe('DashboardLayanan', () => {
  it('menampilkan gangguan tanpa filter status', async () => {
    const service = {
      layananAsync: vi
        .fn()
        .mockResolvedValue({ data: [GANGGUAN], halaman: 1, ukuran: 50, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DashboardLayanan);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(service.layananAsync).toHaveBeenCalledWith(undefined, 1);
    expect(fixture.nativeElement.textContent).toContain('Layanan SP2D');
  });

  it('mengganti filter status memuat ulang dengan nilai baru', async () => {
    const service = {
      layananAsync: vi
        .fn()
        .mockResolvedValue({ data: [GANGGUAN], halaman: 1, ukuran: 50, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DashboardLayanan);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      'select[name="status"]',
    ) as HTMLSelectElement;
    select.value = 'BERHENTI_TOTAL';
    select.dispatchEvent(new Event('change'));
    await flushAsync();

    expect(service.layananAsync).toHaveBeenLastCalledWith('BERHENTI_TOTAL', 1);
  });

  it('mengganti status setelah pindah halaman kembali ke halaman 1, bukan tetap di halaman lama', async () => {
    const layananAsync = vi
      .fn()
      .mockImplementation((_saringan: string | undefined, nomor: number) =>
        Promise.resolve({ data: [], halaman: nomor, ukuran: 50, total: 120 }),
      );
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: { layananAsync } }],
    });
    const fixture = TestBed.createComponent(DashboardLayanan);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();
    tombolHalaman(fixture, 'Berikutnya').click();
    await flushAsync();
    expect(layananAsync).toHaveBeenLastCalledWith(undefined, 2);

    const select = fixture.nativeElement.querySelector(
      'select[name="status"]',
    ) as HTMLSelectElement;
    select.value = 'BERHENTI_TOTAL';
    select.dispatchEvent(new Event('change'));
    await flushAsync();

    expect(layananAsync).toHaveBeenLastCalledWith('BERHENTI_TOTAL', 1);
  });
});
