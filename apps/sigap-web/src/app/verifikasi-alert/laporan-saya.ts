import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Laporan } from './laporan.model';
import { LaporanService } from './laporan.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Riwayat laporan pemanggil sendiri (#9). */
@Component({
  selector: 'app-laporan-saya',
  imports: [PesanGalat, RouterLink, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Laporan Saya</h1>
      </div>
      <div class="page-header__actions">
        <a class="button" routerLink="/lapor-bencana">Laporkan Potensi Bencana</a>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Jenis Bencana</th>
            <th>Lokasi</th>
            <th>Status</th>
            <th>Dilaporkan</th>
          </tr>
        </thead>
        <tbody>
          @for (l of daftar(); track l.id) {
            <tr>
              <td>
                <a [routerLink]="['/detail-laporan', l.id]">{{ l.jenisBencana }}</a>
              </td>
              <td>{{ l.lokasi }}</td>
              <td>
                <span
                  class="status-badge"
                  [class.status-badge--warning]="l.status === 'MENUNGGU'"
                  [class.status-badge--success]="l.status === 'TERVERIFIKASI'"
                  [class.status-badge--danger]="l.status === 'DITOLAK'"
                >
                  {{ l.status }}
                </span>
              </td>
              <td>{{ l.dilaporkanPada | date: 'medium' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="4" class="table-card__empty">Belum pernah melapor.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class LaporanSaya implements OnInit {
  private readonly laporan = inject(LaporanService);

  protected readonly daftar = signal<readonly Laporan[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const halaman = await this.galat.jalankanAsync(() => this.laporan.sayaAsync());
    if (halaman) {
      this.daftar.set(halaman.data);
    }
  }
}
