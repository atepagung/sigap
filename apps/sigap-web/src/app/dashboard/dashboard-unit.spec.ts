import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { flushAsync } from '../shared/testing/flush-async';
import { UnitDetail } from './monitor.model';
import { MonitorService } from './monitor.service';
import { DashboardUnit } from './dashboard-unit';

const UNIT: UnitDetail = {
  unit: {
    id: 'unit-1',
    nama: 'Unit A',
    provinsi: 'Riau',
    kabupatenKota: 'Kota Pekanbaru',
    eselonI: 'djp',
  },
  tanggapDarurat: {
    id: 'td-1',
    status: 'DARURAT',
    jenisBencana: 'Gempa Bumi',
    sejak: '2026-09-20T00:00:00Z',
  },
  safetyCheck: [],
  asesmenTerkini: null,
  layananTerganggu: [],
};

function rute(unitId: string) {
  const params = new BehaviorSubject(convertToParamMap({ unitId }));
  return {
    params,
    get snapshot() {
      return { paramMap: params.value };
    },
    paramMap: params.asObservable(),
  };
}

function siapkan(service: { unitAsync: ReturnType<typeof vi.fn> }, route = rute('unit-1')) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: MonitorService, useValue: service },
      { provide: ActivatedRoute, useValue: route },
    ],
  });
  return { fixture: TestBed.createComponent(DashboardUnit), route };
}

describe('DashboardUnit', () => {
  it('membaca unitId dari route dan menampilkan detail', async () => {
    const service = { unitAsync: vi.fn().mockResolvedValue(UNIT) };
    const { fixture } = siapkan(service);

    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(service.unitAsync).toHaveBeenCalledWith('unit-1');
    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Unit A');
    expect(teks).toContain('DARURAT');
  });

  it('menampilkan pesan, bukan layar kosong, saat API menjawab 404', async () => {
    const service = {
      unitAsync: vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 404 })),
    };
    const { fixture } = siapkan(service, rute('unit-x'));

    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;
    expect(alert.textContent).toContain('Data tidak ditemukan');
  });

  it('memuat ulang saat parameter unitId berganti pada komponen yang sama', async () => {
    const service = { unitAsync: vi.fn().mockResolvedValue(UNIT) };
    const { fixture, route } = siapkan(service);

    fixture.detectChanges();
    await flushAsync();
    route.params.next(convertToParamMap({ unitId: 'unit-2' }));
    await flushAsync();

    expect(service.unitAsync).toHaveBeenNthCalledWith(1, 'unit-1');
    expect(service.unitAsync).toHaveBeenNthCalledWith(2, 'unit-2');
  });
});
