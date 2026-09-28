import { flushAsync } from '../shared/testing/flush-async';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { DetailLaporan } from './detail-laporan';
import { Laporan } from './laporan.model';
import { LaporanService } from './laporan.service';

function laporan(overrides: Partial<Laporan> = {}): Laporan {
  return {
    id: 'lap-1',
    unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
    pelapor: { id: 'p1', nama: 'Pegawai A', nip: null, jabatan: null },
    kategoriBencana: 'ALAM',
    jenisBencana: 'Gempa Bumi',
    level: 'BERAT',
    lokasi: 'Kantor Uji',
    deskripsi: 'Air mulai naik.',
    status: 'MENUNGGU',
    verifikasi: null,
    lampiran: [],
    dilaporkanPada: '2026-09-20T00:00:00Z',
    ...overrides,
  };
}

async function render(dto: Laporan, permissions: readonly string[] = ['sigap:laporan:verify']) {
  const service = {
    bacaAsync: vi.fn().mockResolvedValue(dto),
    verifikasiAsync: vi.fn().mockResolvedValue({ ...dto, status: 'TERVERIFIKASI' }),
  };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      { provide: LaporanService, useValue: service },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: 'lap-1' }) } },
      },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });
  const fixture = TestBed.createComponent(DetailLaporan);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, service };
}

describe('DetailLaporan', () => {
  it('menampilkan form verifikasi untuk Satgas saat belum diputuskan', async () => {
    const { fixture } = await render(laporan());
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Valid'),
      ),
    ).toBe(true);
  });

  it('menyembunyikan form verifikasi tanpa permission sigap:laporan:verify', async () => {
    const { fixture } = await render(laporan(), []);
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Valid'),
      ),
    ).toBe(false);
  });

  it('menampilkan hasil verifikasi, bukan form, bila sudah diputuskan', async () => {
    const { fixture } = await render(
      laporan({
        status: 'TERVERIFIKASI',
        verifikasi: {
          keputusan: 'VALID',
          alasan: null,
          oleh: { id: 's1', nama: 'Satgas A', nip: null, jabatan: null },
          pada: '2026-09-20T02:00:00Z',
        },
      }),
    );
    expect(fixture.nativeElement.textContent).toContain('VALID oleh Satgas A');
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Tolak'),
      ),
    ).toBe(false);
  });

  it('menekan Valid memanggil verifikasiAsync', async () => {
    const { fixture, service } = await render(laporan());
    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Valid',
    ) as HTMLButtonElement;

    tombol.click();
    await flushAsync();
    fixture.detectChanges();

    expect(service.verifikasiAsync).toHaveBeenCalledWith('lap-1', 'VALID', undefined);
    expect(fixture.nativeElement.textContent).toContain('TERVERIFIKASI');
  });
});
