import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-halaman-tidak-ditemukan',
  imports: [RouterLink],
  template: `
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Halaman tidak ditemukan</h1>
        <p class="page-header__subtitle">Alamat "{{ alamat }}" tidak ada di modul ini.</p>
      </div>
      <div class="page-header__actions">
        <a class="button button--secondary" routerLink="/">Kembali ke beranda</a>
      </div>
    </header>
  `,
})
export class HalamanTidakDitemukan {
  protected readonly alamat = inject(ActivatedRoute).snapshot.url.join('/');
}
