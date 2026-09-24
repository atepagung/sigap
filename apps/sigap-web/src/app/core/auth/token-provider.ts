import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { KEYCLOAK_DEV_CLIENT_ID, KEYCLOAK_ISSUER_URL } from '../config/api-config';

interface ResponsTokenKeycloak {
  readonly access_token: string;
}

/**
 * [ASUMSI] Seluruh berkas ini seam pengembangan, BUKAN mekanisme platform. DUMMY_REGISTRY
 * bagian 3.3 butir 51: cara shell menyerahkan token ke remote belum diketahui sama sekali.
 *
 * Selama itu belum ada, halaman `masuk` (mode mandiri) login langsung ke Keycloak dummy lewat
 * client `sigap-uji-lokal` (password grant, khusus lokal — infra/keycloak/README.md). Token
 * disimpan di memori saja (bukan localStorage/sessionStorage): hilang saat reload, tapi tidak
 * ada kredensial tersimpan di disk. Saat mekanisme shell sungguhan diketahui, satu-satunya yang
 * berubah adalah kelas ini dan `auth.interceptor.ts` — komponen halaman tidak menyentuh token.
 */
@Injectable({ providedIn: 'root' })
export class TokenProvider {
  private readonly http = inject(HttpClient);
  private readonly issuer = inject(KEYCLOAK_ISSUER_URL);
  private readonly clientId = inject(KEYCLOAK_DEV_CLIENT_ID);

  private readonly _token = signal<string | null>(null);

  readonly token = this._token.asReadonly();

  get sudahMasuk(): boolean {
    return this._token() !== null;
  }

  /** @returns token akses bila berhasil; `null` bila NIP/kata sandi ditolak Keycloak. */
  async masukAsync(nip: string, kataSandi: string): Promise<boolean> {
    const body = new URLSearchParams({
      grant_type: 'password',
      client_id: this.clientId,
      username: nip,
      password: kataSandi,
      scope: 'openid',
    });

    try {
      const respons = await firstValueFrom(
        this.http.post<ResponsTokenKeycloak>(
          `${this.issuer}/protocol/openid-connect/token`,
          body.toString(),
          {
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
          },
        ),
      );
      this._token.set(respons.access_token);
      return true;
    } catch {
      this._token.set(null);
      return false;
    }
  }

  keluar(): void {
    this._token.set(null);
  }
}
