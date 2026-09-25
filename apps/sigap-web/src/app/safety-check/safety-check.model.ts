import { RingkasPengguna, RingkasUnit } from '../core/models/umum.model';

/** `AMAN` atau `BUTUH_BANTUAN` — koreksi stakeholder 2: hanya dua pilihan. */
export type StatusSafety = 'AMAN' | 'BUTUH_BANTUAN';

export interface PemicuDto {
  readonly pengguna: RingkasPengguna;
  readonly peran: string;
  readonly unit: RingkasUnit;
}

/** Cermin `RingkasBroadcastDto`. */
export interface RingkasBroadcast {
  readonly id: string;
  readonly kategoriBencana: string;
  readonly jenisBencana: string;
  readonly lokasi: string;
  readonly sumber: 'OTOMATIS_BMKG' | 'MANUAL';
  readonly lingkup: string;
  readonly dipicuPada: string;
  readonly status: 'AKTIF' | 'SELESAI';
  readonly pemicu: PemicuDto;
}

export interface ResponsSaya {
  readonly status: StatusSafety;
  readonly dijawabPada: string;
}

/** Butir `GET /safety-check/aktif` (#1). */
export interface Aktif {
  readonly broadcast: RingkasBroadcast;
  readonly pesan: string;
  readonly responsSaya: ResponsSaya | null;
}

/** Butir `GET /safety-check/respons-saya` (#3). */
export interface RiwayatSaya {
  readonly broadcast: RingkasBroadcast;
  readonly status: StatusSafety;
  readonly dijawabPada: string;
  readonly dicatatkanSatgas: boolean;
}

export interface DicatatOlehRingkas {
  readonly id: string;
  readonly nama: string;
}

export interface LokasiTerakhir {
  readonly lat: number;
  readonly lng: number;
  readonly pada: string;
}

/** Baris rekap (#4). Tiga field terakhir Sieve — `null` bagi peran yang tidak berhak. */
export interface RekapBaris {
  readonly pegawai: RingkasPengguna;
  readonly status: StatusSafety | 'BELUM';
  readonly dijawabPada: string | null;
  readonly dicatatkan: boolean;
  readonly dicatatOleh: DicatatOlehRingkas | null;
  readonly keterangan: string | null;
  readonly lokasiTerakhir: LokasiTerakhir | null;
}

export interface BroadcastLainAktif {
  readonly id: string;
  readonly jenisBencana: string;
}

/** `GET /safety-check/rekap` (#4) — amplop sendiri, bukan `Halaman<T>` generik. */
export interface Rekap {
  readonly broadcast: RingkasBroadcast;
  readonly broadcastLainAktif: readonly BroadcastLainAktif[];
  readonly unit: RingkasUnit;
  readonly data: readonly RekapBaris[];
  readonly halaman: number;
  readonly ukuran: number;
  readonly total: number;
}

/** `GET /safety-check/rekap/ringkasan` (#5) — dasar tiga tab (koreksi stakeholder 11). */
export interface RingkasanRekap {
  readonly broadcast: RingkasBroadcast;
  readonly unit: RingkasUnit;
  readonly totalPegawai: number;
  readonly aman: number;
  readonly butuhBantuan: number;
  readonly belumMerespons: number;
  readonly tingkatRespons: number;
}
