import { Injectable, signal } from '@angular/core';

export interface PenggunaShell {
  readonly nama: string;
}

/**
 * [DUMMY] Titik otentikasi shell. SSO sengaja BELUM dipasang di sini: login OIDC ke Keycloak
 * lokal adalah pekerjaan P3.4. Cara shell menyerahkan token ke remote juga belum diketahui
 * (PLAYBOOK Lampiran E #12) — jangan mengarang kontraknya di kode remote.
 */
@Injectable({ providedIn: 'root' })
export class ShellAuth {
  readonly pengguna = signal<PenggunaShell | null>(null);
}
