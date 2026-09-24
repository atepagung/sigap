import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, Injector, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TokenProvider } from '../auth/token-provider';

/**
 * Pesan galat pemuatan halaman. 404 sengaja tidak dibedakan dari "memang tidak ada": data di luar
 * Scope dijawab 404 oleh API (AGENTS.md bagian 5), jadi UI tidak boleh membocorkan keberadaannya.
 */
export function pesanGalatMuat(galat: unknown): string {
  const status = galat instanceof HttpErrorResponse ? galat.status : -1;
  switch (status) {
    case 0:
      return 'Tidak dapat terhubung ke server. Periksa koneksi Anda lalu muat ulang halaman.';
    case 403:
      return 'Anda tidak memiliki izin untuk membuka halaman ini.';
    case 404:
      return 'Data tidak ditemukan.';
    default:
      return 'Terjadi kesalahan saat memuat data. Coba lagi beberapa saat lagi.';
  }
}

/**
 * Satu instans per komponen halaman (`providers: [PenampungGalat]`). Membungkus pemuatan data
 * supaya galat API tampil sebagai pesan, bukan layar kosong dengan galat hanya di konsol.
 * 401 (sesi tidak berlaku) mengarahkan ke `masuk`.
 */
@Injectable()
export class PenampungGalat {
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);

  private readonly _pesan = signal<string | null>(null);

  readonly pesan = this._pesan.asReadonly();

  /** @returns hasil `kerja`, atau `null` bila gagal (pesan sudah diisi / sudah dialihkan). */
  jalankanAsync<T>(kerja: () => Promise<T>): Promise<T | null> {
    this._pesan.set(null);
    // `then` dua argumen (bukan async/try): sesedikit mungkin putaran microtask tambahan.
    return kerja().then(
      (hasil): T | null => hasil,
      (galat: unknown): null => {
        if (galat instanceof HttpErrorResponse && galat.status === 401) {
          this.injector.get(TokenProvider).keluar();
          void this.router.navigate(['masuk']);
        } else {
          this._pesan.set(pesanGalatMuat(galat));
        }
        return null;
      },
    );
  }
}
