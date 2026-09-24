import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { KelompokBencana, OpsiAsesmenSemua } from '../core/referensi/referensi.model';
import { ReferensiService } from '../core/referensi/referensi.service';
import { Asesmen } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

interface BarisLayanan {
  readonly layananId: string;
  readonly nama: string;
  status: string;
}

const ASPEK = ['sdm', 'aset', 'tik', 'arsip'] as const;
const CATATAN: Record<(typeof ASPEK)[number], readonly string[]> = {
  sdm: ['sdm.catatanKondisiPegawai', 'sdm.catatanTambahan'],
  aset: ['aset.catatan'],
  tik: ['tik.catatan'],
  arsip: ['arsip.catatan'],
};

/**
 * Form Asesmen Kondisi Bencana, 5 aspek (#21, #22, #25). Koreksi stakeholder 9: tombol pertama
 * "Kirim", pengiriman berikutnya untuk seri yang sama "Update Asesmen". Tim Satgas saja yang
 * mengisi (koreksi 8: Pimpinan Satker hanya meninjau, tanpa form ini).
 *
 * Field berskala dibangun dari `GET /referensi/opsi-asesmen` (#38), bukan daftar tertulis di sini
 * — satu sumber dengan backend supaya field baru di kontrak otomatis muncul di formulir.
 */
@Component({
  selector: 'app-form-asesmen',
  imports: [PesanGalat, FormsModule, HasPermissionDirective],
  styleUrl: '../verifikasi-alert/lapor-bencana.scss',
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Asesmen Kondisi Bencana</h1>
        <p class="page-header__subtitle">
          {{
            asesmenIdSedangDiubah()
              ? 'Memperbarui asesmen versi #' + urutanBerikutnya()
              : 'Kirim asesmen baru'
          }}
        </p>
      </div>
    </header>

    <ng-container
      *hasPermission="asesmenIdSedangDiubah() ? 'sigap:asesmen:update' : 'sigap:asesmen:create'"
    >
      <div class="table-card">
        <div class="table-card__header">
          <h2 class="table-card__title">Kondisi Bencana</h2>
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
            <label class="form-field__label" for="kondisi-fisik"
              >Kondisi Fisik<span class="form-field__required">*</span></label
            >
            <select id="kondisi-fisik" name="kondisiFisik" [(ngModel)]="kondisiFisik">
              <option value="">— Pilih —</option>
              @for (o of opsi()['kondisiBencana.kondisiFisik']; track o.kode) {
                <option [value]="o.kode">{{ o.label }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label class="form-field__label" for="uraian">Uraian</label>
            <textarea id="uraian" name="uraian" [(ngModel)]="uraian"></textarea>
          </div>
        </div>
      </div>

      @for (aspek of aspekDaftar; track aspek) {
        <div class="table-card">
          <div class="table-card__header">
            <h2 class="table-card__title">Aspek {{ labelAspek(aspek) }}</h2>
          </div>
          <div class="lapor-bencana__isi">
            @for (kunci of kunciPerAspek(aspek); track kunci) {
              <div class="form-field">
                <label class="form-field__label" [attr.for]="kunci"
                  >{{ labelField(kunci) }}<span class="form-field__required">*</span></label
                >
                <select [id]="kunci" [name]="kunci" [(ngModel)]="model[kunci]">
                  <option value="">— Pilih —</option>
                  @for (o of opsi()[kunci]; track o.kode) {
                    <option [value]="o.kode">{{ o.label }}</option>
                  }
                </select>
              </div>
            }
            @for (kunciCatatan of catatanPerAspek(aspek); track kunciCatatan) {
              <div class="form-field">
                <label class="form-field__label" [attr.for]="kunciCatatan">{{
                  labelField(kunciCatatan)
                }}</label>
                <textarea
                  [id]="kunciCatatan"
                  [name]="kunciCatatan"
                  [(ngModel)]="model[kunciCatatan]"
                ></textarea>
              </div>
            }
          </div>
        </div>
      }

      <div class="table-card">
        <div class="table-card__header">
          <h2 class="table-card__title">Aspek Layanan</h2>
        </div>
        <table>
          <thead>
            <tr>
              <th>Layanan</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            @for (b of barisLayanan(); track b.layananId) {
              <tr>
                <td>{{ b.nama }}</td>
                <td>
                  <select [name]="'layanan-' + b.layananId" [(ngModel)]="b.status">
                    @for (o of opsi()['layanan.status']; track o.kode) {
                      <option [value]="o.kode">{{ o.label }}</option>
                    }
                  </select>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="2" class="table-card__empty">
                  Belum ada layanan kritis terdaftar — daftarkan dulu di halaman Layanan Kritis.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <app-pesan-galat [pesan]="galat.pesanAksi()" />
      <button type="button" class="button" (click)="kirimAsync()" [disabled]="!sah() || mengirim()">
        {{ asesmenIdSedangDiubah() ? 'Update Asesmen' : 'Kirim' }}
      </button>
    </ng-container>
  `,
})
export class FormAsesmen implements OnInit {
  private readonly referensi = inject(ReferensiService);
  private readonly asesmen = inject(AsesmenService);
  private readonly router = inject(Router);

  protected readonly aspekDaftar = ASPEK;

  protected readonly kelompok = signal<readonly KelompokBencana[]>([]);
  protected readonly opsi = signal<OpsiAsesmenSemua>({});
  protected readonly barisLayanan = signal<BarisLayanan[]>([]);
  protected readonly mengirim = signal(false);
  protected readonly asesmenIdSedangDiubah = signal<string | null>(null);
  protected readonly urutanBerikutnya = signal(1);

  protected jenisBencana = '';
  protected kondisiFisik = '';
  protected uraian = '';
  protected readonly model: Record<string, string> = {};

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const hasil = await this.galat.jalankanAsync(() =>
      Promise.all([
        this.referensi.jenisBencanaAsync(),
        this.referensi.opsiAsesmenAsync(),
        this.asesmen.layananKritisAsync(),
        this.asesmen.terkiniAsync(),
      ]),
    );
    if (!hasil) {
      return;
    }
    const [jenis, opsiAsesmen, layanan, terkini] = hasil;
    this.kelompok.set(jenis.data);
    this.opsi.set(opsiAsesmen);
    this.barisLayanan.set(
      layanan.data.map((l): BarisLayanan => ({ layananId: l.id, nama: l.nama, status: 'NORMAL' })),
    );
    this.urutanBerikutnya.set(terkini.urutanBerikutnya);

    if (terkini.asesmen) {
      this.isiDariAsesmenTerkini(terkini.asesmen);
    }
  }

  private isiDariAsesmenTerkini(a: Asesmen): void {
    this.asesmenIdSedangDiubah.set(a.id);
    this.jenisBencana = a.kondisiBencana.jenisBencana ?? '';
    this.kondisiFisik = a.kondisiBencana.kondisiFisik ?? '';
    this.uraian = a.kondisiBencana.uraian ?? '';

    const isiAspek = (prefix: string, obj: object | null): void => {
      if (!obj) return;
      for (const [k, v] of Object.entries(obj)) {
        if (typeof v === 'string') this.model[`${prefix}.${k}`] = v;
      }
    };
    isiAspek('sdm', a.aspek.sdm);
    isiAspek('aset', a.aspek.aset);
    isiAspek('tik', a.aspek.tik);
    isiAspek('arsip', a.aspek.arsip);

    if (a.aspek.layanan) {
      const perId = new Map(a.aspek.layanan.map((l) => [l.layananId, l.status]));
      this.barisLayanan.update((baris) =>
        baris.map((b) => ({ ...b, status: perId.get(b.layananId) ?? b.status })),
      );
    }
  }

  protected kunciPerAspek(aspek: string): readonly string[] {
    return Object.keys(this.opsi()).filter((k) => k.startsWith(`${aspek}.`));
  }

  protected catatanPerAspek(aspek: (typeof ASPEK)[number]): readonly string[] {
    return CATATAN[aspek];
  }

  protected labelAspek(aspek: string): string {
    return { sdm: 'SDM', aset: 'Aset', tik: 'TIK', arsip: 'Arsip' }[aspek] ?? aspek;
  }

  protected labelField(kunci: string): string {
    const bagian = kunci.split('.')[1] ?? kunci;
    const dipisah = bagian.replace(/([A-Z])/g, ' $1');
    return dipisah.charAt(0).toUpperCase() + dipisah.slice(1);
  }

  protected sah(): boolean {
    if (!this.jenisBencana || !this.kondisiFisik) {
      return false;
    }

    return ASPEK.every((aspek) => this.kunciPerAspek(aspek).every((k) => !!this.model[k]));
  }

  protected async kirimAsync(): Promise<void> {
    this.mengirim.set(true);
    const isi = {
      kondisiBencana: {
        jenisBencana: this.jenisBencana,
        kondisiFisik: this.kondisiFisik,
        uraian: this.uraian || undefined,
      },
      aspek: {
        sdm: this.subObjek('sdm'),
        aset: this.subObjek('aset'),
        tik: this.subObjek('tik'),
        arsip: this.subObjek('arsip'),
        layanan: this.barisLayanan().map((b) => ({ layananId: b.layananId, status: b.status })),
      },
    };

    const id = this.asesmenIdSedangDiubah();
    const hasil = await this.galat.jalankanAksiAsync(() =>
      id ? this.asesmen.revisiAsync(id, isi) : this.asesmen.kirimAsync(isi),
    );
    this.mengirim.set(false);
    if (hasil) {
      await this.router.navigate(['/detail-asesmen', hasil.id]);
    }
  }

  private subObjek(aspek: string): Record<string, string | null> {
    const hasil: Record<string, string | null> = {};
    for (const kunci of [
      ...this.kunciPerAspek(aspek),
      ...this.catatanPerAspek(aspek as (typeof ASPEK)[number]),
    ]) {
      const field = kunci.split('.')[1];
      hasil[field] = this.model[kunci] || null;
    }

    return hasil;
  }
}
