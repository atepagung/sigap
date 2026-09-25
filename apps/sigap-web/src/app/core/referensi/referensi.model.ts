/** Cermin `KelompokBencana` (API_CONTRACT #37). */
export interface KelompokBencana {
  readonly kategori: string;
  readonly label: string;
  readonly jenis: readonly string[];
}

/** Satu pilihan berskala (API_CONTRACT #38). */
export interface Opsi {
  readonly kode: string;
  readonly label: string;
}

/** `GET /referensi/opsi-asesmen`: peta jalur field → daftar opsi. */
export type OpsiAsesmenSemua = Readonly<Record<string, readonly Opsi[]>>;

/** Cermin `Eselon1Dto` (API_CONTRACT #41). */
export interface Eselon1 {
  readonly kode: string;
  readonly nama: string;
}
