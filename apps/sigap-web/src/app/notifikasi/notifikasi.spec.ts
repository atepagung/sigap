import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { Peringatan } from './notifikasi.model';
import { NotifikasiService } from './notifikasi.service';
import { NotifikasiPage } from './notifikasi';

const PERINGATAN: Peringatan = {
  kode: 'SC_BELUM_DIJAWAB',
  tingkat: 'GENTING',
  judul: 'Anda belum mengonfirmasi keselamatan',
  pesan: 'Broadcast safety check sedang berjalan.',
  terkait: { jenis: 'BROADCAST', id: 'bc-1' },
};

describe('NotifikasiPage', () => {
  it('menampilkan peringatan dari service', async () => {
    const service = { peringatanAsync: vi.fn().mockResolvedValue({ data: [PERINGATAN] }) };
    TestBed.configureTestingModule({
      providers: [{ provide: NotifikasiService, useValue: service }],
    });

    const fixture = TestBed.createComponent(NotifikasiPage);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    const teks = fixture.nativeElement.textContent as string;
    expect(teks).toContain('GENTING');
    expect(teks).toContain('Anda belum mengonfirmasi keselamatan');
  });

  it('menampilkan pesan kosong bila tidak ada peringatan', async () => {
    const service = { peringatanAsync: vi.fn().mockResolvedValue({ data: [] }) };
    TestBed.configureTestingModule({
      providers: [{ provide: NotifikasiService, useValue: service }],
    });

    const fixture = TestBed.createComponent(NotifikasiPage);
    fixture.detectChanges();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Tidak ada peringatan');
  });
});
