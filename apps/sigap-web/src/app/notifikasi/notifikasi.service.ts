import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../core/config/api-config';
import { Daftar } from '../core/models/umum.model';
import { Peringatan } from './notifikasi.model';

/**
 * Peringatan (#43) dan langganan Web Push (#44, #45). Alur peramban-nya (izin, service worker, kunci
 * VAPID) ada di `LanggananPush`; berkas ini hanya memanggil API.
 */
@Injectable({ providedIn: 'root' })
export class NotifikasiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1/notifikasi`;

  peringatanAsync(): Promise<Daftar<Peringatan>> {
    return firstValueFrom(this.http.get<Daftar<Peringatan>>(this.base));
  }

  langgananAsync(
    endpoint: string,
    p256dh: string,
    auth: string,
    peramban?: string,
  ): Promise<unknown> {
    return firstValueFrom(
      this.http.post(`${this.base}/langganan`, { endpoint, keys: { p256dh, auth }, peramban }),
    );
  }

  hapusLanggananAsync(endpoint: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.base}/langganan`, { body: { endpoint } }));
  }
}
