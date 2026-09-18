import { Component } from '@angular/core';
import { REMOTE_IDENTITY } from '../../federation/generated/remote-identity';

@Component({
  selector: 'app-beranda',
  template: `
    <header class="page-header">
      <div>
        <h1 class="page-header__title">{{ judul }}</h1>
        <p class="page-header__subtitle">Sistem Manajemen Keberlangsungan Bisnis — Fase 1</p>
      </div>
    </header>
  `,
})
export class Beranda {
  protected readonly judul = REMOTE_IDENTITY.displayName;
}
