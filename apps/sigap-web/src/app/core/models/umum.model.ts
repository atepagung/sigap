/** Cermin `RingkasUnit` (API_CONTRACT, seluruh domain). */
export interface RingkasUnit {
  readonly id: string;
  readonly nama: string;
  readonly provinsi: string | null;
  readonly kabupatenKota: string | null;
  readonly eselonI: string | null;
}

/** Cermin `RingkasPengguna`. */
export interface RingkasPengguna {
  readonly id: string;
  readonly nama: string;
  readonly nip: string | null;
  readonly jabatan: string | null;
}

/** Amplop koleksi berhalaman (API_CONTRACT bagian 1.4). */
export interface Halaman<T> {
  readonly data: readonly T[];
  readonly halaman: number;
  readonly ukuran: number;
  readonly total: number;
}

/** Amplop daftar tanpa halaman (`{ data: [...] }`). */
export interface Daftar<T> {
  readonly data: readonly T[];
}

/** Bentuk galat `problem+json` (API_CONTRACT bagian 1.5). */
export interface GalatApi {
  readonly kode: string;
  readonly title?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}
