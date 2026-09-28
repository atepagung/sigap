import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { DetailBroadcast as DetailBroadcastDto } from './broadcast.model';
import { BroadcastService } from './broadcast.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Detail satu broadcast (#15) dan aksi mengakhirinya (#16, otorisasi `PEMICU_ATAU_MENCAKUP`). */
@Component({
  selector: 'app-detail-broadcast',
  imports: [PesanGalat, HasPermissionDirective, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    @if (detail(); as d) {
      <header class="page-header">
        <div>
          <h1 class="page-header__title">{{ d.jenisBencana }}</h1>
          <p class="page-header__subtitle">
            {{ d.lokasi }} — dipicu {{ d.dipicuPada | date: 'medium' }} oleh
            {{ d.pemicu.pengguna.nama }}
          </p>
        </div>
        <div class="page-header__actions">
          @if (d.status === 'AKTIF') {
            <button
              *hasPermission="'sigap:broadcast:close'"
              type="button"
              class="button button--danger"
              (click)="akhiriAsync()"
              [disabled]="memproses()"
            >
              Akhiri Broadcast
            </button>
          }
        </div>
      </header>
      <app-pesan-galat [pesan]="galat.pesanAksi()" />

      <div class="stats-row">
        <div class="stat-card">
          <p class="stat-card__label">Status</p>
          <p class="stat-card__value">{{ d.status === 'AKTIF' ? 'Aktif' : 'Selesai' }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Unit Disasar</p>
          <p class="stat-card__value">{{ d.sasaran.jumlahUnitDisasar }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Pegawai Disasar</p>
          <p class="stat-card__value">{{ d.sasaran.jumlahPegawaiDisasar }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Sudah Menjawab</p>
          <p class="stat-card__value">{{ d.sasaran.jumlahMenjawab }}</p>
        </div>
      </div>

      @if (d.diakhiri; as diakhiri) {
        <div class="table-card">
          <div class="table-card__header">
            <h2 class="table-card__title">Diakhiri</h2>
          </div>
          <p class="trigger-safety-check__isi">
            Oleh {{ diakhiri.oleh.nama }} pada {{ diakhiri.pada | date: 'medium' }}.
            @if (diakhiri.alasan) {
              Alasan: {{ diakhiri.alasan }}
            }
          </p>
        </div>
      }

      <div class="table-card">
        <div class="table-card__header">
          <h2 class="table-card__title">Unit yang Disasar</h2>
        </div>
        <table>
          <thead>
            <tr>
              <th>Unit</th>
              <th>Provinsi</th>
              <th>Kabupaten/Kota</th>
            </tr>
          </thead>
          <tbody>
            @for (u of d.sasaran.unitDisasar; track u.id) {
              <tr>
                <td>{{ u.nama }}</td>
                <td>{{ u.provinsi ?? '—' }}</td>
                <td>{{ u.kabupatenKota ?? '—' }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="3" class="table-card__empty">Tidak ada unit di dalam lingkup Anda.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styleUrl: './trigger-safety-check.scss',
})
export class DetailBroadcast implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly broadcast = inject(BroadcastService);

  protected readonly detail = signal<DetailBroadcastDto | null>(null);
  protected readonly memproses = signal(false);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.detail.set(await this.galat.jalankanAsync(() => this.broadcast.bacaAsync(id)));
    }
  }

  protected async akhiriAsync(): Promise<void> {
    const d = this.detail();
    if (!d) {
      return;
    }

    this.memproses.set(true);
    const hasil = await this.galat.jalankanAksiAsync(() => this.broadcast.selesaiAsync(d.id));
    this.memproses.set(false);
    if (hasil) {
      this.detail.set(hasil);
    }
  }
}
