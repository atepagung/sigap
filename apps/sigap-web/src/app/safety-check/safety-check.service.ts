import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../core/config/api-config';
import { Daftar, Halaman } from '../core/models/umum.model';
import { Aktif, Rekap, RingkasanRekap, RiwayatSaya } from './safety-check.model';

/** Safety Check / SOS pribadi dan rekap unit (#1–#6). */
@Injectable({ providedIn: 'root' })
export class SafetyCheckService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1/safety-check`;

  aktifAsync(): Promise<Daftar<Aktif>> {
    return firstValueFrom(this.http.get<Daftar<Aktif>>(`${this.base}/aktif`));
  }

  jawabSayaAsync(broadcastId: string, status: 'AMAN' | 'BUTUH_BANTUAN'): Promise<unknown> {
    return firstValueFrom(
      this.http.put(`${this.base}/broadcast/${broadcastId}/respons-saya`, { status }),
    );
  }

  riwayatSayaAsync(halaman = 1, ukuran = 20): Promise<Halaman<RiwayatSaya>> {
    return firstValueFrom(
      this.http.get<Halaman<RiwayatSaya>>(
        `${this.base}/respons-saya?halaman=${halaman}&ukuran=${ukuran}`,
      ),
    );
  }

  /** #6: Tim Satgas mencatatkan keadaan pegawai yang tidak dapat menjawab sendiri. */
  catatAsync(
    broadcastId: string,
    pegawaiId: string,
    status: 'AMAN' | 'BUTUH_BANTUAN',
    alasan: string,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.put(`${this.base}/broadcast/${broadcastId}/respons/${pegawaiId}`, {
        status,
        alasan,
      }),
    );
  }

  rekapAsync(
    broadcastId?: string,
    status?: string,
    cari?: string,
    halaman = 1,
    ukuran = 50,
  ): Promise<Rekap> {
    const q = new URLSearchParams({ halaman: String(halaman), ukuran: String(ukuran) });
    if (broadcastId) q.set('broadcastId', broadcastId);
    if (status) q.set('status', status);
    if (cari) q.set('cari', cari);
    return firstValueFrom(this.http.get<Rekap>(`${this.base}/rekap?${q.toString()}`));
  }

  ringkasanRekapAsync(broadcastId?: string): Promise<RingkasanRekap> {
    const q = broadcastId ? `?broadcastId=${broadcastId}` : '';
    return firstValueFrom(this.http.get<RingkasanRekap>(`${this.base}/rekap/ringkasan${q}`));
  }
}
