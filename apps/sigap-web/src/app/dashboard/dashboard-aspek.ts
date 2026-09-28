import { Component, OnInit, inject, signal } from '@angular/core';
import { AspekAgregat, Histogram } from './monitor.model';
import { MonitorService } from './monitor.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { KeuBarChartComponent, type KeuBarChartItem } from '@danarakca/keu-ui';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * Agregat lima aspek atas versi terkini tiap seri di lingkup (#33, 2.6.2).
 *
 * [ASUMSI] Histogram tiap field tampil sebagai diagram batang lewat `keu-bar-chart`, komponen dummy
 * yang menunggu jawaban BaTII soal grafik (DUMMY_REGISTRY 2.8). Aplikasi tidak menggambar sendiri.
 */
@Component({
  selector: 'app-dashboard-aspek',
  imports: [PesanGalat, KeuBarChartComponent],
  providers: [PenampungGalat],
  template: `
    <app-pesan-galat [pesan]="galat.pesan()" />
    <header class="page-header">
      <div>
        <h1 class="page-header__title">Aspek 5 — Ringkasan</h1>
        @if (aspek(); as a) {
          <p class="page-header__subtitle">{{ a.jumlahUnitMelapor }} unit melapor</p>
        }
      </div>
    </header>

    @if (aspek(); as a) {
      <div class="stats-row">
        <div class="stat-card stat-card--danger">
          <p class="stat-card__label">Unit Ada Korban Jiwa</p>
          <p class="stat-card__value">{{ a.sdm.unitAdaKorbanJiwa }}</p>
        </div>
        <div class="stat-card stat-card--danger">
          <p class="stat-card__label">Unit Ada Luka Berat</p>
          <p class="stat-card__value">{{ a.sdm.unitAdaLukaBerat }}</p>
        </div>
        <div class="stat-card stat-card--warning">
          <p class="stat-card__label">Unit Ada Trauma Berat</p>
          <p class="stat-card__value">{{ a.sdm.unitAdaTraumaBerat }}</p>
        </div>
        <div class="stat-card stat-card--success">
          <p class="stat-card__label">Layanan Normal</p>
          <p class="stat-card__value">{{ a.layanan.normal }}</p>
        </div>
        <div class="stat-card stat-card--warning">
          <p class="stat-card__label">Layanan Terganggu</p>
          <p class="stat-card__value">{{ a.layanan.terganggu }}</p>
        </div>
        <div class="stat-card stat-card--danger">
          <p class="stat-card__label">Berhenti Total</p>
          <p class="stat-card__value">{{ a.layanan.berhentiTotal }}</p>
        </div>
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">SDM</h2></div>
        <keu-bar-chart judul="Kelengkapan hadir" [data]="batang(a.sdm.kelengkapanHadir)" />
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">Aset</h2></div>
        <keu-bar-chart judul="Konstruksi bangunan" [data]="batang(a.aset.konstruksiBangunan)" />
        <keu-bar-chart judul="Akses lokasi" [data]="batang(a.aset.aksesLokasi)" />
        <keu-bar-chart
          judul="Kendaraan laik operasi"
          [data]="batang(a.aset.kendaraanLaikOperasi)"
        />
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">TIK</h2></div>
        <keu-bar-chart judul="Akses jaringan" [data]="batang(a.tik.aksesJaringan)" />
        <keu-bar-chart judul="Kelistrikan" [data]="batang(a.tik.kelistrikan)" />
        <keu-bar-chart judul="Aplikasi utama" [data]="batang(a.tik.aplikasiUtama)" />
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">Arsip</h2></div>
        <keu-bar-chart judul="Arsip vital" [data]="batang(a.arsip.arsipVital)" />
        <keu-bar-chart judul="Evakuasi fisik" [data]="batang(a.arsip.evakuasiFisik)" />
      </div>
    }
  `,
})
export class DashboardAspek implements OnInit {
  private readonly monitor = inject(MonitorService);

  protected readonly aspek = signal<AspekAgregat | null>(null);

  protected readonly galat = inject(PenampungGalat);

  async ngOnInit(): Promise<void> {
    this.aspek.set(await this.galat.jalankanAsync(() => this.monitor.aspekAsync()));
  }

  protected batang(h: Histogram): readonly KeuBarChartItem[] {
    return Object.entries(h).map(([label, nilai]) => ({ label, nilai }));
  }
}
