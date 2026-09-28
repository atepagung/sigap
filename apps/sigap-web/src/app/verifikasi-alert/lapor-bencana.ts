import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { KelompokBencana, Opsi } from '../core/referensi/referensi.model';
import { ReferensiService } from '../core/referensi/referensi.service';
import { LaporanService } from './laporan.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Laporkan Potensi Bencana (#7) + unggah lampiran (#8) — Pegawai Umum. */
@Component({
  selector: 'app-lapor-bencana',
  imports: [PesanGalat, FormsModule, HasPermissionDirective],
  styleUrl: './lapor-bencana.scss',
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Laporkan Potensi Bencana</h1>
      </div>
    </header>

    @if (!laporanId()) {
      <div class="table-card" *hasPermission="'sigap:laporan:create'">
        <div class="table-card__header">
          <h2 class="table-card__title">Form Laporan</h2>
        </div>
        <div class="lapor-bencana__isi">
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
            <label class="form-field__label" for="level"
              >Level Keparahan<span class="form-field__required">*</span></label
            >
            <select id="level" name="level" [(ngModel)]="level">
              <option value="">— Pilih —</option>
              @for (o of levelOpsi(); track o.kode) {
                <option [value]="o.kode">{{ o.label }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label class="form-field__label" for="lokasi"
              >Lokasi<span class="form-field__required">*</span></label
            >
            <input id="lokasi" type="text" name="lokasi" [(ngModel)]="lokasi" required />
          </div>
          <div class="form-field">
            <label class="form-field__label" for="deskripsi">Deskripsi</label>
            <textarea id="deskripsi" name="deskripsi" [(ngModel)]="deskripsi"></textarea>
          </div>
          <app-pesan-galat [pesan]="galat.pesanAksi()" />
          <button
            type="button"
            class="button"
            (click)="kirimAsync()"
            [disabled]="!sah() || mengirim()"
          >
            Kirim Laporan
          </button>
        </div>
      </div>
    } @else {
      <div class="table-card">
        <div class="table-card__header">
          <h2 class="table-card__title">Laporan Terkirim — Lampiran (opsional)</h2>
        </div>
        <div class="lapor-bencana__isi">
          <p class="form-field__hint">
            Foto, video, atau pesan suara — maksimum 10 MB per berkas, 5 berkas.
          </p>
          <app-pesan-galat [pesan]="galat.pesanAksi()" />
          <input
            *hasPermission="'sigap:lampiran:upload'"
            type="file"
            (change)="unggahAsync($event)"
            [disabled]="mengunggah()"
          />
          <ul>
            @for (l of lampiran(); track l.id) {
              <li>{{ l.tipe }} — {{ l.ukuranBytes }} bytes</li>
            }
          </ul>
          <button type="button" class="button button--secondary" (click)="selesai()">
            Selesai
          </button>
        </div>
      </div>
    }
  `,
})
export class LaporBencana implements OnInit {
  private readonly referensi = inject(ReferensiService);
  private readonly laporan = inject(LaporanService);
  private readonly router = inject(Router);

  protected readonly kelompok = signal<readonly KelompokBencana[]>([]);
  protected readonly levelOpsi = signal<readonly Opsi[]>([]);
  protected readonly mengirim = signal(false);
  protected readonly mengunggah = signal(false);
  protected readonly laporanId = signal<string | null>(null);
  protected readonly lampiran = signal<
    readonly { id: string; tipe: string; ukuranBytes: number | null }[]
  >([]);

  protected jenisBencana = '';
  protected level = '';
  protected lokasi = '';
  protected deskripsi = '';

  protected sah(): boolean {
    return this.jenisBencana.length > 0 && this.level.length > 0 && this.lokasi.length > 0;
  }

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() =>
      Promise.all([this.referensi.jenisBencanaAsync(), this.referensi.opsiAsesmenAsync()]),
    );
    if (!hasil) {
      return;
    }
    const [jenis, opsi] = hasil;
    this.kelompok.set(jenis.data);
    this.levelOpsi.set(opsi['laporan.level'] ?? []);
  }

  protected async kirimAsync(): Promise<void> {
    this.mengirim.set(true);
    const hasil = await this.galat.jalankanAksiAsync(() =>
      this.laporan.buatAsync(
        this.jenisBencana,
        this.level,
        this.lokasi,
        this.deskripsi || undefined,
      ),
    );
    this.mengirim.set(false);
    if (hasil) {
      this.laporanId.set(hasil.id);
    }
  }

  protected async unggahAsync(e: Event): Promise<void> {
    const id = this.laporanId();
    const berkas = (e.target as HTMLInputElement).files?.[0];
    if (!id || !berkas) {
      return;
    }

    this.mengunggah.set(true);
    const hasil = await this.galat.jalankanAksiAsync(() =>
      this.laporan.unggahLampiranAsync(id, berkas),
    );
    this.mengunggah.set(false);
    if (hasil) {
      this.lampiran.update((l) => [...l, hasil]);
    }
  }

  protected selesai(): void {
    void this.router.navigate(['/laporan-saya']);
  }
}
