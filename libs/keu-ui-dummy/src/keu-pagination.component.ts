import { Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * [ASUMSI] Selector, nama kelas, nama @Input/@Output — SELURUHNYA tebakan kita. Katalog platform belum
 * diketahui punya paginasi (DUMMY_REGISTRY 2.8); dummy ini dibuat atas keputusan pemilik supaya halaman
 * berdaftar tidak diam-diam berhenti di halaman pertama.
 *
 * Kontrak yang kita butuhkan, apa pun bentuk komponen aslinya: menerima `halaman` (mulai 1), `ukuran`, dan
 * `total` persis seperti amplop koleksi API_CONTRACT 1.4, dan memberi tahu nomor halaman yang diminta.
 * Tanpa style sendiri — memakai class katalog global (.pagination, .button).
 */
@Component({
  selector: 'keu-pagination',
  standalone: true,
  template: `
    @if (total > 0) {
      <nav class="pagination" aria-label="Paginasi">
        <p class="pagination__info">{{ awal }}–{{ akhir }} dari {{ total }}</p>
        <div class="pagination__actions">
          <button
            type="button"
            class="button button--secondary"
            [disabled]="halaman <= 1"
            (click)="pindah(halaman - 1)"
          >
            Sebelumnya
          </button>
          <span class="pagination__posisi">Halaman {{ halaman }} dari {{ totalHalaman }}</span>
          <button
            type="button"
            class="button button--secondary"
            [disabled]="halaman >= totalHalaman"
            (click)="pindah(halaman + 1)"
          >
            Berikutnya
          </button>
        </div>
      </nav>
    }
  `,
})
export class KeuPaginationComponent {
  /** Nomor halaman sekarang, mulai 1. */
  @Input() halaman = 1;

  @Input() ukuran = 20;

  @Input() total = 0;

  /** Nomor halaman yang diminta pengguna; pemanggil yang memuat datanya. */
  @Output() readonly halamanBerubah = new EventEmitter<number>();

  get totalHalaman(): number {
    return this.ukuran > 0 ? Math.max(1, Math.ceil(this.total / this.ukuran)) : 1;
  }

  get awal(): number {
    return this.total === 0 ? 0 : (this.halaman - 1) * this.ukuran + 1;
  }

  get akhir(): number {
    return Math.min(this.halaman * this.ukuran, this.total);
  }

  pindah(tujuan: number): void {
    if (tujuan >= 1 && tujuan <= this.totalHalaman && tujuan !== this.halaman) {
      this.halamanBerubah.emit(tujuan);
    }
  }
}
