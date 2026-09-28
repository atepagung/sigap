import { Component, input } from '@angular/core';

/**
 * Pembungkus tipis kelas katalog `.table-card__empty` untuk galat pemuatan halaman. Katalog
 * design system belum punya komponen pesan galat (DUMMY_REGISTRY 2.8); ganti isi berkas ini
 * saat komponen aslinya ada.
 */
@Component({
  selector: 'app-pesan-galat',
  template: `
    @if (pesan(); as p) {
      <div class="table-card">
        <p class="table-card__empty" role="alert">{{ p }}</p>
      </div>
    }
  `,
})
export class PesanGalat {
  readonly pesan = input<string | null>(null);
}
