import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LayananGangguan } from './monitor.model';
import { MonitorService } from './monitor.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** "Lihat Detail" aspek Layanan — gangguan yang masih berjalan di lingkup (#34). */
@Component({
  selector: 'app-dashboard-layanan',
  imports: [PesanGalat, DatePipe, DecimalPipe, FormsModule, RouterLink],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Layanan Terganggu</h1>
      </div>
    </header>

    <div class="table-card">
      <div class="table-card__header">
        <h2 class="table-card__title">Filter</h2>
        <div class="table-card__toolbar">
          <select name="status" [(ngModel)]="status" (ngModelChange)="muatAsync()">
            <option value="">Semua</option>
            <option value="TERGANGGU">Terganggu</option>
            <option value="BERHENTI_TOTAL">Berhenti Total</option>
          </select>
        </div>
      </div>
      <table>
        <thead>
          <tr>
            <th>Layanan</th>
            <th>Unit</th>
            <th>Status</th>
            <th>Sejak</th>
            <th>Sisa RTO (jam)</th>
          </tr>
        </thead>
        <tbody>
          @for (g of daftar(); track g.layanan.id + g.unit.id) {
            <tr>
              <td>{{ g.layanan.nama }} ({{ g.layanan.rtoJam }} jam)</td>
              <td>
                <a [routerLink]="['/dashboard-unit', g.unit.id]">{{ g.unit.nama }}</a>
              </td>
              <td>
                <span
                  class="status-badge"
                  [class.status-badge--warning]="g.status === 'TERGANGGU'"
                  [class.status-badge--danger]="g.status === 'BERHENTI_TOTAL'"
                >
                  {{ g.status }}
                </span>
              </td>
              <td>{{ g.sejak | date: 'medium' }}</td>
              <td [class.status-badge--danger]="g.sisaRtoJam <= 0">
                {{ g.sisaRtoJam | number: '1.1-1' }}
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="5" class="table-card__empty">Tidak ada gangguan layanan berjalan.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class DashboardLayanan implements OnInit {
  private readonly monitor = inject(MonitorService);

  protected readonly daftar = signal<readonly LayananGangguan[]>([]);
  protected status = '';

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync();
  }

  protected async muatAsync(): Promise<void> {
    const halaman = await this.galat.jalankanAsync(() =>
      this.monitor.layananAsync(this.status || undefined),
    );
    if (halaman) {
      this.daftar.set(halaman.data);
    }
  }
}
