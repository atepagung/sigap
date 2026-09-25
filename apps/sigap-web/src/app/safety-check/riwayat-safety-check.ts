import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RiwayatSaya } from './safety-check.model';
import { SafetyCheckService } from './safety-check.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Riwayat safety check pemanggil sendiri (#3). */
@Component({
  selector: 'app-riwayat-safety-check',
  imports: [PesanGalat, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Riwayat Safety Check Saya</h1>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Jenis Bencana</th>
            <th>Lokasi</th>
            <th>Status</th>
            <th>Dijawab</th>
            <th>Dicatatkan Satgas</th>
          </tr>
        </thead>
        <tbody>
          @for (r of riwayat(); track r.broadcast.id + r.dijawabPada) {
            <tr>
              <td>{{ r.broadcast.jenisBencana }}</td>
              <td>{{ r.broadcast.lokasi }}</td>
              <td>
                <span
                  class="status-badge"
                  [class.status-badge--success]="r.status === 'AMAN'"
                  [class.status-badge--danger]="r.status === 'BUTUH_BANTUAN'"
                >
                  {{ r.status === 'AMAN' ? 'Saya Aman' : 'Butuh Bantuan' }}
                </span>
              </td>
              <td>{{ r.dijawabPada | date: 'medium' }}</td>
              <td>{{ r.dicatatkanSatgas ? 'Ya' : 'Tidak' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="5" class="table-card__empty">Belum ada riwayat.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class RiwayatSafetyCheck implements OnInit {
  private readonly safetyCheck = inject(SafetyCheckService);

  protected readonly riwayat = signal<readonly RiwayatSaya[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const halaman = await this.galat.jalankanAsync(() => this.safetyCheck.riwayatSayaAsync());
    if (halaman) {
      this.riwayat.set(halaman.data);
    }
  }
}
