import { Asesmen } from '../asesmen-bencana/asesmen.model';
import { RingkasUnit } from '../core/models/umum.model';
import { RingkasBroadcast } from '../safety-check/safety-check.model';

export interface LingkupMonitor {
  readonly jenis: string;
  readonly label: string | null;
  readonly penyaringAktif: Readonly<Record<string, string>>;
}

export interface SafetyCheckAgregat {
  readonly totalPegawai: number;
  readonly aman: number;
  readonly butuhBantuan: number;
  readonly belumMerespons: number;
  readonly tingkatRespons: number;
  readonly jumlahUnitDisasar: number;
  readonly jumlahUnitBelumDisasar: number;
}

export interface AsesmenAgregat {
  readonly unitMelapor: number;
  readonly menungguPimpinan: number;
  readonly disetujui: number;
}

export interface LayananAgregat {
  readonly normal: number;
  readonly terganggu: number;
  readonly berhentiTotal: number;
}

/** `GET /monitor/ringkasan` (#30). */
export interface RingkasanMonitor {
  readonly lingkup: LingkupMonitor;
  readonly jenisBencana: string | null;
  readonly jenisAktif: readonly string[];
  readonly safetyCheck: SafetyCheckAgregat;
  readonly asesmen: AsesmenAgregat;
  readonly tanggapDarurat: { readonly unitDarurat: number };
  readonly layanan: LayananAgregat;
}

/** Butir `GET /monitor/safety-check` (#31). */
export interface SafetyCheckKelompok {
  readonly kelompok: { readonly kode: string; readonly label: string };
  readonly totalPegawai: number;
  readonly aman: number;
  readonly butuhBantuan: number;
  readonly belumMerespons: number;
  readonly tingkatRespons: number;
}

/** Butir `GET /monitor/asesmen-masuk` (#32). */
export interface AsesmenMasuk {
  readonly asesmenId: string;
  readonly unit: RingkasUnit;
  readonly jenisBencana: string;
  readonly urutan: number;
  readonly dikirimPada: string;
  readonly statusPersetujuan: string;
  readonly tanggapDarurat: { readonly id: string; readonly status: string } | null;
}

export type Histogram = Readonly<Record<string, number>>;

/** `GET /monitor/aspek` (#33). */
export interface AspekAgregat {
  readonly jumlahUnitMelapor: number;
  readonly sdm: {
    readonly kelengkapanHadir: Histogram;
    readonly unitAdaKorbanJiwa: number;
    readonly unitAdaLukaBerat: number;
    readonly unitAdaTraumaBerat: number;
  };
  readonly aset: {
    readonly konstruksiBangunan: Histogram;
    readonly aksesLokasi: Histogram;
    readonly kendaraanLaikOperasi: Histogram;
  };
  readonly tik: {
    readonly aksesJaringan: Histogram;
    readonly kelistrikan: Histogram;
    readonly aplikasiUtama: Histogram;
  };
  readonly arsip: { readonly arsipVital: Histogram; readonly evakuasiFisik: Histogram };
  readonly layanan: LayananAgregat;
}

/** Butir `GET /monitor/layanan` (#34). */
export interface LayananGangguan {
  readonly layanan: { readonly id: string; readonly nama: string; readonly rtoJam: number };
  readonly unit: RingkasUnit;
  readonly status: 'TERGANGGU' | 'BERHENTI_TOTAL';
  readonly sejak: string;
  readonly sisaRtoJam: number;
}

/** `GET /monitor/unit/{unitId}` (#35). */
export interface UnitDetail {
  readonly unit: RingkasUnit;
  readonly tanggapDarurat: {
    readonly id: string;
    readonly status: string;
    readonly jenisBencana: string;
    readonly sejak: string;
  } | null;
  readonly safetyCheck: readonly {
    readonly broadcast: RingkasBroadcast;
    readonly totalPegawai: number;
    readonly aman: number;
    readonly butuhBantuan: number;
    readonly belumMerespons: number;
  }[];
  readonly asesmenTerkini: Asesmen | null;
  readonly layananTerganggu: readonly LayananGangguan[];
}
