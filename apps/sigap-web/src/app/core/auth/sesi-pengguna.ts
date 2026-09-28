import { Injectable, signal } from '@angular/core';
import { KonteksSaya } from './konteks-saya.model';

/**
 * Konteks pengguna yang sedang masuk (API_CONTRACT #36), untuk ditampilkan — BUKAN untuk
 * menyaring data. AGENTS.md bagian 5: `lingkup` hanya label tampilan, keputusan akses tetap
 * di API. Diisi `masuk.ts` setelah login berhasil.
 */
@Injectable({ providedIn: 'root' })
export class SesiPengguna {
  private readonly _konteks = signal<KonteksSaya | null>(null);

  readonly konteks = this._konteks.asReadonly();

  atur(konteks: KonteksSaya): void {
    this._konteks.set(konteks);
  }

  bersihkan(): void {
    this._konteks.set(null);
  }
}
