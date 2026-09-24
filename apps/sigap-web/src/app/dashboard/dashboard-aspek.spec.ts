import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { AspekAgregat } from './monitor.model';
import { MonitorService } from './monitor.service';
import { DashboardAspek } from './dashboard-aspek';

const ASPEK: AspekAgregat = {
  jumlahUnitMelapor: 5,
  sdm: {
    kelengkapanHadir: { PENUH_100: 3, SEBAGIAN_75: 2 },
    unitAdaKorbanJiwa: 1,
    unitAdaLukaBerat: 0,
    unitAdaTraumaBerat: 0,
  },
  aset: {
    konstruksiBangunan: { KOKOH: 5 },
    aksesLokasi: { DAPAT_DIAKSES: 5 },
    kendaraanLaikOperasi: { NORMAL: 4 },
  },
  tik: {
    aksesJaringan: { NORMAL: 5 },
    kelistrikan: { PLN_NORMAL: 5 },
    aplikasiUtama: { BERFUNGSI_NORMAL: 5 },
  },
  arsip: { arsipVital: { AMAN: 5 }, evakuasiFisik: { DAPAT_DILAKUKAN: 5 } },
  layanan: { normal: 10, terganggu: 2, berhentiTotal: 0 },
};

describe('DashboardAspek', () => {
  it('menampilkan histogram sebagai tabel kode → jumlah', async () => {
    const service = { aspekAsync: vi.fn().mockResolvedValue(ASPEK) };
    TestBed.configureTestingModule({ providers: [{ provide: MonitorService, useValue: service }] });

    const fixture = TestBed.createComponent(DashboardAspek);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('5 unit melapor');
    expect(teks).toContain('PENUH_100');
    expect(teks).toContain('3');
    expect(teks).toContain('1'); // unitAdaKorbanJiwa
  });
});
