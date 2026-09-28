import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { HasPermissionDirective } from '@danarakca/iam';
import { KeuModalComponent } from '@danarakca/keu-ui';
import { Aktif } from './safety-check.model';
import { SafetyCheckService } from './safety-check.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * Safety Check / SOS pribadi (#1, #2) — koreksi stakeholder 1: hanya dua pilihan "Saya Aman" /
 * "Butuh Bantuan", seragam tanpa field tambahan (koreksi 2).
 *
 * Broadcast paling lama yang belum dijawab dipaksa muncul lewat modal `dismissible=false` — tidak
 * bisa ditutup tanpa menjawab, sesuai maksud fitur (memastikan pegawai benar-benar mengonfirmasi).
 */
@Component({
  selector: 'app-safety-check-saya',
  imports: [PesanGalat, HasPermissionDirective, KeuModalComponent, DatePipe],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Safety Check Saya</h1>
        <p class="page-header__subtitle">
          Konfirmasi keselamatan Anda saat ada broadcast berjalan.
        </p>
      </div>
    </header>

    @if (memuat()) {
      <p>Memuat…</p>
    } @else if (aktif().length === 0) {
      <div class="table-card">
        <p class="table-card__empty">
          Tidak ada broadcast safety check yang menyasar unit Anda saat ini.
        </p>
      </div>
    } @else {
      <div class="table-card">
        <div class="table-card__header">
          <h2 class="table-card__title">Broadcast berjalan</h2>
        </div>
        <table>
          <thead>
            <tr>
              <th>Jenis Bencana</th>
              <th>Lokasi</th>
              <th>Dipicu</th>
              <th>Status Saya</th>
              <th *hasPermission="'sigap:safety-check:respond'">Aksi</th>
            </tr>
          </thead>
          <tbody>
            @for (a of aktif(); track a.broadcast.id) {
              <tr>
                <td>{{ a.broadcast.jenisBencana }}</td>
                <td>{{ a.broadcast.lokasi }}</td>
                <td>{{ a.broadcast.dipicuPada | date: 'medium' }}</td>
                <td>
                  @if (a.responsSaya) {
                    <span
                      class="status-badge"
                      [class.status-badge--success]="a.responsSaya.status === 'AMAN'"
                      [class.status-badge--danger]="a.responsSaya.status === 'BUTUH_BANTUAN'"
                    >
                      {{ a.responsSaya.status === 'AMAN' ? 'Saya Aman' : 'Butuh Bantuan' }}
                    </span>
                  } @else {
                    <span class="status-badge status-badge--warning">Belum Merespons</span>
                  }
                </td>
                <td *hasPermission="'sigap:safety-check:respond'">
                  <button
                    type="button"
                    class="button button--success"
                    (click)="jawabAsync(a, 'AMAN')"
                    [disabled]="mengirim()"
                  >
                    Saya Aman
                  </button>
                  <button
                    type="button"
                    class="button button--danger"
                    (click)="jawabAsync(a, 'BUTUH_BANTUAN')"
                    [disabled]="mengirim()"
                  >
                    Butuh Bantuan
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <app-pesan-galat [pesan]="galat.pesanAksi()" />
    }

    @if (wajibDijawab(); as wajib) {
      <keu-modal [open]="true" [dismissible]="false" title="Konfirmasi Keselamatan Anda">
        <p>
          Broadcast safety check untuk <strong>{{ wajib.broadcast.jenisBencana }}</strong> di
          {{ wajib.broadcast.lokasi }} sedang berjalan. {{ wajib.pesan }}
        </p>
        <app-pesan-galat [pesan]="galat.pesanAksi()" />
        <div keuModalFooter *hasPermission="'sigap:safety-check:respond'">
          <button
            type="button"
            class="button button--success"
            (click)="jawabAsync(wajib, 'AMAN')"
            [disabled]="mengirim()"
          >
            Saya Aman
          </button>
          <button
            type="button"
            class="button button--danger"
            (click)="jawabAsync(wajib, 'BUTUH_BANTUAN')"
            [disabled]="mengirim()"
          >
            Butuh Bantuan
          </button>
        </div>
      </keu-modal>
    }
  `,
})
export class SafetyCheckSaya implements OnInit {
  private readonly safetyCheck = inject(SafetyCheckService);

  protected readonly memuat = signal(true);
  protected readonly mengirim = signal(false);
  protected readonly aktif = signal<readonly Aktif[]>([]);

  /** Paling lama dipicu di antara yang belum dijawab — urutan API sudah dari yang paling lama. */
  protected readonly wajibDijawab = signal<Aktif | null>(null);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync();
  }

  private async muatAsync(): Promise<void> {
    this.memuat.set(true);
    const daftar = await this.galat.jalankanAsync(() => this.safetyCheck.aktifAsync());
    if (daftar) {
      this.aktif.set(daftar.data);
      this.wajibDijawab.set(daftar.data.find((a) => a.responsSaya === null) ?? null);
    }
    this.memuat.set(false);
  }

  protected async jawabAsync(a: Aktif, status: 'AMAN' | 'BUTUH_BANTUAN'): Promise<void> {
    this.mengirim.set(true);
    // Hasil `true` (bukan nilai kembali PUT, yang bisa kosong) menandai berhasil; `null` = gagal.
    const berhasil = await this.galat.jalankanAksiAsync(async () => {
      await this.safetyCheck.jawabSayaAsync(a.broadcast.id, status);
      return true;
    });
    if (berhasil) {
      await this.muatAsync();
    }
    this.mengirim.set(false);
  }
}
