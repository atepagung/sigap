import { Component, OnInit, inject, signal } from '@angular/core';
import { AspekAgregat, Histogram } from './monitor.model';
import { MonitorService } from './monitor.service';
import { PenampungGalat } from '../core/galat/penampung-galat';
import { PesanGalat } from '../shared/pesan-galat/pesan-galat';

/**
 * Agregat lima aspek atas versi terkini tiap seri di lingkup (#33, 2.6.2).
 *
 * [ASUMSI] Katalog belum punya komponen grafik, jadi histogram tiap field ditampilkan sebagai
 * tabel kode → jumlah, bukan diagram batang/lingkaran — dilaporkan sebagai kebutuhan katalog,
 * bukan dibangun sendiri di sini.
 */
@Component({
  selector: 'app-dashboard-aspek',
  imports: [PesanGalat],
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
        <div class="table-card__header">
          <h2 class="table-card__title">SDM — Kelengkapan Hadir</h2>
        </div>
        <table>
          <tbody>
            @for (e of entries(a.sdm.kelengkapanHadir); track e[0]) {
              <tr>
                <th>{{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">Aset</h2></div>
        <table>
          <tbody>
            @for (e of entries(a.aset.konstruksiBangunan); track e[0]) {
              <tr>
                <th>Konstruksi: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
            @for (e of entries(a.aset.aksesLokasi); track e[0]) {
              <tr>
                <th>Akses Lokasi: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
            @for (e of entries(a.aset.kendaraanLaikOperasi); track e[0]) {
              <tr>
                <th>Kendaraan: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">TIK</h2></div>
        <table>
          <tbody>
            @for (e of entries(a.tik.aksesJaringan); track e[0]) {
              <tr>
                <th>Akses Jaringan: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
            @for (e of entries(a.tik.kelistrikan); track e[0]) {
              <tr>
                <th>Kelistrikan: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
            @for (e of entries(a.tik.aplikasiUtama); track e[0]) {
              <tr>
                <th>Aplikasi Utama: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="table-card">
        <div class="table-card__header"><h2 class="table-card__title">Arsip</h2></div>
        <table>
          <tbody>
            @for (e of entries(a.arsip.arsipVital); track e[0]) {
              <tr>
                <th>Arsip Vital: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
            @for (e of entries(a.arsip.evakuasiFisik); track e[0]) {
              <tr>
                <th>Evakuasi Fisik: {{ e[0] }}</th>
                <td>{{ e[1] }}</td>
              </tr>
            }
          </tbody>
        </table>
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

  protected entries(h: Histogram): readonly (readonly [string, number])[] {
    return Object.entries(h);
  }
}
