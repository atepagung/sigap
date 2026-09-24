import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../core/config/api-config';
import { Halaman } from '../core/models/umum.model';
import { DetailBroadcast, PicuPermintaan, Pratinjau, RiwayatBroadcast } from './broadcast.model';

/** Trigger Safety Check / Broadcast (#12–#16). */
@Injectable({ providedIn: 'root' })
export class BroadcastService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1/safety-check/broadcast`;

  pratinjauAsync(
    jenisBencana: string,
    penyempit?: { unitId?: string; provinsi?: string; kabupatenKota?: string; eselonI?: string },
  ): Promise<Pratinjau> {
    const q = new URLSearchParams({ jenisBencana });
    if (penyempit?.unitId) q.set('unitId', penyempit.unitId);
    if (penyempit?.provinsi) q.set('provinsi', penyempit.provinsi);
    if (penyempit?.kabupatenKota) q.set('kabupatenKota', penyempit.kabupatenKota);
    if (penyempit?.eselonI) q.set('eselonI', penyempit.eselonI);
    return firstValueFrom(this.http.get<Pratinjau>(`${this.base}/pratinjau?${q.toString()}`));
  }

  picuAsync(permintaan: PicuPermintaan): Promise<DetailBroadcast> {
    return firstValueFrom(this.http.post<DetailBroadcast>(this.base, permintaan));
  }

  daftarAsync(
    status?: string,
    jenisBencana?: string,
    sumber?: string,
    halaman = 1,
    ukuran = 20,
  ): Promise<Halaman<RiwayatBroadcast>> {
    const q = new URLSearchParams({ halaman: String(halaman), ukuran: String(ukuran) });
    if (status) q.set('status', status);
    if (jenisBencana) q.set('jenisBencana', jenisBencana);
    if (sumber) q.set('sumber', sumber);
    return firstValueFrom(this.http.get<Halaman<RiwayatBroadcast>>(`${this.base}?${q.toString()}`));
  }

  bacaAsync(id: string): Promise<DetailBroadcast> {
    return firstValueFrom(this.http.get<DetailBroadcast>(`${this.base}/${id}`));
  }

  selesaiAsync(id: string, alasan?: string): Promise<DetailBroadcast> {
    return firstValueFrom(
      this.http.post<DetailBroadcast>(`${this.base}/${id}/selesai`, { alasan }),
    );
  }
}
