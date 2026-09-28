import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AsesmenRingkas } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { KeuPaginationComponent } from '@danarakca/keu-ui';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Daftar asesmen dalam lingkup pemanggil (#24). */
@Component({
  selector: 'app-daftar-asesmen',
  imports: [PesanGalat, KeuPaginationComponent, RouterLink, DatePipe],
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
      <keu-pagination
        [halaman]="halaman()"
        [ukuran]="ukuran()"
        [total]="total()"
        (halamanBerubah)="muatAsync($event)"
      />
    </div>
  `,
})
export class DaftarAsesmen implements OnInit {
  private readonly asesmen = inject(AsesmenService);

  protected readonly daftar = signal<readonly AsesmenRingkas[]>([]);
  protected readonly halaman = signal(1);
  protected readonly ukuran = signal(20);
  protected readonly total = signal(0);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync(1);
  }

  protected async muatAsync(nomor: number): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() =>
      this.asesmen.daftarAsync(undefined, undefined, nomor),
    );
    if (hasil) {
      this.daftar.set(hasil.data);
      this.halaman.set(hasil.halaman);
      this.ukuran.set(hasil.ukuran);
      this.total.set(hasil.total);
    }
  }
}
