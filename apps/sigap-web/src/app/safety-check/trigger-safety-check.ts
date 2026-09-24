import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { KelompokBencana } from '../core/referensi/referensi.model';
import { ReferensiService } from '../core/referensi/referensi.service';
import { Pratinjau } from './broadcast.model';
import { BroadcastService } from './broadcast.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * Trigger Safety Check (#12, #13) — Tim Satgas, Kepala Perwakilan, Subkoordinator, Koordinator MKB
 * (koreksi stakeholder 10: Kepala Perwakilan wajib punya tombol ini di tingkat wilayah).
 * Penyempit (unit/provinsi/kabupatenKota/eselonI) yang berlaku bergantung lingkup pemicu — field
 * yang tidak relevan ditolak API (400 `PENYEMPIT_TIDAK_BERLAKU`), jadi form membiarkan keempatnya
 * terisi bebas dan menyerahkan validasinya ke API.
 */
@Component({
  selector: 'app-trigger-safety-check',
  imports: [PesanGalat, FormsModule, HasPermissionDirective],
  styleUrl: './trigger-safety-check.scss',
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <app-pesan-galat [pesan]="galat.pesanAksi()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Trigger Safety Check</h1>
        <p class="page-header__subtitle">Siapa pun lebih dulu tahu, dialah yang memicu.</p>
      </div>
    </header>

    <div class="table-card" *hasPermission="'sigap:broadcast:trigger'">
      <div class="table-card__header">
        <h2 class="table-card__title">Kriteria</h2>
      </div>
      <div class="trigger-safety-check__isi">
        <div class="form-field">
          <label class="form-field__label" for="jenis"
            >Jenis Bencana<span class="form-field__required">*</span></label
          >
          <select id="jenis" name="jenis" [(ngModel)]="jenisBencana">
            <option value="">— Pilih —</option>
            @for (k of kelompok(); track k.kategori) {
              @for (j of k.jenis; track j) {
                <option [value]="j">{{ j }}</option>
              }
            }
          </select>
        </div>
        <div class="form-field">
          <label class="form-field__label" for="provinsi">Provinsi (penyempit)</label>
          <input id="provinsi" type="text" name="provinsi" [(ngModel)]="provinsi" />
        </div>
        <div class="form-field">
          <label class="form-field__label" for="kabkota">Kabupaten/Kota (penyempit)</label>
          <input id="kabkota" type="text" name="kabkota" [(ngModel)]="kabupatenKota" />
        </div>
        <div class="form-field">
          <label class="form-field__label" for="eselon">Eselon I (penyempit)</label>
          <input id="eselon" type="text" name="eselon" [(ngModel)]="eselonI" />
        </div>
        <div class="form-field">
          <label class="form-field__label" for="pesan">Pesan (opsional)</label>
          <textarea id="pesan" name="pesan" [(ngModel)]="pesan" maxlength="500"></textarea>
        </div>
        <button
          type="button"
          class="button button--secondary"
          (click)="pratinjauAsync()"
          [disabled]="!jenisBencana || memproses()"
        >
          Pratinjau Sasaran
        </button>
      </div>
    </div>

    @if (pratinjau(); as p) {
      <div class="stats-row">
        <div class="stat-card">
          <p class="stat-card__label">Unit Disasar</p>
          <p class="stat-card__value">{{ p.disasar.jumlahUnit }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Pegawai Disasar</p>
          <p class="stat-card__value">{{ p.disasar.jumlahPegawai }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Unit Dilewati (sudah dipegang)</p>
          <p class="stat-card__value">{{ p.dilewati.length }}</p>
        </div>
      </div>

      <button
        *hasPermission="'sigap:broadcast:trigger'"
        type="button"
        class="button"
        (click)="picuAsync()"
        [disabled]="memproses()"
      >
        Picu Safety Check
      </button>
    }
  `,
})
export class TriggerSafetyCheck implements OnInit {
  private readonly referensi = inject(ReferensiService);
  private readonly broadcast = inject(BroadcastService);
  private readonly router = inject(Router);

  protected readonly kelompok = signal<readonly KelompokBencana[]>([]);
  protected readonly pratinjau = signal<Pratinjau | null>(null);
  protected readonly memproses = signal(false);

  protected jenisBencana = '';
  protected provinsi = '';
  protected kabupatenKota = '';
  protected eselonI = '';
  protected pesan = '';

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const daftar = await this.galat.jalankanAsync(() => this.referensi.jenisBencanaAsync());
    if (daftar) {
      this.kelompok.set(daftar.data);
    }
  }

  protected async pratinjauAsync(): Promise<void> {
    this.memproses.set(true);
    const hasil = await this.galat.jalankanAksiAsync(() =>
      this.broadcast.pratinjauAsync(this.jenisBencana, {
        provinsi: this.provinsi || undefined,
        kabupatenKota: this.kabupatenKota || undefined,
        eselonI: this.eselonI || undefined,
      }),
    );
    this.memproses.set(false);
    if (hasil) {
      this.pratinjau.set(hasil);
    }
  }

  protected async picuAsync(): Promise<void> {
    this.memproses.set(true);
    const hasil = await this.galat.jalankanAksiAsync(() =>
      this.broadcast.picuAsync({
        jenisBencana: this.jenisBencana,
        pesan: this.pesan || undefined,
        penyempit: {
          provinsi: this.provinsi || undefined,
          kabupatenKota: this.kabupatenKota || undefined,
          eselonI: this.eselonI || undefined,
        },
      }),
    );
    this.memproses.set(false);
    if (hasil) {
      await this.router.navigate(['detail-broadcast', hasil.id]);
    }
  }
}
