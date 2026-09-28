import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { ReferensiService } from '../core/referensi/referensi.service';
import { BroadcastService } from './broadcast.service';
import { TriggerSafetyCheck } from './trigger-safety-check';

const PRATINJAU = {
  lingkupPemicu: 'UNIT',
  lokasi: 'Kota Uji',
  disasar: { jumlahUnit: 2, jumlahPegawai: 30, unit: [] },
  dilewati: [],
};

async function render(permissions: readonly string[] = ['sigap:broadcast:trigger']) {
  const referensi = {
    jenisBencanaAsync: vi.fn().mockResolvedValue({
      data: [{ kategori: 'ALAM', label: 'Bencana Alam', jenis: ['Gempa Bumi', 'Banjir'] }],
    }),
  };
  const broadcast = {
    pratinjauAsync: vi.fn().mockResolvedValue(PRATINJAU),
    picuAsync: vi.fn().mockResolvedValue({ id: 'bc-baru' }),
  };

  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ReferensiService, useValue: referensi },
      { provide: BroadcastService, useValue: broadcast },
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });

  const fixture = TestBed.createComponent(TriggerSafetyCheck);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return { fixture, referensi, broadcast };
}

describe('TriggerSafetyCheck', () => {
  it('memuat daftar jenis bencana ke opsi <select>', async () => {
    const { fixture } = await render();
    const opsi = [...fixture.nativeElement.querySelectorAll('#jenis option')].map(
      (o: HTMLOptionElement) => o.value,
    );
    expect(opsi).toContain('Gempa Bumi');
    expect(opsi).toContain('Banjir');
  });

  it('pratinjau menampilkan angka sasaran', async () => {
    const { fixture } = await render();
    const select = fixture.nativeElement.querySelector('#jenis') as HTMLSelectElement;
    select.value = 'Gempa Bumi';
    select.dispatchEvent(new Event('change'));
    await flushAsync();
    fixture.detectChanges(); // tombol baru aktif setelah tampilan diperbarui

    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Pratinjau'),
    ) as HTMLButtonElement;
    tombol.click();
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('30');
    expect(
      [...fixture.nativeElement.querySelectorAll('button')].some((b: HTMLButtonElement) =>
        b.textContent?.includes('Picu Safety Check'),
      ),
    ).toBe(true);
  });

  it('tanpa izin sigap:broadcast:trigger tidak ada formulir maupun tombol aksi', async () => {
    const { fixture } = await render([]);
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('#jenis')).toBeNull();
    expect(el.querySelectorAll('button').length).toBe(0);
    expect(el.textContent).toContain('Trigger Safety Check');
  });

  it('izin lain (mis. baca broadcast) tidak cukup untuk memicu', async () => {
    const { fixture } = await render(['sigap:broadcast:read']);

    expect(fixture.nativeElement.querySelector('#jenis')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('button').length).toBe(0);
  });
});
