import {
  EnvironmentProviders,
  Injectable,
  inject,
  provideAppInitializer,
  signal,
} from '@angular/core';

/**
 * [ASUMSI] Penyimpan permission pengguna di sisi tampilan. Dari mana platform menyediakan daftar
 * permission ke remote belum diketahui (PLAYBOOK Lampiran E #12; PERMISSION_MAP bagian 8 no. 5).
 *
 * Ini hanya untuk MENYEMBUNYIKAN elemen. Keamanan sesungguhnya tetap di API
 * ([KemenkeuAuthorize] + Scope + Sieve); elemen yang disembunyikan di sini tetap ditolak API
 * kalau dipanggil langsung.
 */
@Injectable({ providedIn: 'root' })
export class IamPermissions {
  private readonly granted = signal<ReadonlySet<string>>(new Set());

  /** Reaktif: pemanggil di dalam template/effect ikut diperbarui saat daftar berubah. */
  has(permission: string): boolean {
    return this.granted().has(permission);
  }

  /** @internal Diisi oleh {@link provideIamPermissions}. */
  replace(permissions: readonly string[]): void {
    this.granted.set(new Set(permissions));
  }
}

/**
 * [ASUMSI] Titik pasang sumber permission — satu baris di app.config yang diganti saat
 * mekanisme platform diketahui. Tidak menahan bootstrap: sebelum daftar tiba, semua elemen
 * ber-*hasPermission tersembunyi (fail-closed), lalu muncul begitu daftar tersedia.
 */
export function provideIamPermissions(load: () => Promise<readonly string[]>): EnvironmentProviders {
  return provideAppInitializer(() => {
    const permissions = inject(IamPermissions);
    load()
      .then((list) => permissions.replace(list))
      .catch((err) => console.error('Gagal memuat permission pengguna; semua elemen berizin disembunyikan.', err));
  });
}
