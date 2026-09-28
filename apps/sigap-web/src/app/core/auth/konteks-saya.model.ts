/** Cermin `KonteksSayaDto` (API_CONTRACT #36). */
export interface KonteksSaya {
  readonly pengguna: {
    readonly id: string | null;
    readonly nip: string | null;
    readonly nama: string | null;
  };
  readonly unit: {
    readonly id: string;
    readonly kode: string | null;
    readonly nama: string | null;
    readonly provinsi: string | null;
    readonly eselonI: string | null;
  } | null;
  readonly peran: readonly string[];
  readonly permission: readonly string[];
  readonly lingkup: {
    readonly jenis: string;
    readonly label: string | null;
    readonly catatan: string | null;
  };
}
