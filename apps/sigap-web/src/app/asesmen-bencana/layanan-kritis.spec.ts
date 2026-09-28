import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideIamPermissions } from '@danarakca/iam';
import { AsesmenService } from './asesmen.service';
import { LayananKritisPage } from './layanan-kritis';

async function render(permissions: readonly string[] = ['sigap:layanan-kritis:create']) {
  const service = {
    layananKritisAsync: vi.fn().mockResolvedValue({
      data: [
        { id: 'lk-1', nama: 'Layanan SP2D', rtoJam: 24, rtoLabel: '1 hari', sumber: 'MANUAL' },
      ],
    }),
    tambahLayananKritisAsync: vi.fn().mockResolvedValue({
      id: 'lk-2',
      nama: 'Layanan Baru',
      rtoJam: 12,
      rtoLabel: '12 jam',
      sumber: 'MANUAL',
    }),
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: AsesmenService, useValue: service },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });

  const fixture = TestBed.createComponent(LayananKritisPage);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, service };
}

describe('LayananKritisPage', () => {
  it('menampilkan layanan kritis terdaftar', async () => {
    const { fixture } = await render();
    expect(fixture.nativeElement.textContent).toContain('Layanan SP2D');
  });

  it('form tambah tersembunyi tanpa permission sigap:layanan-kritis:create', async () => {
    const { fixture } = await render([]);
    expect(fixture.nativeElement.querySelector('#nama')).toBeNull();
  });

  it('menambah layanan memanggil service lalu memuat ulang', async () => {
    const { fixture, service } = await render();
    const nama = fixture.nativeElement.querySelector('#nama') as HTMLInputElement;
    const rto = fixture.nativeElement.querySelector('#rto') as HTMLInputElement;
    nama.value = 'Layanan Baru';
    nama.dispatchEvent(new Event('input'));
    rto.value = '12';
    rto.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Tambah'),
    ) as HTMLButtonElement;
    tombol.click();
    await flushAsync();

    expect(service.tambahLayananKritisAsync).toHaveBeenCalledWith('Layanan Baru', 12);
  });
});
