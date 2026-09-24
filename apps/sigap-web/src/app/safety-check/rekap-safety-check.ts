import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HasPermissionDirective } from '@danarakca/iam';
import { KeuModalComponent, KeuTabComponent, KeuTabsComponent } from '@danarakca/keu-ui';
import { RekapBaris } from './safety-check.model';
import { SafetyCheckService } from './safety-check.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * Rekap Safety Check per unit (#4, #5). Koreksi stakeholder 11: tampil sebagai TAB per kondisi
 * (Aman / Butuh Bantuan / Belum Merespons), bukan list yang di-scroll.
 *
 * [ASUMSI] Katalog belum punya komponen paginasi (DUMMY_REGISTRY 2.8), jadi rekap diambil dalam
 * satu halaman berukuran besar (200) alih-alih membangun pager sendiri. Unit dengan pegawai lebih
 * banyak dari itu perlu paginasi katalog sungguhan — dilaporkan, bukan dibuat sendiri di sini.
 */
@Component({
  selector: 'app-rekap-safety-check',
  imports: [
    PesanGalat,
    FormsModule,
    HasPermissionDirective,
    KeuTabsComponent,
    KeuTabComponent,
    KeuModalComponent,
    DatePipe,
  ],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Rekap Safety Check</h1>
        @if (unit(); as u) {
          <p class="page-header__subtitle">{{ u.nama }} — {{ broadcastJenis() }}</p>
        }
      </div>
    </header>

    @if (memuat()) {
      <p>Memuat…</p>
    } @else if (!adaBroadcast()) {
      <div class="table-card">
        <p class="table-card__empty">Tidak ada broadcast aktif yang memegang unit Anda.</p>
      </div>
    } @else {
      <keu-tabs>
        <keu-tab label="Butuh Bantuan" [count]="butuhBantuan().length">
          <div class="table-card">
            <table>
              <thead>
                <tr>
                  <th>Pegawai</th>
                  <th>Dijawab</th>
                  <th>Dicatatkan Oleh</th>
                  <th>Keterangan</th>
                  <th *hasPermission="'sigap:safety-check:record'">Aksi</th>
                </tr>
              </thead>
              <tbody>
                @for (b of butuhBantuan(); track b.pegawai.id) {
                  <tr>
                    <td>{{ b.pegawai.nama }}</td>
                    <td>{{ b.dijawabPada ? (b.dijawabPada | date: 'medium') : '—' }}</td>
                    <td>{{ b.dicatatOleh?.nama ?? '—' }}</td>
                    <td>{{ b.keterangan ?? '—' }}</td>
                    <td *hasPermission="'sigap:safety-check:record'">
                      <button type="button" class="button button--secondary" (click)="bukaCatat(b)">
                        Catatkan
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="table-card__empty">Tidak ada baris.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </keu-tab>
        <keu-tab label="Belum Merespons" [count]="belum().length">
          <div class="table-card">
            <table>
              <thead>
                <tr>
                  <th>Pegawai</th>
                  <th>Dijawab</th>
                  <th>Dicatatkan Oleh</th>
                  <th>Keterangan</th>
                  <th *hasPermission="'sigap:safety-check:record'">Aksi</th>
                </tr>
              </thead>
              <tbody>
                @for (b of belum(); track b.pegawai.id) {
                  <tr>
                    <td>{{ b.pegawai.nama }}</td>
                    <td>{{ b.dijawabPada ? (b.dijawabPada | date: 'medium') : '—' }}</td>
                    <td>{{ b.dicatatOleh?.nama ?? '—' }}</td>
                    <td>{{ b.keterangan ?? '—' }}</td>
                    <td *hasPermission="'sigap:safety-check:record'">
                      <button type="button" class="button button--secondary" (click)="bukaCatat(b)">
                        Catatkan
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="table-card__empty">Tidak ada baris.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </keu-tab>
        <keu-tab label="Aman" [count]="aman().length">
          <div class="table-card">
            <table>
              <thead>
                <tr>
                  <th>Pegawai</th>
                  <th>Dijawab</th>
                  <th>Dicatatkan Oleh</th>
                  <th>Keterangan</th>
                  <th *hasPermission="'sigap:safety-check:record'">Aksi</th>
                </tr>
              </thead>
              <tbody>
                @for (b of aman(); track b.pegawai.id) {
                  <tr>
                    <td>{{ b.pegawai.nama }}</td>
                    <td>{{ b.dijawabPada ? (b.dijawabPada | date: 'medium') : '—' }}</td>
                    <td>{{ b.dicatatOleh?.nama ?? '—' }}</td>
                    <td>{{ b.keterangan ?? '—' }}</td>
                    <td *hasPermission="'sigap:safety-check:record'">
                      <button type="button" class="button button--secondary" (click)="bukaCatat(b)">
                        Catatkan
                      </button>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5" class="table-card__empty">Tidak ada baris.</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </keu-tab>
      </keu-tabs>
    }

    @if (dicatat(); as target) {
      <keu-modal [open]="true" title="Catatkan Keadaan Pegawai" (closed)="dicatat.set(null)">
        <p>{{ target.pegawai.nama }}</p>
        <div class="form-field">
          <label class="form-field__label" for="status-catat">Status</label>
          <select id="status-catat" name="status" [(ngModel)]="statusCatat">
            <option value="AMAN">Saya Aman</option>
            <option value="BUTUH_BANTUAN">Butuh Bantuan</option>
          </select>
        </div>
        <div class="form-field">
          <label class="form-field__label" for="alasan-catat"
            >Alasan<span class="form-field__required">*</span></label
          >
          <textarea id="alasan-catat" name="alasan" [(ngModel)]="alasanCatat"></textarea>
          <p class="form-field__hint">5–300 karakter — dasar pencatatan atas nama pegawai lain.</p>
        </div>
        <div keuModalFooter>
          <button type="button" class="button button--secondary" (click)="dicatat.set(null)">
            Batal
          </button>
          <button type="button" class="button" (click)="kirimCatatAsync()" [disabled]="mengirim()">
            Simpan
          </button>
        </div>
      </keu-modal>
    }
  `,
})
export class RekapSafetyCheck implements OnInit {
  private readonly safetyCheck = inject(SafetyCheckService);

  protected readonly memuat = signal(true);
  protected readonly adaBroadcast = signal(false);
  protected readonly unit = signal<Rekap['unit'] | null>(null);
  protected readonly broadcastId = signal<string | null>(null);
  protected readonly broadcastJenis = signal('');
  protected readonly semua = signal<readonly RekapBaris[]>([]);

  protected readonly aman = signal<readonly RekapBaris[]>([]);
  protected readonly butuhBantuan = signal<readonly RekapBaris[]>([]);
  protected readonly belum = signal<readonly RekapBaris[]>([]);

  protected readonly dicatat = signal<RekapBaris | null>(null);
  protected statusCatat: 'AMAN' | 'BUTUH_BANTUAN' = 'BUTUH_BANTUAN';
  protected alasanCatat = '';
  protected readonly mengirim = signal(false);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.muatAsync();
  }

  private async muatAsync(): Promise<void> {
    this.memuat.set(true);
    try {
      // 404 = tidak ada broadcast aktif (keadaan normal, bukan galat): jangan tampilkan pesan galat.
      const rekap = await this.galat.jalankanAsync(() =>
        this.safetyCheck
          .rekapAsync(undefined, undefined, undefined, 1, 200)
          .catch((galat: unknown) => {
            if (galat instanceof HttpErrorResponse && galat.status === 404) {
              return null;
            }
            throw galat;
          }),
      );
      if (!rekap) {
        this.adaBroadcast.set(false);
        return;
      }
      this.adaBroadcast.set(true);
      this.unit.set(rekap.unit);
      this.broadcastId.set(rekap.broadcast.id);
      this.broadcastJenis.set(rekap.broadcast.jenisBencana);
      this.semua.set(rekap.data);
      this.aman.set(rekap.data.filter((b) => b.status === 'AMAN'));
      this.butuhBantuan.set(rekap.data.filter((b) => b.status === 'BUTUH_BANTUAN'));
      this.belum.set(rekap.data.filter((b) => b.status === 'BELUM'));
    } catch {
      this.adaBroadcast.set(false);
    } finally {
      this.memuat.set(false);
    }
  }

  protected bukaCatat(b: RekapBaris): void {
    this.statusCatat = 'BUTUH_BANTUAN';
    this.alasanCatat = '';
    this.dicatat.set(b);
  }

  protected async kirimCatatAsync(): Promise<void> {
    const target = this.dicatat();
    const broadcastId = this.broadcastId();
    if (!target || !broadcastId) {
      return;
    }

    this.mengirim.set(true);
    try {
      await this.safetyCheck.catatAsync(
        broadcastId,
        target.pegawai.id,
        this.statusCatat,
        this.alasanCatat,
      );
      this.dicatat.set(null);
      await this.muatAsync();
    } finally {
      this.mengirim.set(false);
    }
  }
}

/** Alias lokal supaya tipe `unit` mudah dirujuk di atas tanpa impor terpisah. */
type Rekap = import('./safety-check.model').Rekap;
