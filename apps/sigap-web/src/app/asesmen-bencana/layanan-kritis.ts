import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HasPermissionDirective } from '@danarakca/iam';
import { LayananKritis } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Layanan kritis unit (#19, #20) — dasar aspek Layanan, didaftarkan manual (koreksi stakeholder 7). */
@Component({
  selector: 'app-layanan-kritis',
  imports: [PesanGalat, FormsModule, HasPermissionDirective],
  styleUrl: '../verifikasi-alert/lapor-bencana.scss',
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Layanan Kritis Unit</h1>
      </div>
    </header>

    <div class="table-card">
      <table>
        <thead>
          <tr>
            <th>Nama Layanan</th>
            <th>RTO</th>
            <th>Sumber</th>
          </tr>
        </thead>
        <tbody>
          @for (l of daftar(); track l.id) {
            <tr>
              <td>{{ l.nama }}</td>
              <td>{{ l.rtoLabel }}</td>
              <td>{{ l.sumber === 'ADB' ? 'ADB' : 'Manual' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="3" class="table-card__empty">Belum ada layanan kritis terdaftar.</td>
            </tr>
          }
        </tbody>
      </table>
    </div>

    <div class="table-card" *hasPermission="'sigap:layanan-kritis:create'">
      <div class="table-card__header">
        <h2 class="table-card__title">Daftarkan Layanan Baru</h2>
      </div>
      <div class="lapor-bencana__isi">
        <div class="form-field">
          <label class="form-field__label" for="nama"
            >Nama Layanan<span class="form-field__required">*</span></label
          >
          <input id="nama" type="text" name="nama" [(ngModel)]="nama" />
        </div>
        <div class="form-field">
          <label class="form-field__label" for="rto"
            >RTO (jam)<span class="form-field__required">*</span></label
          >
          <input id="rto" type="number" name="rto" min="1" [(ngModel)]="rtoJam" />
        </div>
        <app-pesan-galat [pesan]="galat.pesanAksi()" />
        <button
          type="button"
          class="button"
          (click)="tambahAsync()"
          [disabled]="!nama || !rtoJam || memproses()"
        >
          Tambah
        </button>
      </div>
    </div>
  `,
})
export class LayananKritisPage implements OnInit {
  private readonly asesmen = inject(AsesmenService);

  protected readonly daftar = signal<readonly LayananKritis[]>([]);
  protected readonly memproses = signal(false);
  protected nama = '';
  protected rtoJam: number | null = null;

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync();
  }

  private async muatAsync(): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() => this.asesmen.layananKritisAsync());
    if (hasil) {
      this.daftar.set(hasil.data);
    }
  }

  protected async tambahAsync(): Promise<void> {
    if (!this.nama || !this.rtoJam) {
      return;
    }

    const { nama, rtoJam } = this;
    this.memproses.set(true);
    const baru = await this.galat.jalankanAksiAsync(() =>
      this.asesmen.tambahLayananKritisAsync(nama, rtoJam),
    );
    if (baru) {
      this.nama = '';
      this.rtoJam = null;
      await this.muatAsync();
    }
    this.memproses.set(false);
  }
}
