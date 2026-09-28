import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../core/config/api-config';
import { Daftar, Halaman } from '../core/models/umum.model';
import { LampiranDto } from '../verifikasi-alert/laporan.model';
import {
  Asesmen,
  AsesmenPermintaan,
  AsesmenRingkas,
  LayananKritis,
  TanggapDarurat,
  Terkini,
  Versi,
} from './asesmen.model';

/** Asesmen Kondisi Bencana 5 aspek, Layanan Kritis, dan Tanggap Darurat (#19–#29). */
@Injectable({ providedIn: 'root' })
export class AsesmenService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/api/v1`;

  layananKritisAsync(): Promise<Daftar<LayananKritis>> {
    return firstValueFrom(this.http.get<Daftar<LayananKritis>>(`${this.base}/layanan-kritis`));
  }

  tambahLayananKritisAsync(nama: string, rtoJam: number): Promise<LayananKritis> {
    return firstValueFrom(
      this.http.post<LayananKritis>(`${this.base}/layanan-kritis`, { nama, rtoJam }),
    );
  }

  kirimAsync(isi: AsesmenPermintaan): Promise<Asesmen> {
    return firstValueFrom(this.http.post<Asesmen>(`${this.base}/asesmen`, isi));
  }

  revisiAsync(id: string, isi: AsesmenPermintaan): Promise<Asesmen> {
    return firstValueFrom(this.http.post<Asesmen>(`${this.base}/asesmen/${id}/revisi`, isi));
  }

  unggahLampiranAsync(id: string, berkas: File): Promise<LampiranDto> {
    const formData = new FormData();
    formData.append('berkas', berkas);
    return firstValueFrom(
      this.http.post<LampiranDto>(`${this.base}/asesmen/${id}/lampiran`, formData),
    );
  }

  daftarAsync(
    unitId?: string,
    statusPersetujuan?: string,
    halaman = 1,
    ukuran = 20,
  ): Promise<Halaman<AsesmenRingkas>> {
    const q = new URLSearchParams({ halaman: String(halaman), ukuran: String(ukuran) });
    if (unitId) q.set('unitId', unitId);
    if (statusPersetujuan) q.set('statusPersetujuan', statusPersetujuan);
    return firstValueFrom(
      this.http.get<Halaman<AsesmenRingkas>>(`${this.base}/asesmen?${q.toString()}`),
    );
  }

  terkiniAsync(unitId?: string): Promise<Terkini> {
    const q = unitId ? `?unitId=${unitId}` : '';
    return firstValueFrom(this.http.get<Terkini>(`${this.base}/asesmen/terkini${q}`));
  }

  bacaAsync(id: string): Promise<Asesmen> {
    return firstValueFrom(this.http.get<Asesmen>(`${this.base}/asesmen/${id}`));
  }

  versiAsync(id: string): Promise<Daftar<Versi>> {
    return firstValueFrom(this.http.get<Daftar<Versi>>(`${this.base}/asesmen/${id}/versi`));
  }

  setujuiAsync(id: string): Promise<Asesmen> {
    return firstValueFrom(this.http.post<Asesmen>(`${this.base}/asesmen/${id}/persetujuan`, {}));
  }

  selesaikanTanggapDaruratAsync(id: string): Promise<TanggapDarurat> {
    return firstValueFrom(
      this.http.post<TanggapDarurat>(`${this.base}/tanggap-darurat/${id}/selesai`, {}),
    );
  }
}
