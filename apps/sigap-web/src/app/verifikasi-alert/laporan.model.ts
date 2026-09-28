import { RingkasPengguna, RingkasUnit } from '../core/models/umum.model';

export interface LampiranDto {
  readonly id: string;
  readonly tipe: string;
  readonly mimeType: string | null;
  readonly ukuranBytes: number | null;
  readonly url: string;
  readonly diunggahPada: string;
}

export interface Verifikasi {
  readonly keputusan: 'VALID' | 'TOLAK';
  readonly alasan: string | null;
  readonly oleh: RingkasPengguna;
  readonly pada: string;
}

/** Cermin `LaporanDto` (API_CONTRACT bagian 3.2). */
export interface Laporan {
  readonly id: string;
  readonly unit: RingkasUnit;
  readonly pelapor: RingkasPengguna;
  readonly kategoriBencana: string | null;
  readonly jenisBencana: string;
  readonly level: string;
  readonly lokasi: string;
  readonly deskripsi: string | null;
  readonly status: 'MENUNGGU' | 'TERVERIFIKASI' | 'DITOLAK';
  readonly verifikasi: Verifikasi | null;
  readonly lampiran: readonly LampiranDto[];
  readonly dilaporkanPada: string;
}
