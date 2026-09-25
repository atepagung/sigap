import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AsesmenRingkas } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Daftar asesmen dalam lingkup pemanggil (#24). */
@Component({
  selector: 'app-daftar-asesmen',
  imports: [PesanGalat, RouterLink, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Daftar Asesmen</h1>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Unit</th>
            <th>Jenis Bencana</th>
            <th>Versi</th>
            <th>Status Persetujuan</th>
            <th>Dikirim</th>
          </tr>
        </thead>
        <tbody>
          @for (a of daftar(); track a.id) {
            <tr>
              <td>{{ a.unit.nama }}</td>
              <td>
                <a [routerLink]="['/detail-asesmen', a.id]">{{ a.jenisBencana }}</a>
              </td>
              <td>#{{ a.urutan }}</td>
              <td>
                <span
                  class="status-badge"
                  [class.status-badge--warning]="a.statusPersetujuan === 'MENUNGGU_PIMPINAN'"
                  [class.status-badge--success]="a.statusPersetujuan === 'DISETUJUI'"
                >
                  {{ a.statusPersetujuan }}
                </span>
              </td>
              <td>{{ a.dikirimPada | date: 'medium' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="5" class="table-card__empty">Tidak ada asesmen.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class DaftarAsesmen implements OnInit {
  private readonly asesmen = inject(AsesmenService);

  protected readonly daftar = signal<readonly AsesmenRingkas[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const halaman = await this.galat.jalankanAsync(() => this.asesmen.daftarAsync());
    if (halaman) {
      this.daftar.set(halaman.data);
    }
  }
}
