import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HasPermissionDirective } from '@danarakca/iam';
import { Peringatan } from './notifikasi.model';
import { StatusPush, LanggananPush } from './langganan-push.service';
import { NotifikasiService } from './notifikasi.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Keadaan yang tidak dapat diubah pengguna dijelaskan apa adanya, bukan disembunyikan. */
const KETERANGAN_PUSH: Record<StatusPush, string> = {
  memeriksa: 'Memeriksa status notifikasi…',
  aktif: 'Aktif: peringatan genting akan muncul di perangkat ini walau halaman ini tertutup.',
  mati: 'Belum aktif: peringatan hanya terlihat di halaman ini.',
  ditolak: 'Diblokir di peramban ini. Ubah izin notifikasi situs di pengaturan peramban.',
  'tidak-didukung': 'Peramban ini tidak mendukung notifikasi di perangkat.',
  'tidak-dikonfigurasi':
    'Belum tersedia: notifikasi di perangkat belum diaktifkan untuk sistem ini.',
  'tanpa-service-worker':
    'Belum tersedia: platform belum menyiapkan service worker untuk notifikasi.',
};

/** Peringatan yang dihitung saat diminta (#43) — tanpa status "sudah dibaca" (skema tidak punya tabelnya). */
@Component({
  selector: 'app-notifikasi',
  imports: [PesanGalat, HasPermissionDirective],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Notifikasi</h1>
      </div>
    </header>

    <section
      *hasPermission="'sigap:notifikasi:subscribe'"
      class="table-card"
      aria-label="Notifikasi di perangkat ini"
    >
      <p>
        <strong>Notifikasi di perangkat ini.</strong>
        {{ keterangan() }}
      </p>
      @if (galat.pesanAksi(); as pesan) {
        <p class="table-card__empty" role="alert">{{ pesan }}</p>
      }
      @if (bisaDiubah()) {
        <button
          type="button"
          class="button button--secondary"
          [disabled]="memproses()"
          (click)="ubahAsync()"
        >
          {{ push.status() === 'aktif' ? 'Matikan notifikasi' : 'Aktifkan notifikasi' }}
        </button>
      }
    </section>

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

  protected readonly push = inject(LanggananPush);

  protected readonly memproses = signal(false);

  protected readonly bisaDiubah = computed(() => ['aktif', 'mati'].includes(this.push.status()));

  protected readonly keterangan = computed(() => KETERANGAN_PUSH[this.push.status()]);

  protected readonly daftar = signal<readonly Peringatan[]>([]);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    await this.push.periksaAsync();
    const hasil = await this.galat.jalankanAsync(() => this.notifikasi.peringatanAsync());
    if (hasil) {
      this.daftar.set(hasil.data);
    }
  }

  protected async ubahAsync(): Promise<void> {
    this.memproses.set(true);
    try {
      await this.galat.jalankanAksiAsync(() =>
        this.push.status() === 'aktif' ? this.push.matikanAsync() : this.push.aktifkanAsync(),
      );
    } finally {
      this.memproses.set(false);
    }
  }
}
