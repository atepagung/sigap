import { Component, Input } from '@angular/core';

/** Satu batang: `label` teks apa adanya, `nilai` cacah (>= 0). */
export interface KeuBarChartItem {
  readonly label: string;
  readonly nilai: number;
}

/**
 * [ASUMSI] Selector, nama kelas, nama @Input — SELURUHNYA tebakan kita. Katalog platform belum diketahui
 * punya grafik (DUMMY_REGISTRY 2.8); dummy ini dibuat atas keputusan pemilik supaya histogram dashboard
 * tampil sebagai diagram, bukan tabel.
 *
 * Sengaja hanya diagram batang mendatar dari HTML dan CSS (tanpa pustaka grafik, tanpa `<canvas>`): nilai
 * tiap batang tetap tertulis sebagai teks, jadi terbaca pembaca layar dan tidak ada angka yang hanya
 * tersirat dari panjang batang. Tanpa style sendiri — memakai class katalog global (.bar-chart).
 */
@Component({
  selector: 'keu-bar-chart',
  standalone: true,
  template: `
    <figure class="bar-chart">
      @if (judul) {
        <figcaption class="bar-chart__judul">{{ judul }}</figcaption>
      }
      <ul class="bar-chart__daftar">
        @for (b of data; track b.label) {
          <li class="bar-chart__baris">
            <span class="bar-chart__label">{{ b.label }}</span>
            <span class="bar-chart__jalur" aria-hidden="true">
              <span class="bar-chart__batang" [style.width.%]="persen(b.nilai)"></span>
            </span>
            <span class="bar-chart__nilai">{{ b.nilai }}</span>
          </li>
        } @empty {
          <li class="bar-chart__kosong">Belum ada data.</li>
        }
      </ul>
    </figure>
  `,
})
export class KeuBarChartComponent {
  @Input() data: readonly KeuBarChartItem[] = [];

  @Input() judul = '';

  /** Lebar batang terhadap nilai terbesar; semua nol atau negatif menjadi 0, bukan NaN. */
  persen(nilai: number): number {
    const terbesar = Math.max(0, ...this.data.map((b) => b.nilai));
    return terbesar > 0 && nilai > 0 ? (nilai / terbesar) * 100 : 0;
  }
}
