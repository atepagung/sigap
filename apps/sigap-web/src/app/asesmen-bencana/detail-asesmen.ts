import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HasPermissionDirective } from '@danarakca/iam';
import { OpsiAsesmenSemua } from '../core/referensi/referensi.model';
import { ReferensiService } from '../core/referensi/referensi.service';
import { bukaLampiranAsync } from '../shared/lampiran/buka-lampiran';
import { Asesmen } from './asesmen.model';
import { AsesmenService } from './asesmen.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * Detail asesmen (#26, #27) beserta persetujuan Pimpinan (#28) dan penyelesaian tanggap darurat
 * (#29). Koreksi stakeholder 8: Pimpinan Satker HANYA meninjau lima aspek, tanpa form ulang jenis
 * bencana/lokasi — halaman ini memang tidak punya field yang bisa diedit sama sekali untuk peran itu.
 */
@Component({
  selector: 'app-detail-asesmen',
  imports: [PesanGalat, HasPermissionDirective, DatePipe],
  styleUrl: '../verifikasi-alert/lapor-bencana.scss',
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    @if (asesmen(); as a) {
      <header class="page-header">
        <div>
          <h1 class="page-header__title">
            {{ a.kondisiBencana.jenisBencana }} — {{ a.unit.nama }}
          </h1>
          <p class="page-header__subtitle">
            Versi #{{ a.urutan }}, dikirim {{ a.dikirimOleh.nama }} pada
            {{ a.dikirimPada | date: 'medium' }}
          </p>
        </div>
        <div class="page-header__actions">
          @if (a.persetujuan.status === 'MENUNGGU_PIMPINAN') {
            <button
              *hasPermission="'sigap:asesmen:approve'"
              type="button"
              class="button button--success"
              (click)="setujuiAsync()"
              [disabled]="memproses()"
            >
              Setujui Asesmen
            </button>
          } @else if (a.persetujuan.tanggapDarurat?.status === 'DARURAT') {
            <button
              *hasPermission="'sigap:tanggap-darurat:close'"
              type="button"
              class="button"
              (click)="selesaikanAsync()"
              [disabled]="memproses()"
            >
              Selesaikan Tanggap Darurat
            </button>
          }
        </div>
      </header>

      <div class="stats-row">
        <div class="stat-card">
          <p class="stat-card__label">Kondisi Fisik</p>
          <p class="stat-card__value">
            {{ labelOpsi('kondisiBencana.kondisiFisik', a.kondisiBencana.kondisiFisik) }}
          </p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Status Persetujuan</p>
          <p class="stat-card__value">{{ a.persetujuan.status }}</p>
        </div>
        @if (a.persetujuan.tanggapDarurat; as td) {
          <div
            class="stat-card"
            [class.stat-card--danger]="td.status === 'DARURAT'"
            [class.stat-card--success]="td.status === 'PULIH'"
          >
            <p class="stat-card__label">Tanggap Darurat</p>
            <p class="stat-card__value">{{ td.status }}</p>
          </div>
        }
      </div>

      @if (a.kondisiBencana.uraian) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Uraian</h2></div>
          <p class="lapor-bencana__isi">{{ a.kondisiBencana.uraian }}</p>
        </div>
      }

      @if (a.aspek.sdm; as sdm) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Aspek SDM</h2></div>
          <table>
            <tbody>
              <tr>
                <th>Kelengkapan Hadir</th>
                <td>{{ labelOpsi('sdm.kelengkapanHadir', sdm.kelengkapanHadir) }}</td>
              </tr>
              <tr>
                <th>Korban Jiwa</th>
                <td>{{ labelOpsi('sdm.korbanJiwa', sdm.korbanJiwa) }}</td>
              </tr>
              <tr>
                <th>Kondisi Fisik</th>
                <td>{{ labelOpsi('sdm.kondisiFisik', sdm.kondisiFisik) }}</td>
              </tr>
              <tr>
                <th>Kondisi Psikis</th>
                <td>{{ labelOpsi('sdm.kondisiPsikis', sdm.kondisiPsikis) }}</td>
              </tr>
              <tr>
                <th>Catatan Kondisi Pegawai</th>
                <td>{{ sdm.catatanKondisiPegawai ?? 'Tidak tersedia' }}</td>
              </tr>
              <tr>
                <th>Catatan Tambahan</th>
                <td>{{ sdm.catatanTambahan ?? 'Tidak tersedia' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      }

      @if (a.aspek.aset; as aset) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Aspek Aset</h2></div>
          <table>
            <tbody>
              <tr>
                <th>Konstruksi Bangunan</th>
                <td>{{ labelOpsi('aset.konstruksiBangunan', aset.konstruksiBangunan) }}</td>
              </tr>
              <tr>
                <th>Akses Lokasi</th>
                <td>{{ labelOpsi('aset.aksesLokasi', aset.aksesLokasi) }}</td>
              </tr>
              <tr>
                <th>Kondisi Peralatan</th>
                <td>{{ labelOpsi('aset.kondisiPeralatan', aset.kondisiPeralatan) }}</td>
              </tr>
              <tr>
                <th>Jumlah Peralatan</th>
                <td>{{ labelOpsi('aset.jumlahPeralatan', aset.jumlahPeralatan) }}</td>
              </tr>
              <tr>
                <th>Kondisi Perlengkapan</th>
                <td>{{ labelOpsi('aset.kondisiPerlengkapan', aset.kondisiPerlengkapan) }}</td>
              </tr>
              <tr>
                <th>Jumlah Perlengkapan</th>
                <td>{{ labelOpsi('aset.jumlahPerlengkapan', aset.jumlahPerlengkapan) }}</td>
              </tr>
              <tr>
                <th>Kendaraan Laik Operasi</th>
                <td>{{ labelOpsi('aset.kendaraanLaikOperasi', aset.kendaraanLaikOperasi) }}</td>
              </tr>
              <tr>
                <th>Jumlah Kendaraan</th>
                <td>{{ labelOpsi('aset.jumlahKendaraan', aset.jumlahKendaraan) }}</td>
              </tr>
              <tr>
                <th>Catatan</th>
                <td>{{ aset.catatan ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      }

      @if (a.aspek.tik; as tik) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Aspek TIK</h2></div>
          <table>
            <tbody>
              <tr>
                <th>Kondisi Perangkat</th>
                <td>{{ labelOpsi('tik.kondisiPerangkat', tik.kondisiPerangkat) }}</td>
              </tr>
              <tr>
                <th>Jumlah Perangkat</th>
                <td>{{ labelOpsi('tik.jumlahPerangkat', tik.jumlahPerangkat) }}</td>
              </tr>
              <tr>
                <th>Akses Jaringan</th>
                <td>{{ labelOpsi('tik.aksesJaringan', tik.aksesJaringan) }}</td>
              </tr>
              <tr>
                <th>Kelistrikan</th>
                <td>{{ labelOpsi('tik.kelistrikan', tik.kelistrikan) }}</td>
              </tr>
              <tr>
                <th>Aplikasi Utama</th>
                <td>{{ labelOpsi('tik.aplikasiUtama', tik.aplikasiUtama) }}</td>
              </tr>
              <tr>
                <th>Catatan</th>
                <td>{{ tik.catatan ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      }

      @if (a.aspek.arsip; as arsip) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Aspek Arsip</h2></div>
          <table>
            <tbody>
              <tr>
                <th>Arsip Vital</th>
                <td>{{ labelOpsi('arsip.arsipVital', arsip.arsipVital) }}</td>
              </tr>
              <tr>
                <th>Arsip Penting</th>
                <td>{{ labelOpsi('arsip.arsipPenting', arsip.arsipPenting) }}</td>
              </tr>
              <tr>
                <th>Evakuasi Fisik</th>
                <td>{{ labelOpsi('arsip.evakuasiFisik', arsip.evakuasiFisik) }}</td>
              </tr>
              <tr>
                <th>Catatan</th>
                <td>{{ arsip.catatan ?? '—' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      }

      @if (a.aspek.layanan; as layanan) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Aspek Layanan</h2></div>
          <table>
            <thead>
              <tr>
                <th>Layanan</th>
                <th>RTO</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (l of layanan; track l.layananId) {
                <tr>
                  <td>{{ l.nama }}</td>
                  <td>{{ l.rtoJam }} jam</td>
                  <td>
                    <span
                      class="status-badge"
                      [class.status-badge--success]="l.status === 'NORMAL'"
                      [class.status-badge--warning]="l.status === 'TERGANGGU'"
                      [class.status-badge--danger]="l.status === 'BERHENTI_TOTAL'"
                    >
                      {{ labelOpsi('layanan.status', l.status) }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (a.lampiran.length > 0) {
        <div class="table-card">
          <div class="table-card__header"><h2 class="table-card__title">Lampiran</h2></div>
          <ul class="lapor-bencana__isi">
            @for (lm of a.lampiran; track lm.id) {
              <li>
                <button type="button" class="button button--ghost" (click)="bukaAsync(lm.url)">
                  {{ lm.tipe }}
                </button>
              </li>
            }
          </ul>
        </div>
      }
    }
  `,
})
export class DetailAsesmen implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly asesmenService = inject(AsesmenService);
  private readonly referensi = inject(ReferensiService);
  private readonly http = inject(HttpClient);

  protected readonly asesmenSignal = signal<Asesmen | null>(null);
  protected readonly opsi = signal<OpsiAsesmenSemua>({});
  protected readonly memproses = signal(false);

  protected asesmen(): Asesmen | null {
    return this.asesmenSignal();
  }

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    const id = this.route.snapshot.paramMap.get('id');
    const hasil = await this.galat.jalankanAsync(async () => ({
      opsi: await this.referensi.opsiAsesmenAsync(),
      asesmen: id ? await this.asesmenService.bacaAsync(id) : null,
    }));
    if (hasil) {
      this.opsi.set(hasil.opsi);
      this.asesmenSignal.set(hasil.asesmen);
    }
  }

  protected labelOpsi(kunci: string, kode: string | null): string {
    if (!kode) {
      return 'Tidak tersedia';
    }

    return this.opsi()[kunci]?.find((o) => o.kode === kode)?.label ?? kode;
  }

  protected async bukaAsync(url: string): Promise<void> {
    await bukaLampiranAsync(this.http, url);
  }

  protected async setujuiAsync(): Promise<void> {
    const a = this.asesmenSignal();
    if (!a) return;
    this.memproses.set(true);
    try {
      this.asesmenSignal.set(await this.asesmenService.setujuiAsync(a.id));
    } finally {
      this.memproses.set(false);
    }
  }

  protected async selesaikanAsync(): Promise<void> {
    const a = this.asesmenSignal();
    const tanggapDaruratId = a?.persetujuan.tanggapDarurat?.id;
    if (!tanggapDaruratId) return;
    this.memproses.set(true);
    try {
      await this.asesmenService.selesaikanTanggapDaruratAsync(tanggapDaruratId);
      this.asesmenSignal.set(await this.asesmenService.bacaAsync(a!.id));
    } finally {
      this.memproses.set(false);
    }
  }
}
