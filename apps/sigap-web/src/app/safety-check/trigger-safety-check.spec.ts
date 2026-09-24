import { flushAsync } from '../shared/testing/flush-async';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReferensiService } from '../core/referensi/referensi.service';
import { BroadcastService } from './broadcast.service';
import { TriggerSafetyCheck } from './trigger-safety-check';

const PRATINJAU = {
  lingkupPemicu: 'UNIT',
  lokasi: 'Kota Uji',
  disasar: { jumlahUnit: 2, jumlahPegawai: 30, unit: [] },
  dilewati: [],
};

async function render() {
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
});
