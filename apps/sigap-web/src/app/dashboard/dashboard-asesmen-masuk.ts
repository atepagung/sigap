import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AsesmenMasuk } from './monitor.model';
import { MonitorService } from './monitor.service';
import { KeuPaginationComponent } from '@danarakca/keu-ui';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Notifikasi asesmen masuk, versi terkini tiap seri (#32, 2.6.1). */
@Component({
  selector: 'app-dashboard-asesmen-masuk',
  imports: [PesanGalat, KeuPaginationComponent, DatePipe, RouterLink],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Asesmen Masuk</h1>
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
            <th>Tanggap Darurat</th>
            <th>Dikirim</th>
          </tr>
        </thead>
        <tbody>
          @for (a of daftar(); track a.asesmenId) {
            <tr>
              <td>
                <a [routerLink]="['/dashboard-unit', a.unit.id]">{{ a.unit.nama }}</a>
              </td>
              <td>{{ a.jenisBencana }}</td>
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
              <td>{{ a.tanggapDarurat?.status ?? '—' }}</td>
              <td>{{ a.dikirimPada | date: 'medium' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="6" class="table-card__empty">Tidak ada asesmen masuk.</td>
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
export class DashboardAsesmenMasuk implements OnInit {
  private readonly monitor = inject(MonitorService);

  protected readonly daftar = signal<readonly AsesmenMasuk[]>([]);
  protected readonly halaman = signal(1);
  protected readonly ukuran = signal(20);
  protected readonly total = signal(0);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync(1);
  }

  protected async muatAsync(nomor: number): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() => this.monitor.asesmenMasukAsync(nomor));
    if (hasil) {
      this.daftar.set(hasil.data);
      this.halaman.set(hasil.halaman);
      this.ukuran.set(hasil.ukuran);
      this.total.set(hasil.total);
    }
  }
}
