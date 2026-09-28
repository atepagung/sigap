import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { KonteksSaya } from './konteks-saya.model';

/** `GET /me/konteks` (API_CONTRACT #36): identitas, lingkup, dan permission pemanggil. */
@Injectable({ providedIn: 'root' })
export class KonteksSayaService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  bacaAsync(): Promise<KonteksSaya> {
    return firstValueFrom(this.http.get<KonteksSaya>(`${this.base}/api/v1/me/konteks`));
  }
}
