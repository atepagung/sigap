import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { ReferensiService } from '../core/referensi/referensi.service';
import { LaporBencana } from './lapor-bencana';
import { LaporanService } from './laporan.service';

async function render(
  permissions: readonly string[] = ['sigap:laporan:create', 'sigap:lampiran:upload'],
) {
  const referensi = {
    jenisBencanaAsync: vi.fn().mockResolvedValue({
      data: [{ kategori: 'ALAM', label: 'Bencana Alam', jenis: ['Gempa Bumi'] }],
    }),
    opsiAsesmenAsync: vi
      .fn()
      .mockResolvedValue({ 'laporan.level': [{ kode: 'BERAT', label: 'Berat' }] }),
  };
  const laporan = {
    buatAsync: vi.fn().mockResolvedValue({ id: 'lap-1' }),
    unggahLampiranAsync: vi.fn(),
  };

  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ReferensiService, useValue: referensi },
      { provide: LaporanService, useValue: laporan },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });

  const fixture = TestBed.createComponent(LaporBencana);
  fixture.detectChanges();
  await flushAsync();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, laporan };
}

function isi(
  fixture: { nativeElement: HTMLElement; detectChanges: () => void },
  selector: string,
  value: string,
): void {
  const el = fixture.nativeElement.querySelector(selector) as HTMLInputElement | HTMLSelectElement;
  el.value = value;
  el.dispatchEvent(new Event(el.tagName === 'SELECT' ? 'change' : 'input'));
  fixture.detectChanges();
}

describe('LaporBencana', () => {
  it('tombol kirim nonaktif sebelum form terisi lengkap', async () => {
    const { fixture } = await render();
    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Kirim Laporan'),
    ) as HTMLButtonElement;
    expect(tombol.disabled).toBe(true);
  });

  it('mengirim laporan lengkap memanggil service dan beralih ke langkah lampiran', async () => {
    const { fixture, laporan } = await render();

    isi(fixture, '#jenis', 'Gempa Bumi');
    isi(fixture, '#level', 'BERAT');
    isi(fixture, '#lokasi', 'Kantor Uji');
    await flushAsync();
    fixture.detectChanges();

    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Kirim Laporan'),
    ) as HTMLButtonElement;
    expect(tombol.disabled).toBe(false);
    tombol.click();
    await flushAsync();
    fixture.detectChanges();

    expect(laporan.buatAsync).toHaveBeenCalledWith('Gempa Bumi', 'BERAT', 'Kantor Uji', undefined);
    expect(fixture.nativeElement.textContent).toContain('Lampiran');
  });

  it('tanpa izin sigap:laporan:create tidak ada formulir maupun tombol kirim', async () => {
    const { fixture } = await render([]);
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('#jenis')).toBeNull();
    expect(el.querySelectorAll('button').length).toBe(0);
  });

  it('input unggah lampiran hanya tampil dengan izin sigap:lampiran:upload', async () => {
    const tanpaUnggah = await render(['sigap:laporan:create']);
    isi(tanpaUnggah.fixture, '#jenis', 'Gempa Bumi');
    isi(tanpaUnggah.fixture, '#level', 'BERAT');
    isi(tanpaUnggah.fixture, '#lokasi', 'Kantor Uji');
    await flushAsync();
    tanpaUnggah.fixture.detectChanges();
    (
      [...tanpaUnggah.fixture.nativeElement.querySelectorAll('button')].find(
        (b: HTMLButtonElement) => b.textContent?.includes('Kirim Laporan'),
      ) as HTMLButtonElement
    ).click();
    await flushAsync();
    tanpaUnggah.fixture.detectChanges();

    expect(tanpaUnggah.fixture.nativeElement.textContent).toContain('Lampiran');
    expect(tanpaUnggah.fixture.nativeElement.querySelector('input[type="file"]')).toBeNull();
  });
});
