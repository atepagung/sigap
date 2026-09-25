import { RingkasPengguna, RingkasUnit } from '../core/models/umum.model';
import { PemicuDto, RingkasBroadcast } from './safety-check.model';

export interface DisasarPratinjau {
  readonly jumlahUnit: number;
  readonly jumlahPegawai: number;
  readonly unit: readonly RingkasUnit[];
}

export interface PemicuSingkat {
  readonly nama: string;
  readonly peran: string;
}

export interface Pemegang {
  readonly broadcastId: string;
  readonly jenisBencana: string;
  readonly pemicu: PemicuSingkat;
  readonly dipicuPada: string;
}

export interface Dilewati {
  readonly unit: RingkasUnit;
  readonly dipegangOleh: Pemegang;
}

/** `GET /safety-check/broadcast/pratinjau` (#12). */
export interface Pratinjau {
  readonly lingkupPemicu: string;
  readonly lokasi: string;
  readonly disasar: DisasarPratinjau;
  readonly dilewati: readonly Dilewati[];
}

export interface Penyempit {
  readonly unitId?: string;
  readonly provinsi?: string;
  readonly kabupatenKota?: string;
  readonly eselonI?: string;
}

/** Body `POST /safety-check/broadcast` (#13). */
export interface PicuPermintaan {
  readonly kategoriBencana?: string;
  readonly jenisBencana: string;
  readonly pesan?: string;
  readonly penyempit?: Penyempit;
}

/** Baris `GET /safety-check/broadcast` (#14). */
export interface RiwayatBroadcast extends RingkasBroadcast {
  readonly jumlahUnitDisasar: number;
  readonly jumlahPegawaiDisasar: number;
  readonly jumlahMenjawab: number;
}

export interface Kriteria {
  readonly unitId: string | null;
  readonly provinsi: string | null;
  readonly kabupatenKota: string | null;
  readonly eselonI: string | null;
}

export interface Sasaran {
  readonly jumlahUnitDisasar: number;
  readonly jumlahPegawaiDisasar: number;
  readonly jumlahMenjawab: number;
  readonly unitDisasar: readonly RingkasUnit[];
  readonly unitDilewati: readonly Dilewati[];
}

export interface Diakhiri {
  readonly oleh: RingkasPengguna;
  readonly pada: string;
  readonly alasan: string | null;
}

/** `DetailBroadcast` (#15), juga hasil #13/#16. */
export interface DetailBroadcast {
  readonly id: string;
  readonly kategoriBencana: string;
  readonly jenisBencana: string;
  readonly pesan: string;
  readonly lokasi: string;
  readonly sumber: string;
  readonly mmiTertinggi: number | null;
  readonly lingkup: string;
  readonly kriteria: Kriteria;
  readonly pemicu: PemicuDto;
  readonly dipicuPada: string;
  readonly status: 'AKTIF' | 'SELESAI';
  readonly diakhiri: Diakhiri | null;
  readonly sasaran: Sasaran;
}
