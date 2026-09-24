import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { SafetyCheckKelompok } from './monitor.model';
import { MonitorService } from './monitor.service';
import { DashboardSafetyCheck } from './dashboard-safety-check';

const BARIS: SafetyCheckKelompok = {
  kelompok: { kode: 'unit-1', label: 'Unit A' },
  totalPegawai: 20,
  aman: 15,
  butuhBantuan: 1,
  belumMerespons: 4,
  tingkatRespons: 0.8,
};

describe('DashboardSafetyCheck', () => {
  it('memuat kelompok "unit" secara bawaan', async () => {
    const service = {
      safetyCheckAsync: vi
        .fn()
        .mockResolvedValue({ data: [BARIS], halaman: 1, ukuran: 50, total: 1 }),
    };
    TestBed.configureTestingModule({ providers: [{ provide: MonitorService, useValue: service }] });

    const fixture = TestBed.createComponent(DashboardSafetyCheck);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(service.safetyCheckAsync).toHaveBeenCalledWith('unit');
    expect(fixture.nativeElement.textContent).toContain('Unit A');
  });

  it('mengganti kelompok memuat ulang dengan parameter baru', async () => {
    const service = {
      safetyCheckAsync: vi
        .fn()
        .mockResolvedValue({ data: [BARIS], halaman: 1, ukuran: 50, total: 1 }),
    };
    TestBed.configureTestingModule({ providers: [{ provide: MonitorService, useValue: service }] });

    const fixture = TestBed.createComponent(DashboardSafetyCheck);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      'select[name="kelompok"]',
    ) as HTMLSelectElement;
    select.value = 'provinsi';
    select.dispatchEvent(new Event('change'));
    await flushAsync();

    expect(service.safetyCheckAsync).toHaveBeenLastCalledWith('provinsi');
  });
});
