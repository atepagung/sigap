import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { Provider, Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { AsesmenService } from '../../asesmen-bencana/asesmen.service';
import { DetailAsesmen } from '../../asesmen-bencana/detail-asesmen';
import { ReferensiService } from '../../core/referensi/referensi.service';
import { BroadcastService } from '../../safety-check/broadcast.service';
import { DetailBroadcast } from '../../safety-check/detail-broadcast';
import { DetailLaporan } from '../../verifikasi-alert/detail-laporan';
import { LaporanService } from '../../verifikasi-alert/laporan.service';
import { flushAsync } from '../testing/flush-async';

const galat404 = () => vi.fn().mockRejectedValue(new HttpErrorResponse({ status: 404 }));

async function render(komponen: Type<unknown>, providers: Provider[]) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: 'x' }) } },
      },
      provideIamPermissions(() => Promise.resolve([])),
      ...providers,
    ],
  });
  const fixture = TestBed.createComponent(komponen);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]');
}

describe('galat pemuatan halaman detail', () => {
  it('detail-asesmen menampilkan pesan saat 404', async () => {
    const alert = await render(DetailAsesmen, [
      { provide: AsesmenService, useValue: { bacaAsync: galat404() } },
      { provide: ReferensiService, useValue: { opsiAsesmenAsync: vi.fn().mockResolvedValue({}) } },
    ]);
    expect(alert?.textContent).toContain('Data tidak ditemukan');
  });

  it('detail-laporan menampilkan pesan saat 404', async () => {
    const alert = await render(DetailLaporan, [
      { provide: LaporanService, useValue: { bacaAsync: galat404() } },
    ]);
    expect(alert?.textContent).toContain('Data tidak ditemukan');
  });

  it('detail-broadcast menampilkan pesan saat 404', async () => {
    const alert = await render(DetailBroadcast, [
      { provide: BroadcastService, useValue: { bacaAsync: galat404() } },
    ]);
    expect(alert?.textContent).toContain('Data tidak ditemukan');
  });
});
