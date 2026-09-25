import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { RingkasanMonitor } from './monitor.model';
import { MonitorService } from './monitor.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/** Dashboard Monitor SC & Sumber Daya — ringkasan lingkup pemanggil (#30). */
@Component({
  selector: 'app-dashboard-ringkasan',
  imports: [PesanGalat, RouterLink],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Dashboard Monitor SC & Sumber Daya</h1>
        @if (ringkasan(); as r) {
          <p class="page-header__subtitle">
            Lingkup: {{ r.lingkup.label ?? r.lingkup.jenis }}
            @if (r.jenisBencana) {
              — {{ r.jenisBencana }}
            }
          </p>
        }
      </div>
      <div class="page-header__actions">
        <a class="button button--secondary" routerLink="/dashboard-safety-check">Rekap per Unit</a>
        <a class="button button--secondary" routerLink="/dashboard-asesmen-masuk">Asesmen Masuk</a>
        <a class="button button--secondary" routerLink="/dashboard-aspek">Aspek 5</a>
        <a class="button button--secondary" routerLink="/dashboard-layanan">Layanan Terganggu</a>
      </div>
    </header>

    @if (ringkasan(); as r) {
      <div class="stats-row">
        <div class="stat-card">
          <p class="stat-card__label">Total Pegawai (Safety Check)</p>
          <p class="stat-card__value">{{ r.safetyCheck.totalPegawai }}</p>
        </div>
        <div class="stat-card stat-card--success">
          <p class="stat-card__label">Aman</p>
          <p class="stat-card__value">{{ r.safetyCheck.aman }}</p>
        </div>
        <div class="stat-card stat-card--danger">
          <p class="stat-card__label">Butuh Bantuan</p>
          <p class="stat-card__value">{{ r.safetyCheck.butuhBantuan }}</p>
        </div>
        <div class="stat-card stat-card--warning">
          <p class="stat-card__label">Belum Merespons</p>
          <p class="stat-card__value">{{ r.safetyCheck.belumMerespons }}</p>
        </div>
        <div class="stat-card">
          <p class="stat-card__label">Unit Disasar / Belum</p>
          <p class="stat-card__value">
            {{ r.safetyCheck.jumlahUnitDisasar }} / {{ r.safetyCheck.jumlahUnitBelumDisasar }}
          </p>
        </div>
      </div>

      <div class="stats-row">
        <div class="stat-card">
          <p class="stat-card__label">Unit Melapor (Asesmen)</p>
          <p class="stat-card__value">{{ r.asesmen.unitMelapor }}</p>
        </div>
        <div class="stat-card stat-card--warning">
          <p class="stat-card__label">Menunggu Pimpinan</p>
          <p class="stat-card__value">{{ r.asesmen.menungguPimpinan }}</p>
        </div>
        <div class="stat-card stat-card--success">
          <p class="stat-card__label">Disetujui</p>
          <p class="stat-card__value">{{ r.asesmen.disetujui }}</p>
        </div>
        <div class="stat-card stat-card--danger">
          <p class="stat-card__label">Unit Tanggap Darurat</p>
          <p class="stat-card__value">{{ r.tanggapDarurat.unitDarurat }}</p>
        </div>
      </div>

      <div class="stats-row">
        <div class="stat-card stat-card--success">
          <p class="stat-card__label">Layanan Normal</p>
          <p class="stat-card__value">{{ r.layanan.normal }}</p>
        </div>
        <div class="stat-card stat-card--warning">
          <p class="stat-card__label">Layanan Terganggu</p>
          <p class="stat-card__value">{{ r.layanan.terganggu }}</p>
        </div>
        <div class="stat-card stat-card--danger">
          <p class="stat-card__label">Berhenti Total</p>
          <p class="stat-card__value">{{ r.layanan.berhentiTotal }}</p>
        </div>
      </div>
    }
  `,
})
export class DashboardRingkasan implements OnInit {
  private readonly monitor = inject(MonitorService);

  protected readonly ringkasan = signal<RingkasanMonitor | null>(null);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    this.ringkasan.set(await this.galat.jalankanAsync(() => this.monitor.ringkasanAsync()));
  }
}
