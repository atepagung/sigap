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

/** Status yang pesannya ditulis API untuk pengguna (validasi dan aturan bisnis), bukan galat teknis. */
const STATUS_PESAN_API = new Set([400, 409, 422]);

/**
 * Pesan galat aksi tulis (kirim, setujui, simpan). Untuk 400/409/422, API menjawab
 * `application/problem+json` dengan `detail` berbahasa Indonesia yang ditujukan bagi pengguna
 * (mis. "Sebutkan dari mana keadaan ini diketahui, minimal lima huruf."), jadi ditampilkan apa
 * adanya. Status lain memakai pesan tetap: tidak ada rincian teknis yang sampai ke layar, dan 404
 * tetap tidak dibedakan dari "tidak ada" (data di luar Scope dijawab 404).
 */
export function pesanGalatAksi(galat: unknown): string {
  const status = galat instanceof HttpErrorResponse ? galat.status : -1;
  if (galat instanceof HttpErrorResponse && STATUS_PESAN_API.has(status)) {
    const detail = (galat.error as { detail?: unknown } | null)?.detail;
    if (typeof detail === 'string' && detail.trim().length > 0) {
      return detail;
    }

    return 'Isian tidak dapat diproses. Periksa kembali lalu coba lagi.';
  }

  switch (status) {
    case 0:
      return 'Tidak dapat terhubung ke server. Periksa koneksi Anda lalu coba lagi.';
    case 403:
      return 'Anda tidak memiliki izin untuk melakukan aksi ini.';
    case 404:
      return 'Data tidak ditemukan.';
    default:
      return 'Aksi gagal diproses. Coba lagi beberapa saat lagi.';
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

  private readonly _pesanAksi = signal<string | null>(null);

  /** Galat aksi tulis terakhir; terpisah dari {@link pesan} (galat memuat) supaya tampil dekat aksinya. */
  readonly pesanAksi = this._pesanAksi.asReadonly();

  /** @returns hasil `kerja`, atau `null` bila gagal (pesan sudah diisi / sudah dialihkan). */
  jalankanAsync<T>(kerja: () => Promise<T>): Promise<T | null> {
    this._pesan.set(null);
    return this.bungkus(kerja, pesanGalatMuat, this._pesan);
  }

  /**
   * Seperti {@link jalankanAsync} untuk aksi tulis (POST/PUT/DELETE): galat menjadi
   * {@link pesanAksi}, bukan galat tak tertangani. Pesan lama dihapus di awal tiap percobaan.
   */
  jalankanAksiAsync<T>(kerja: () => Promise<T>): Promise<T | null> {
    this._pesanAksi.set(null);
    return this.bungkus(kerja, pesanGalatAksi, this._pesanAksi);
  }

  /** Untuk komponen yang membuka ulang atau menutup dialog: pesan percobaan sebelumnya tak boleh terbawa. */
  hapusPesanAksi(): void {
    this._pesanAksi.set(null);
  }

  private bungkus<T>(
    kerja: () => Promise<T>,
    pesanDari: (galat: unknown) => string,
    tujuan: { set(nilai: string | null): void },
  ): Promise<T | null> {
    // `then` dua argumen (bukan async/try): sesedikit mungkin putaran microtask tambahan.
    return kerja().then(
      (hasil): T | null => hasil,
      (galat: unknown): null => {
        if (galat instanceof HttpErrorResponse && galat.status === 401) {
          this.injector.get(TokenProvider).keluar();
          void this.router.navigate(['masuk']);
        } else {
          tujuan.set(pesanDari(galat));
        }
        return null;
      },
    );
  }
}
