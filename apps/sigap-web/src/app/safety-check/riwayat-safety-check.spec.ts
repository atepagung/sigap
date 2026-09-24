import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { RiwayatSafetyCheck } from './riwayat-safety-check';
import { RiwayatSaya } from './safety-check.model';
import { SafetyCheckService } from './safety-check.service';

const RIWAYAT: RiwayatSaya = {
  broadcast: {
    id: 'bc-1',
    kategoriBencana: 'ALAM',
    jenisBencana: 'Gempa Bumi',
    lokasi: 'Kota Uji',
    sumber: 'MANUAL',
    lingkup: 'UNIT',
    dipicuPada: '2026-09-20T00:00:00Z',
    status: 'SELESAI',
    pemicu: {
      pengguna: { id: 'u1', nama: 'Satgas A', nip: null, jabatan: null },
      peran: 'SATGAS',
      unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
    },
  },
  status: 'AMAN',
  dijawabPada: '2026-09-20T01:00:00Z',
  dicatatkanSatgas: false,
};

describe('RiwayatSafetyCheck', () => {
  it('menampilkan riwayat dari service', async () => {
    const service = {
      riwayatSayaAsync: vi
        .fn()
        .mockResolvedValue({ data: [RIWAYAT], halaman: 1, ukuran: 20, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [{ provide: SafetyCheckService, useValue: service }],
    });

    const fixture = TestBed.createComponent(RiwayatSafetyCheck);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Gempa Bumi');
    expect(fixture.nativeElement.textContent).toContain('Saya Aman');
  });

  it('menampilkan pesan kosong bila belum ada riwayat', async () => {
    const service = {
      riwayatSayaAsync: vi.fn().mockResolvedValue({ data: [], halaman: 1, ukuran: 20, total: 0 }),
    };
    TestBed.configureTestingModule({
      providers: [{ provide: SafetyCheckService, useValue: service }],
    });

    const fixture = TestBed.createComponent(RiwayatSafetyCheck);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Belum ada riwayat');
  });
});
