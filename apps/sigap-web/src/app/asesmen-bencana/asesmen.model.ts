import { RingkasPengguna, RingkasUnit } from '../core/models/umum.model';
import { LampiranDto } from '../verifikasi-alert/laporan.model';

export interface KondisiBencana {
  readonly kategoriBencana: string | null;
  readonly jenisBencana: string | null;
  readonly waktuKejadian: string | null;
  readonly kondisiFisik: string | null;
  readonly uraian: string | null;
}

/** `catatanKondisiPegawai`/`catatanTambahan` di-Sieve — `null` bila pemanggil tidak berhak (#26). */
export interface AspekSdm {
  readonly kelengkapanHadir: string | null;
  readonly korbanJiwa: string | null;
  readonly kondisiFisik: string | null;
  readonly kondisiPsikis: string | null;
  readonly catatanKondisiPegawai: string | null;
  readonly catatanTambahan: string | null;
}

export interface AspekAset {
  readonly konstruksiBangunan: string | null;
  readonly aksesLokasi: string | null;
  readonly kondisiPeralatan: string | null;
  readonly jumlahPeralatan: string | null;
  readonly kondisiPerlengkapan: string | null;
  readonly jumlahPerlengkapan: string | null;
  readonly kendaraanLaikOperasi: string | null;
  readonly jumlahKendaraan: string | null;
  readonly catatan: string | null;
}

export interface AspekTik {
  readonly kondisiPerangkat: string | null;
  readonly jumlahPerangkat: string | null;
  readonly aksesJaringan: string | null;
  readonly kelistrikan: string | null;
  readonly aplikasiUtama: string | null;
  readonly catatan: string | null;
}

export interface AspekArsip {
  readonly arsipVital: string | null;
  readonly arsipPenting: string | null;
  readonly evakuasiFisik: string | null;
  readonly catatan: string | null;
}

export interface LayananAsesmen {
  readonly layananId: string | null;
  readonly nama: string | null;
  readonly rtoJam: number | null;
  readonly status: string | null;
}

export interface Aspek {
  readonly sdm: AspekSdm | null;
  readonly aset: AspekAset | null;
  readonly tik: AspekTik | null;
  readonly arsip: AspekArsip | null;
  readonly layanan: readonly LayananAsesmen[] | null;
}

export interface TanggapDaruratRingkas {
  readonly id: string;
  readonly status: string;
}

/** `MENUNGGU_PIMPINAN` selama seri belum disetujui, lalu `DISETUJUI`. */
export interface Persetujuan {
  readonly status: 'MENUNGGU_PIMPINAN' | 'DISETUJUI';
  readonly disetujuiOleh: RingkasPengguna | null;
  readonly disetujuiPada: string | null;
  readonly tanggapDarurat: TanggapDaruratRingkas | null;
}

/** Objek `Asesmen` (API_CONTRACT 3.5.2). */
export interface Asesmen {
  readonly id: string;
  readonly unit: RingkasUnit;
  readonly dikirimOleh: RingkasPengguna;
  readonly dikirimPada: string;
  readonly urutan: number;
  readonly kondisiBencana: KondisiBencana;
  readonly aspek: Aspek;
  readonly persetujuan: Persetujuan;
  readonly lampiran: readonly LampiranDto[];
}

export interface AsesmenRingkas {
  readonly id: string;
  readonly unit: RingkasUnit;
  readonly jenisBencana: string;
  readonly urutan: number;
  readonly dikirimPada: string;
  readonly dikirimOleh: RingkasPengguna;
  readonly statusPersetujuan: string;
}

/** `GET /asesmen/terkini` (#25). `asesmen` `null` bila belum ada. */
export interface Terkini {
  readonly asesmen: Asesmen | null;
  readonly urutanBerikutnya: number;
}

export interface Versi {
  readonly id: string;
  readonly urutan: number;
  readonly dikirimPada: string;
  readonly dikirimOleh: RingkasPengguna;
}

export interface LayananKritis {
  readonly id: string;
  readonly nama: string;
  readonly rtoJam: number;
  readonly rtoLabel: string;
  readonly sumber: 'ADB' | 'MANUAL';
}

export interface TanggapDarurat {
  readonly id: string;
  readonly status: 'DARURAT' | 'PULIH';
  readonly jenisBencana: string;
  readonly sejak: string;
  readonly selesaiPada: string | null;
}

/** Isian yang dikirim `POST /asesmen` (#21) dan `POST /asesmen/{id}/revisi` (#22). */
export interface AsesmenPermintaan {
  readonly kondisiBencana?: { jenisBencana: string; kondisiFisik: string; uraian?: string };
  readonly aspek?: {
    sdm?: Record<string, string | null>;
    aset?: Record<string, string | null>;
    tik?: Record<string, string | null>;
    arsip?: Record<string, string | null>;
    layanan?: readonly { layananId: string; status: string }[];
  };
}
