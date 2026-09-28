export interface Terkait {
  readonly jenis: string;
  readonly id: string | null;
}

/** Butir `GET /notifikasi` (#43). */
export interface Peringatan {
  readonly kode: string;
  readonly tingkat: 'GENTING' | 'PERINGATAN' | 'INFORMASI';
  readonly judul: string;
  readonly pesan: string;
  readonly terkait: Terkait | null;
}
