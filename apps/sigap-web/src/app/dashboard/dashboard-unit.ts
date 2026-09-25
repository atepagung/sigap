import { DatePipe } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { UnitDetail } from './monitor.model';
import { MonitorService } from './monitor.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * "Lihat Detail" satu unit dari dashboard (#35). Unit di luar lingkup dijawab 404 oleh API dan
 * ditampilkan sebagai "Data tidak ditemukan" (`PenampungGalat`) — tidak dibedakan dari unit yang
 * memang tidak ada.
 */
@Component({
  selector: 'app-dashboard-unit',
  imports: [PesanGalat, DatePipe, RouterLink],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    @if (detail(); as d) {
      <header class="page-header">
        <div>
          <h1 class="page-header__title">{{ d.unit.nama }}</h1>
          <p class="page-header__subtitle">{{ d.unit.provinsi }} — {{ d.unit.kabupatenKota }}</p>
        </div>
      </header>

      <div class="stats-row">
        @if (d.tanggapDarurat; as td) {
          <div
            class="stat-card"
            [class.stat-card--danger]="td.status === 'DARURAT'"
            [class.stat-card--success]="td.status === 'PULIH'"
          >
            <p class="stat-card__label">Tanggap Darurat</p>
            <p class="stat-card__value">{{ td.status }}</p>
            <p class="stat-card__context">
              {{ td.jenisBencana }}, sejak {{ td.sejak | date: 'medium' }}
            </p>
          </div>
        }
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">Safety Check</h2></div>
        <table>
          <thead>
            <tr>
              <th>Jenis Bencana</th>
              <th>Total Pegawai</th>
              <th>Aman</th>
              <th>Butuh Bantuan</th>
              <th>Belum Merespons</th>
            </tr>
          </thead>
          <tbody>
            @for (sc of d.safetyCheck; track sc.broadcast.id) {
              <tr>
                <td>{{ sc.broadcast.jenisBencana }}</td>
                <td>{{ sc.totalPegawai }}</td>
                <td>{{ sc.aman }}</td>
                <td>{{ sc.butuhBantuan }}</td>
                <td>{{ sc.belumMerespons }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="table-card__empty">
                  Tidak ada broadcast aktif untuk unit ini.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      @if (d.asesmenTerkini; as a) {
        <div class="table-card">
          <div class="table-card__header">
            <h2 class="table-card__title">Asesmen Terkini</h2>
            <div class="table-card__toolbar">
              <a class="button button--secondary" [routerLink]="['/detail-asesmen', a.id]"
                >Lihat Detail</a
              >
            </div>
          </div>
          <p class="lapor-bencana__isi">Versi #{{ a.urutan }} — {{ a.persetujuan.status }}</p>
        </div>
      }

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">Layanan Terganggu</h2></div>
        <table>
          <thead>
            <tr>
              <th>Layanan</th>
              <th>Status</th>
              <th>Sisa RTO (jam)</th>
            </tr>
          </thead>
          <tbody>
            @for (l of d.layananTerganggu; track l.layanan.id) {
              <tr>
                <td>{{ l.layanan.nama }}</td>
                <td>
                  <span
                    class="status-badge"
                    [class.status-badge--warning]="l.status === 'TERGANGGU'"
                    [class.status-badge--danger]="l.status === 'BERHENTI_TOTAL'"
                  >
                    {{ l.status }}
                  </span>
                </td>
                <td>{{ l.sisaRtoJam.toFixed(1) }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="3" class="table-card__empty">Tidak ada layanan terganggu.</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styleUrl: '../verifikasi-alert/lapor-bencana.scss',
})
export class DashboardUnit implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly monitor = inject(MonitorService);

  protected readonly detail = signal<UnitDetail | null>(null);

  protected readonly galat = inject(PenampungGalat);

  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    // paramMap (bukan snapshot): berpindah unit lewat rute yang sama tidak membuat ulang komponen.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      void this.muatAsync(params.get('unitId'));
    });
  }

  private async muatAsync(unitId: string | null): Promise<void> {
    this.detail.set(null);
    if (!unitId) {
      return;
    }

    const hasil = await this.galat.jalankanAsync(() => this.monitor.unitAsync(unitId));
    // Abaikan jawaban basi bila pengguna sudah pindah ke unit lain selama menunggu.
    if (unitId === this.route.snapshot.paramMap.get('unitId')) {
      this.detail.set(hasil);
    }
  }
}
