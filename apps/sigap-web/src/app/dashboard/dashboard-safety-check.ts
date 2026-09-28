import { PercentPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SafetyCheckKelompok } from './monitor.model';
import { MonitorService } from './monitor.service';
import { KeuPaginationComponent } from '@danarakca/keu-ui';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Tabel agregat safety check per unit/provinsi/Eselon I (#31). */
@Component({
  selector: 'app-dashboard-safety-check',
  imports: [PesanGalat, KeuPaginationComponent, FormsModule, PercentPipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Rekap Safety Check per Kelompok</h1>
      </div>
    </header>

    <div class="table-card">
      <div class="table-card__header">
        <h2 class="table-card__title">Kelompokkan berdasarkan</h2>
        <div class="table-card__toolbar">
          <select name="kelompok" [(ngModel)]="kelompok" (ngModelChange)="muatAsync(1)">
            <option value="unit">Unit</option>
            <option value="provinsi">Provinsi</option>
            <option value="eselon-1">Eselon I</option>
            <option value="provinsi-eselon-1">Provinsi × Eselon I</option>
          </select>
        </div>
      </div>
      <table>
        <thead>
          <tr>
            <th>Kelompok</th>
            <th>Total Pegawai</th>
            <th>Aman</th>
            <th>Butuh Bantuan</th>
            <th>Belum Merespons</th>
            <th>Tingkat Respons</th>
          </tr>
        </thead>
        <tbody>
          @for (b of daftar(); track b.kelompok.kode) {
            <tr>
              <td>{{ b.kelompok.label }}</td>
              <td>{{ b.totalPegawai }}</td>
              <td>{{ b.aman }}</td>
              <td>{{ b.butuhBantuan }}</td>
              <td>{{ b.belumMerespons }}</td>
              <td>{{ b.tingkatRespons | percent: '1.0-0' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="6" class="table-card__empty">Tidak ada data.</td>
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
export class DashboardSafetyCheck implements OnInit {
  private readonly monitor = inject(MonitorService);

  protected readonly daftar = signal<readonly SafetyCheckKelompok[]>([]);
  protected readonly halaman = signal(1);
  protected readonly ukuran = signal(50);
  protected readonly total = signal(0);
  protected kelompok = 'unit';

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync(1);
  }

  protected async muatAsync(nomor: number): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() =>
      this.monitor.safetyCheckAsync(this.kelompok, nomor),
    );
    if (hasil) {
      this.daftar.set(hasil.data);
      this.halaman.set(hasil.halaman);
      this.ukuran.set(hasil.ukuran);
      this.total.set(hasil.total);
    }
  }
}
