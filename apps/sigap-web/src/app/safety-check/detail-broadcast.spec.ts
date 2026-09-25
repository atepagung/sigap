import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { BroadcastService } from './broadcast.service';
import { DetailBroadcast as DetailBroadcastDto } from './broadcast.model';
import { DetailBroadcast } from './detail-broadcast';

const UNIT = { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null };

function detail(status: 'AKTIF' | 'SELESAI'): DetailBroadcastDto {
  return {
    id: 'bc-1',
    kategoriBencana: 'ALAM',
    jenisBencana: 'Gempa Bumi',
    pesan: 'Konfirmasi keadaan Anda.',
    lokasi: 'Kota Uji',
    sumber: 'MANUAL',
    mmiTertinggi: null,
    lingkup: 'UNIT',
    kriteria: { unitId: 'unit-1', provinsi: null, kabupatenKota: null, eselonI: null },
    pemicu: {
      pengguna: { id: 'u1', nama: 'Satgas A', nip: null, jabatan: null },
      peran: 'SATGAS',
      unit: UNIT,
    },
    dipicuPada: '2026-09-20T00:00:00Z',
    status,
    diakhiri: null,
    sasaran: {
      jumlahUnitDisasar: 1,
      jumlahPegawaiDisasar: 10,
      jumlahMenjawab: 4,
      unitDisasar: [UNIT],
      unitDilewati: [],
    },
  };
}

async function render(
  dto: DetailBroadcastDto,
  permissions: readonly string[] = ['sigap:broadcast:close'],
) {
  const service = {
    bacaAsync: vi.fn().mockResolvedValue(dto),
    selesaiAsync: vi.fn().mockResolvedValue({ ...dto, status: 'SELESAI' }),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: BroadcastService, useValue: service },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: 'bc-1' }) } },
      },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });
  const fixture = TestBed.createComponent(DetailBroadcast);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, service };
}

describe('DetailBroadcast', () => {
  it('menampilkan detail dan tombol akhiri untuk broadcast aktif', async () => {
    const { fixture } = await render(detail('AKTIF'));
    expect(fixture.nativeElement.textContent).toContain('Gempa Bumi');
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Akhiri Broadcast'),
      ),
    ).toBe(true);
  });

  it('tombol akhiri tersembunyi tanpa permission sigap:broadcast:close', async () => {
    const { fixture } = await render(detail('AKTIF'), []);
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Akhiri Broadcast'),
      ),
    ).toBe(false);
  });

  it('mengakhiri broadcast memanggil service dan memperbarui status', async () => {
    const { fixture, service } = await render(detail('AKTIF'));
    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Akhiri Broadcast'),
    ) as HTMLButtonElement;

    tombol.click();
    await flushAsync();
    fixture.detectChanges();

    expect(service.selesaiAsync).toHaveBeenCalledWith('bc-1');
    expect(fixture.nativeElement.textContent).toContain('Selesai');
  });
});
