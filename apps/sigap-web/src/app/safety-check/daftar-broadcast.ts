import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RiwayatBroadcast } from './broadcast.model';
import { BroadcastService } from './broadcast.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Riwayat broadcast (#14) — peran mana pun yang menyentuh lingkup pemanggil, atau pemicu sendiri. */
@Component({
  selector: 'app-daftar-broadcast',
  imports: [PesanGalat, RouterLink, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Riwayat Broadcast Safety Check</h1>
      </div>
      <div class="page-header__actions">
        <a class="button" routerLink="/trigger-safety-check">Trigger Baru</a>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Jenis Bencana</th>
            <th>Lokasi</th>
            <th>Sumber</th>
            <th>Status</th>
            <th>Unit Disasar</th>
            <th>Menjawab</th>
            <th>Dipicu</th>
          </tr>
        </thead>
        <tbody>
          @for (b of daftar(); track b.id) {
            <tr>
              <td>
                <a [routerLink]="['/detail-broadcast', b.id]">{{ b.jenisBencana }}</a>
              </td>
              <td>{{ b.lokasi }}</td>
              <td>{{ b.sumber === 'OTOMATIS_BMKG' ? 'Otomatis BMKG' : 'Manual' }}</td>
              <td>
                <span
                  class="status-badge"
                  [class.status-badge--danger]="b.status === 'AKTIF'"
                  [class.status-badge--neutral]="b.status === 'SELESAI'"
                >
                  {{ b.status === 'AKTIF' ? 'Aktif' : 'Selesai' }}
                </span>
              </td>
              <td>{{ b.jumlahUnitDisasar }}</td>
              <td>{{ b.jumlahMenjawab }} / {{ b.jumlahPegawaiDisasar }}</td>
              <td>{{ b.dipicuPada | date: 'medium' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="7" class="table-card__empty">Belum ada broadcast dalam lingkup Anda.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class DaftarBroadcast implements OnInit {
  private readonly broadcast = inject(BroadcastService);

  protected readonly daftar = signal<readonly RiwayatBroadcast[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const halaman = await this.galat.jalankanAsync(() =>
      this.broadcast.daftarAsync(undefined, undefined, undefined, 1, 50),
    );
    if (halaman) {
      this.daftar.set(halaman.data);
    }
  }
}
