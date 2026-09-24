import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { Daftar, RingkasUnit } from '../models/umum.model';
import { Eselon1, KelompokBencana, OpsiAsesmenSemua } from './referensi.model';

/** Data rujukan (API_CONTRACT #37–#42): taksonomi bencana, opsi asesmen, dan lingkup organisasi. */
@Injectable({ providedIn: 'root' })
export class ReferensiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1/referensi`;

  jenisBencanaAsync(): Promise<Daftar<KelompokBencana>> {
    return firstValueFrom(this.http.get<Daftar<KelompokBencana>>(`${this.base}/jenis-bencana`));
  }

  opsiAsesmenAsync(): Promise<OpsiAsesmenSemua> {
    return firstValueFrom(this.http.get<OpsiAsesmenSemua>(`${this.base}/opsi-asesmen`));
  }

  provinsiAsync(): Promise<Daftar<string>> {
    return firstValueFrom(this.http.get<Daftar<string>>(`${this.base}/provinsi`));
  }

  kabupatenKotaAsync(provinsi?: string): Promise<Daftar<string>> {
    const q = provinsi ? `?provinsi=${encodeURIComponent(provinsi)}` : '';
    return firstValueFrom(this.http.get<Daftar<string>>(`${this.base}/kabupaten-kota${q}`));
  }

  eselon1Async(): Promise<Daftar<Eselon1>> {
    return firstValueFrom(this.http.get<Daftar<Eselon1>>(`${this.base}/eselon-1`));
  }

  unitAsync(ukuran = 100): Promise<{ data: readonly RingkasUnit[]; total: number }> {
    return firstValueFrom(
      this.http.get<{ data: readonly RingkasUnit[]; total: number }>(
        `${this.base}/unit?ukuran=${ukuran}`,
      ),
    );
  }
}
