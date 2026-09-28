import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../core/config/api-config';
import { Halaman } from '../core/models/umum.model';
import {
  AsesmenMasuk,
  AspekAgregat,
  LayananGangguan,
  RingkasanMonitor,
  SafetyCheckKelompok,
  UnitDetail,
} from './monitor.model';

/** Dashboard Monitor SC & Sumber Daya (#30–#35). */
@Injectable({ providedIn: 'root' })
export class MonitorService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1/monitor`;

  ringkasanAsync(): Promise<RingkasanMonitor> {
    return firstValueFrom(this.http.get<RingkasanMonitor>(`${this.base}/ringkasan`));
  }

  safetyCheckAsync(
    kelompok: string,
    halaman = 1,
    ukuran = 50,
  ): Promise<Halaman<SafetyCheckKelompok>> {
    return firstValueFrom(
      this.http.get<Halaman<SafetyCheckKelompok>>(
        `${this.base}/safety-check?kelompok=${kelompok}&halaman=${halaman}&ukuran=${ukuran}`,
      ),
    );
  }

  asesmenMasukAsync(halaman = 1, ukuran = 20): Promise<Halaman<AsesmenMasuk>> {
    return firstValueFrom(
      this.http.get<Halaman<AsesmenMasuk>>(
        `${this.base}/asesmen-masuk?halaman=${halaman}&ukuran=${ukuran}`,
      ),
    );
  }

  aspekAsync(): Promise<AspekAgregat> {
    return firstValueFrom(this.http.get<AspekAgregat>(`${this.base}/aspek`));
  }

  layananAsync(status?: string, halaman = 1, ukuran = 50): Promise<Halaman<LayananGangguan>> {
    const q = new URLSearchParams({ halaman: String(halaman), ukuran: String(ukuran) });
    if (status) q.set('status', status);
    return firstValueFrom(
      this.http.get<Halaman<LayananGangguan>>(`${this.base}/layanan?${q.toString()}`),
    );
  }

  unitAsync(unitId: string): Promise<UnitDetail> {
    return firstValueFrom(this.http.get<UnitDetail>(`${this.base}/unit/${unitId}`));
  }
}
