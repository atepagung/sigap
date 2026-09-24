import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { ReferensiService } from '../core/referensi/referensi.service';
import { flushAsync } from '../shared/testing/flush-async';
import { Asesmen } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { DetailAsesmen } from './detail-asesmen';

function asesmen(overrides: Partial<Asesmen> = {}): Asesmen {
  return {
    id: 'as-1',
    unit: { id: 'unit-1', nama: 'Unit A', provinsi: null, kabupatenKota: null, eselonI: null },
    dikirimOleh: { id: 'p1', nama: 'Satgas A', nip: null, jabatan: null },
    dikirimPada: '2026-09-20T00:00:00Z',
    urutan: 1,
    kondisiBencana: {
      kategoriBencana: 'ALAM',
      jenisBencana: 'Gempa Bumi',
      waktuKejadian: null,
      kondisiFisik: 'BERAT',
      uraian: 'Uraian uji',
    },
    aspek: {
      sdm: {
        kelengkapanHadir: 'PENUH_100',
        korbanJiwa: 'TIDAK_ADA',
        kondisiFisik: 'AMAN',
        kondisiPsikis: 'AMAN',
        catatanKondisiPegawai: null,
        catatanTambahan: null,
      },
      aset: null,
      tik: null,
      arsip: null,
      layanan: [],
    },
    persetujuan: {
      status: 'MENUNGGU_PIMPINAN',
      disetujuiOleh: null,
      disetujuiPada: null,
      tanggapDarurat: null,
    },
    lampiran: [],
    ...overrides,
  };
}

async function render(dto: Asesmen, permissions: readonly string[] = ['sigap:asesmen:approve']) {
  const service = {
    bacaAsync: vi.fn().mockResolvedValue(dto),
    setujuiAsync: vi.fn().mockResolvedValue({
      ...dto,
      persetujuan: {
        status: 'DISETUJUI',
        disetujuiOleh: { id: 'pi1', nama: 'Pimpinan A', nip: null, jabatan: null },
        disetujuiPada: '2026-09-20T03:00:00Z',
        tanggapDarurat: { id: 'td-1', status: 'DARURAT' },
      },
    }),
    selesaikanTanggapDaruratAsync: vi.fn().mockResolvedValue({}),
  };
  const referensi = {
    opsiAsesmenAsync: vi.fn().mockResolvedValue({
      'sdm.kelengkapanHadir': [{ kode: 'PENUH_100', label: '100% Lengkap' }],
    }),
  };

  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      { provide: AsesmenService, useValue: service },
      { provide: ReferensiService, useValue: referensi },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: 'as-1' }) } },
      },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });
  const fixture = TestBed.createComponent(DetailAsesmen);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, service };
}

describe('DetailAsesmen', () => {
  it('menampilkan aspek dengan label opsi, bukan kode mentah', async () => {
    const { fixture } = await render(asesmen());
    expect(fixture.nativeElement.textContent).toContain('100% Lengkap');
  });

  it('Pimpinan melihat tombol setuju saat menunggu, lalu tombol selesaikan tanggap darurat setelah disetujui', async () => {
    const { fixture, service } = await render(asesmen());
    const setujui = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Setujui Asesmen'),
    ) as HTMLButtonElement;
    expect(setujui).not.toBeUndefined();

    setujui.click();
    await flushAsync();
    fixture.detectChanges();

    expect(service.setujuiAsync).toHaveBeenCalledWith('as-1');
    expect(fixture.nativeElement.textContent).toContain('DISETUJUI');
  });

  it('tombol setuju tersembunyi tanpa permission sigap:asesmen:approve', async () => {
    const { fixture } = await render(asesmen(), []);
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Setujui'),
      ),
    ).toBe(false);
  });

  it('catatan SDM yang di-Sieve tampil "Tidak tersedia", bukan kosong tanpa keterangan', async () => {
    const { fixture } = await render(asesmen());
    expect(fixture.nativeElement.textContent).toContain('Tidak tersedia');
  });
});
