import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RingkasanMonitor } from './monitor.model';
import { MonitorService } from './monitor.service';
import { DashboardRingkasan } from './dashboard-ringkasan';

const RINGKASAN: RingkasanMonitor = {
  lingkup: { jenis: 'NASIONAL', label: 'Nasional', penyaringAktif: {} },
  jenisBencana: 'Gempa Bumi',
  jenisAktif: ['Gempa Bumi'],
  safetyCheck: {
    totalPegawai: 100,
    aman: 80,
    butuhBantuan: 5,
    belumMerespons: 15,
    tingkatRespons: 0.85,
    jumlahUnitDisasar: 4,
    jumlahUnitBelumDisasar: 1,
  },
  asesmen: { unitMelapor: 4, menungguPimpinan: 1, disetujui: 3 },
  tanggapDarurat: { unitDarurat: 2 },
  layanan: { normal: 10, terganggu: 2, berhentiTotal: 0 },
};

describe('DashboardRingkasan', () => {
  it('menampilkan lingkup dan angka agregat', async () => {
    const service = { ringkasanAsync: vi.fn().mockResolvedValue(RINGKASAN) };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: MonitorService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DashboardRingkasan);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Nasional');
    expect(teks).toContain('80');
    expect(teks).toContain('2');
  });
});
