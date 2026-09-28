import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { bukaLampiranAsync } from '../shared/lampiran/buka-lampiran';
import { Laporan } from './laporan.model';
import { LaporanService } from './laporan.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Detail laporan (#10, #11) dan aksi verifikasi (#18) — koreksi stakeholder 3: wajib approve/reject. */
@Component({
  selector: 'app-detail-laporan',
  imports: [PesanGalat, FormsModule, HasPermissionDirective, DatePipe],
  styleUrl: './lapor-bencana.scss',
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    @if (laporan(); as l) {
      <header class="page-header">
        <div>
          <h1 class="page-header__title">{{ l.jenisBencana }} — {{ l.lokasi }}</h1>
          <p class="page-header__subtitle">
            Dilaporkan {{ l.pelapor.nama }}, {{ l.dilaporkanPada | date: 'medium' }}
          </p>
        </div>
      </header>

      <div class="stats-row">
        <div class="stat-card">
          <p class="stat-card__label">Level</p>
          <p class="stat-card__value">{{ l.level }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Status</p>
          <p class="stat-card__value">{{ l.status }}</p>
        </div>
      </div>

      <div class="table-card">
        <div class="table-card__header">
          <h2 class="table-card__title">Deskripsi</h2>
        </div>
        <p class="lapor-bencana__isi">{{ l.deskripsi ?? 'Tidak ada deskripsi.' }}</p>
      </div>

      @if (l.lampiran.length > 0) {
        <div class="table-card">
          <div class="table-card__header">
            <h2 class="table-card__title">Lampiran</h2>
          </div>
          <ul class="lapor-bencana__isi">
            @for (lm of l.lampiran; track lm.id) {
              <li>
                <button type="button" class="button button--ghost" (click)="bukaAsync(lm.url)">
                  {{ lm.tipe }}
                </button>
              </li>
            }
          </ul>
        </div>
      }

      @if (l.verifikasi; as v) {
        <div class="table-card">
          <div class="table-card__header">
            <h2 class="table-card__title">Hasil Verifikasi</h2>
          </div>
          <p class="lapor-bencana__isi">
            {{ v.keputusan }} oleh {{ v.oleh.nama }}, {{ v.pada | date: 'medium' }}.
            @if (v.alasan) {
              Alasan: {{ v.alasan }}
            }
          </p>
        </div>
      } @else {
        <div class="table-card" *hasPermission="'sigap:laporan:verify'">
          <div class="table-card__header">
            <h2 class="table-card__title">Verifikasi</h2>
          </div>
          <div class="lapor-bencana__isi">
            <div class="form-field">
              <label class="form-field__label" for="alasan">Alasan (wajib bila menolak)</label>
              <textarea id="alasan" name="alasan" [(ngModel)]="alasan"></textarea>
            </div>
            <app-pesan-galat [pesan]="galat.pesanAksi()" />
            <button
              type="button"
              class="button button--success"
              (click)="verifikasiAsync('VALID')"
              [disabled]="memproses()"
            >
              Valid
            </button>
            <button
              type="button"
              class="button button--danger"
              (click)="verifikasiAsync('TOLAK')"
              [disabled]="memproses()"
            >
              Tolak
            </button>
          </div>
        </div>
      }
    }
  `,
})
export class DetailLaporan implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly laporanService = inject(LaporanService);
  private readonly http = inject(HttpClient);

  protected readonly laporan = signal<Laporan | null>(null);
  protected readonly memproses = signal(false);
  protected alasan = '';

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.laporan.set(await this.galat.jalankanAsync(() => this.laporanService.bacaAsync(id)));
    }
  }

  protected async bukaAsync(url: string): Promise<void> {
    await bukaLampiranAsync(this.http, url);
  }

  protected async verifikasiAsync(keputusan: 'VALID' | 'TOLAK'): Promise<void> {
    const l = this.laporan();
    if (!l) {
      return;
    }

    this.memproses.set(true);
    const hasil = await this.galat.jalankanAksiAsync(() =>
      this.laporanService.verifikasiAsync(l.id, keputusan, this.alasan || undefined),
    );
    this.memproses.set(false);
    if (hasil) {
      this.laporan.set(hasil);
    }
  }
}
