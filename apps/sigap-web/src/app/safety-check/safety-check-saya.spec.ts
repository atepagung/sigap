import { HttpErrorResponse } from '@angular/common/http';
import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideIamPermissions } from '@danarakca/iam';
import { Aktif } from './safety-check.model';
import { SafetyCheckSaya } from './safety-check-saya';
import { SafetyCheckService } from './safety-check.service';

const AKTIF_BELUM_DIJAWAB: Aktif = {
  broadcast: {
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
  },
  pesan: 'Konfirmasi keadaan Anda.',
  responsSaya: null,
};

function fakeService(daftar: readonly Aktif[]) {
  return {
    aktifAsync: vi.fn().mockResolvedValue({ data: daftar }),
    jawabSayaAsync: vi.fn().mockResolvedValue({}),
  };
}

async function render(
  daftar: readonly Aktif[],
  permissions: readonly string[] = ['sigap:safety-check:respond'],
) {
  const service = fakeService(daftar);
  TestBed.configureTestingModule({
    providers: [
      { provide: SafetyCheckService, useValue: service },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });
  const fixture = TestBed.createComponent(SafetyCheckSaya);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, service };
}

describe('SafetyCheckSaya', () => {
  it('menampilkan modal wajib saat ada broadcast belum dijawab', async () => {
    const { fixture } = await render([AKTIF_BELUM_DIJAWAB]);
    const modal = fixture.nativeElement.querySelector('keu-modal');
    expect(modal).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Gempa Bumi');
  });

  it('tidak menampilkan modal saat tidak ada broadcast aktif', async () => {
    const { fixture } = await render([]);
    expect(fixture.nativeElement.querySelector('keu-modal')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Tidak ada broadcast');
  });

  it('menjawab AMAN memanggil service lalu memuat ulang', async () => {
    const { fixture, service } = await render([AKTIF_BELUM_DIJAWAB]);
    const tombolAman = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Saya Aman'),
    ) as HTMLButtonElement;

    tombolAman.click();
    await flushAsync();

    expect(service.jawabSayaAsync).toHaveBeenCalledWith('bc-1', 'AMAN');
    expect(service.aktifAsync).toHaveBeenCalledTimes(2);
  });

  it('tombol jawab tersembunyi tanpa permission sigap:safety-check:respond', async () => {
    const { fixture } = await render([AKTIF_BELUM_DIJAWAB], []);
    const teks = fixture.nativeElement.textContent as string;

    // Modal wajib tetap tampil (informasi keadaan), tapi tanpa tombol aksi yang akan ditolak API.
    expect(fixture.nativeElement.querySelector('keu-modal')).not.toBeNull();
    expect(teks).not.toContain('Aksi');
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Saya Aman'),
      ),
    ).toBe(false);
  });

  it('galat saat menjawab tampil di DALAM modal wajib (yang tidak bisa ditutup), dan tombol aktif kembali', async () => {
    const { fixture, service } = await render([AKTIF_BELUM_DIJAWAB]);
    service.jawabSayaAsync.mockRejectedValueOnce(
      new HttpErrorResponse({ status: 409, error: { detail: 'Broadcast ini sudah selesai.' } }),
    );
    const el = fixture.nativeElement as HTMLElement;
    const tombolModal = [...el.querySelectorAll('keu-modal button')].find(
      (b) => b.textContent?.trim() === 'Saya Aman',
    ) as HTMLButtonElement;

    tombolModal.click();
    await flushAsync();
    fixture.detectChanges();

    expect(el.querySelector('keu-modal [role="alert"]')?.textContent?.trim()).toBe(
      'Broadcast ini sudah selesai.',
    );
    expect(el.querySelector('keu-modal')).not.toBeNull();
    expect(service.aktifAsync).toHaveBeenCalledTimes(1);
    expect(
      ([...el.querySelectorAll('keu-modal button')] as HTMLButtonElement[]).every(
        (b) => !b.disabled,
      ),
    ).toBe(true);
  });
});
