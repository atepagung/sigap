import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { IamPermissions } from '@danarakca/iam';
import { KonteksSayaService } from '../core/auth/konteks-saya.service';
import { SesiPengguna } from '../core/auth/sesi-pengguna';
import { TokenProvider } from '../core/auth/token-provider';

/**
 * Masuk (mode mandiri saja — di dalam shell, otentikasi milik shell). Login dev langsung ke
 * Keycloak dummy; lihat `core/auth/token-provider.ts` untuk batasannya.
 */
@Component({
  selector: 'app-masuk',
  imports: [FormsModule],
  styleUrl: './masuk.scss',
  template: `
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Masuk</h1>
        <p class="page-header__subtitle">Mode pengembangan — login langsung ke Keycloak dummy.</p>
      </div>
    </header>

    <form class="table-card" (submit)="kirimAsync($event)">
      <div class="table-card__header">
        <h2 class="table-card__title">NIP dan kata sandi akun uji</h2>
      </div>
      <div class="masuk__isi">
        <div class="form-field">
          <label class="form-field__label" for="nip">NIP</label>
          <input
            id="nip"
            type="text"
            name="nip"
            [(ngModel)]="nip"
            autocomplete="username"
            required
          />
          <p class="form-field__hint">
            Sepuluh akun uji: 900000000000000001 (Pegawai) sampai …010 (Impl. RKB).
          </p>
        </div>
        <div class="form-field" [class.form-field--invalid]="gagal()">
          <label class="form-field__label" for="kata-sandi">Kata sandi</label>
          <input
            id="kata-sandi"
            type="text"
            name="kataSandi"
            [(ngModel)]="kataSandi"
            autocomplete="current-password"
            required
          />
          @if (gagal()) {
            <p class="form-field__error">NIP atau kata sandi ditolak.</p>
          }
        </div>
        <button type="submit" class="button" [disabled]="memproses()">
          {{ memproses() ? 'Memeriksa…' : 'Masuk' }}
        </button>
      </div>
    </form>
  `,
})
export class Masuk {
  private readonly tokenProvider = inject(TokenProvider);
  private readonly konteksSaya = inject(KonteksSayaService);
  private readonly sesi = inject(SesiPengguna);
  private readonly permissions = inject(IamPermissions);
  private readonly router = inject(Router);

  protected nip = '';
  protected kataSandi = '';
  protected readonly memproses = signal(false);
  protected readonly gagal = signal(false);

  protected async kirimAsync(e: Event): Promise<void> {
    e.preventDefault();
    this.memproses.set(true);
    this.gagal.set(false);

    const berhasil = await this.tokenProvider.masukAsync(this.nip, this.kataSandi);
    if (!berhasil) {
      this.gagal.set(true);
      this.memproses.set(false);
      return;
    }

    const konteks = await this.konteksSaya.bacaAsync();
    this.sesi.atur(konteks);
    this.permissions.replace(konteks.permission);
    this.memproses.set(false);
    await this.router.navigate(['/']);
  }
}
