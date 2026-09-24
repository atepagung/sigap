import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../core/config/api-config';
import { Halaman } from '../core/models/umum.model';
import { Laporan, LampiranDto } from './laporan.model';

/** Laporkan Potensi Bencana dan Verifikasi Alert (#7–#11, #17, #18). */
@Injectable({ providedIn: 'root' })
export class LaporanService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1/laporan-bencana`;

  buatAsync(
    jenisBencana: string,
    level: string,
    lokasi: string,
    deskripsi?: string,
  ): Promise<Laporan> {
    return firstValueFrom(
      this.http.post<Laporan>(this.base, { jenisBencana, level, lokasi, deskripsi }),
    );
  }

  unggahLampiranAsync(id: string, berkas: File): Promise<LampiranDto> {
    const formData = new FormData();
    formData.append('berkas', berkas);
    return firstValueFrom(this.http.post<LampiranDto>(`${this.base}/${id}/lampiran`, formData));
  }

  sayaAsync(halaman = 1, ukuran = 20): Promise<Halaman<Laporan>> {
    return firstValueFrom(
      this.http.get<Halaman<Laporan>>(`${this.base}/saya?halaman=${halaman}&ukuran=${ukuran}`),
    );
  }

  bacaAsync(id: string): Promise<Laporan> {
    return firstValueFrom(this.http.get<Laporan>(`${this.base}/${id}`));
  }

  daftarAsync(status?: string, halaman = 1, ukuran = 20): Promise<Halaman<Laporan>> {
    const q = new URLSearchParams({ halaman: String(halaman), ukuran: String(ukuran) });
    if (status) q.set('status', status);
    return firstValueFrom(this.http.get<Halaman<Laporan>>(`${this.base}?${q.toString()}`));
  }

  verifikasiAsync(id: string, keputusan: 'VALID' | 'TOLAK', alasan?: string): Promise<Laporan> {
    return firstValueFrom(
      this.http.post<Laporan>(`${this.base}/${id}/verifikasi`, { keputusan, alasan }),
    );
  }
}
