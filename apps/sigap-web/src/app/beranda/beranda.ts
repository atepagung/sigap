import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { REMOTE_IDENTITY } from '../../federation/generated/remote-identity';
import { SesiPengguna } from '../core/auth/sesi-pengguna';
import { TokenProvider } from '../core/auth/token-provider';

/**
 * Beranda — sekaligus menu navigasi mode mandiri. Di dalam shell, sidebar sungguhan yang
 * menautkan ke tiap halaman (AGENTS.md bagian 6: "Sidebar hanya berisi satu entri per modul; menu
 * di dalam modul urusan remote" — DUMMY_REGISTRY 3.4 butir 53); halaman ini menyediakan tautan
 * yang sama untuk pengujian tanpa shell.
 */
@Component({
  selector: 'app-beranda',
  imports: [RouterLink, HasPermissionDirective],
  template: `
    <header class="page-header">
      <div>
        <h1 class="page-header__title">{{ judul }}</h1>
        <p class="page-header__subtitle">Sistem Manajemen Keberlangsungan Bisnis — Fase 1</p>
        @if (sesi.konteks(); as k) {
          <p class="page-header__subtitle">
            {{ k.pengguna.nama ?? k.pengguna.nip }} — {{ k.peran.join(', ') }} — lingkup
            {{ k.lingkup.label ?? k.lingkup.jenis }}
          </p>
        }
      </div>
      <div class="page-header__actions">
        <button type="button" class="button button--secondary" (click)="keluar()">Keluar</button>
      </div>
    </header>

    <div class="table-card beranda__kartu">
      <div class="table-card__header">
        <h2 class="table-card__title">Safety Check</h2>
      </div>
      <div class="beranda__isi">
        <a class="button" routerLink="/safety-check" *hasPermission="'sigap:safety-check:read'"
          >Safety Check Saya</a
        >
        <a
          class="button button--secondary"
          routerLink="/riwayat-safety-check"
          *hasPermission="'sigap:safety-check:read'"
          >Riwayat Saya</a
        >
        <a
          class="button button--secondary"
          routerLink="/rekap-safety-check"
          *hasPermission="'sigap:safety-check-rekap:read'"
          >Rekap Unit</a
        >
        <a
          class="button button--secondary"
          routerLink="/trigger-safety-check"
          *hasPermission="'sigap:broadcast:trigger'"
          >Trigger</a
        >
        <a
          class="button button--secondary"
          routerLink="/daftar-broadcast"
          *hasPermission="'sigap:broadcast:read'"
          >Riwayat Broadcast</a
        >
      </div>
    </div>

    <div class="table-card beranda__kartu">
      <div class="table-card__header">
        <h2 class="table-card__title">Verifikasi Alert Bencana</h2>
      </div>
      <div class="beranda__isi">
        <a class="button" routerLink="/lapor-bencana" *hasPermission="'sigap:laporan:create'"
          >Laporkan Potensi Bencana</a
        >
        <a
          class="button button--secondary"
          routerLink="/laporan-saya"
          *hasPermission="'sigap:laporan:read'"
          >Laporan Saya</a
        >
        <a
          class="button button--secondary"
          routerLink="/verifikasi-alert"
          *hasPermission="'sigap:laporan:verify'"
          >Antrean Verifikasi</a
        >
      </div>
    </div>

    <div class="table-card beranda__kartu">
      <div class="table-card__header">
        <h2 class="table-card__title">Asesmen Kondisi Bencana</h2>
      </div>
      <div class="beranda__isi">
        <a class="button" routerLink="/asesmen-bencana" *hasPermission="'sigap:asesmen:create'"
          >Kirim/Perbarui Asesmen</a
        >
        <a
          class="button button--secondary"
          routerLink="/daftar-asesmen"
          *hasPermission="'sigap:asesmen:read'"
          >Daftar Asesmen</a
        >
        <a
          class="button button--secondary"
          routerLink="/layanan-kritis"
          *hasPermission="'sigap:layanan-kritis:read'"
          >Layanan Kritis</a
        >
      </div>
    </div>

    <div class="table-card beranda__kartu">
      <div class="table-card__header">
        <h2 class="table-card__title">Dashboard Monitor SC & Sumber Daya</h2>
      </div>
      <div class="beranda__isi">
        <a class="button" routerLink="/dashboard" *hasPermission="'sigap:monitor:read'"
          >Buka Dashboard</a
        >
      </div>
    </div>

    <div class="table-card beranda__kartu">
      <div class="table-card__header">
        <h2 class="table-card__title">Lainnya</h2>
      </div>
      <div class="beranda__isi">
        <a
          class="button button--secondary"
          routerLink="/notifikasi"
          *hasPermission="'sigap:notifikasi:read'"
          >Notifikasi</a
        >
      </div>
    </div>
  `,
  styleUrl: './beranda.scss',
})
export class Beranda {
  protected readonly judul = REMOTE_IDENTITY.displayName;
  protected readonly sesi = inject(SesiPengguna);

  private readonly tokenProvider = inject(TokenProvider);
  private readonly router = inject(Router);

  protected keluar(): void {
    this.tokenProvider.keluar();
    this.sesi.bersihkan();
    void this.router.navigate(['/masuk']);
  }
}
