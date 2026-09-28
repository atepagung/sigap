import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BroadcastService } from './broadcast.service';
import { DaftarBroadcast } from './daftar-broadcast';
import { RiwayatBroadcast } from './broadcast.model';

const BROADCAST: RiwayatBroadcast = {
  id: 'bc-1',
  kategoriBencana: 'ALAM',
  jenisBencana: 'Gempa Bumi',
  lokasi: 'Kota Uji',
  sumber: 'MANUAL',
  lingkup: 'UNIT',
  dipicuPada: '2026-09-20T00:00:00Z',
  status: 'AKTIF',
  pemicu: {
    pengguna: { id: 'u1', nama: 'Satgas A', nip: null, jabatan: null },
    peran: 'SATGAS',
    unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
  },
  jumlahUnitDisasar: 1,
  jumlahPegawaiDisasar: 10,
  jumlahMenjawab: 4,
};

describe('DaftarBroadcast', () => {
  it('menampilkan riwayat broadcast', async () => {
    const service = {
      daftarAsync: vi
        .fn()
        .mockResolvedValue({ data: [BROADCAST], halaman: 1, ukuran: 20, total: 1 }),
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: BroadcastService, useValue: service }],
    });

    const fixture = TestBed.createComponent(DaftarBroadcast);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('Gempa Bumi');
    expect(teks).toContain('4 / 10');
  });
});
