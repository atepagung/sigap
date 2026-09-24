import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Laporan } from './laporan.model';
import { LaporanService } from './laporan.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Antrean verifikasi (#17) — Tim Satgas, urut MENUNGGU lebih dulu. */
@Component({
  selector: 'app-daftar-laporan',
  imports: [PesanGalat, RouterLink, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Verifikasi Alert Bencana</h1>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Pelapor</th>
            <th>Jenis Bencana</th>
            <th>Lokasi</th>
            <th>Status</th>
            <th>Dilaporkan</th>
          </tr>
        </thead>
        <tbody>
          @for (l of daftar(); track l.id) {
            <tr>
              <td>{{ l.pelapor.nama }}</td>
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
              <td colspan="5" class="table-card__empty">Tidak ada laporan.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class DaftarLaporan implements OnInit {
  private readonly laporan = inject(LaporanService);

  protected readonly daftar = signal<readonly Laporan[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const halaman = await this.galat.jalankanAsync(() => this.laporan.daftarAsync());
    if (halaman) {
      this.daftar.set(halaman.data);
    }
  }
}
