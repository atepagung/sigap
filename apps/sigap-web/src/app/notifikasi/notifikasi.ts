import { Component, OnInit, inject, signal } from '@angular/core';
import { Peringatan } from './notifikasi.model';
import { NotifikasiService } from './notifikasi.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Peringatan yang dihitung saat diminta (#43) — tanpa status "sudah dibaca" (skema tidak punya tabelnya). */
@Component({
  selector: 'app-notifikasi',
  imports: [PesanGalat],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Notifikasi</h1>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Tingkat</th>
            <th>Judul</th>
            <th>Pesan</th>
          </tr>
        </thead>
        <tbody>
          @for (p of daftar(); track p.kode + (p.terkait?.id ?? '')) {
            <tr>
              <td>
                <span
                  class="status-badge"
                  [class.status-badge--danger]="p.tingkat === 'GENTING'"
                  [class.status-badge--warning]="p.tingkat === 'PERINGATAN'"
                  [class.status-badge--info]="p.tingkat === 'INFORMASI'"
                >
                  {{ p.tingkat }}
                </span>
              </td>
              <td>{{ p.judul }}</td>
              <td>{{ p.pesan }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="3" class="table-card__empty">Tidak ada peringatan saat ini.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class NotifikasiPage implements OnInit {
  private readonly notifikasi = inject(NotifikasiService);

  protected readonly daftar = signal<readonly Peringatan[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() => this.notifikasi.peringatanAsync());
    if (hasil) {
      this.daftar.set(hasil.data);
    }
  }
}
