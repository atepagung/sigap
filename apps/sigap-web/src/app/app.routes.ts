import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { Beranda } from './beranda/beranda';
import { HalamanTidakDitemukan } from './halaman-tidak-ditemukan/halaman-tidak-ditemukan';
import { Masuk } from './masuk/masuk';

// Aturan platform: path RELATIF, flat, DILARANG nested routing (tanpa `children`).
export const routes: Routes = [
  { path: '', component: Beranda, canActivate: [authGuard] },
  { path: 'masuk', component: Masuk },

  // ── Safety Check / SOS + Trigger (#1–#6, #12–#16) ──
  {
    path: 'safety-check',
    loadComponent: () => import('./safety-check/safety-check-saya').then((m) => m.SafetyCheckSaya),
    canActivate: [authGuard],
  },
  {
    path: 'riwayat-safety-check',
    loadComponent: () =>
      import('./safety-check/riwayat-safety-check').then((m) => m.RiwayatSafetyCheck),
    canActivate: [authGuard],
  },
  {
    path: 'rekap-safety-check',
    loadComponent: () =>
      import('./safety-check/rekap-safety-check').then((m) => m.RekapSafetyCheck),
    canActivate: [authGuard],
  },
  {
    path: 'trigger-safety-check',
    loadComponent: () =>
      import('./safety-check/trigger-safety-check').then((m) => m.TriggerSafetyCheck),
    canActivate: [authGuard],
  },
  {
    path: 'daftar-broadcast',
    loadComponent: () => import('./safety-check/daftar-broadcast').then((m) => m.DaftarBroadcast),
    canActivate: [authGuard],
  },
  {
    path: 'detail-broadcast/:id',
    loadComponent: () => import('./safety-check/detail-broadcast').then((m) => m.DetailBroadcast),
    canActivate: [authGuard],
  },

  // ── Laporkan Potensi Bencana + Verifikasi Alert (#7–#11, #17, #18) ──
  {
    path: 'lapor-bencana',
    loadComponent: () => import('./verifikasi-alert/lapor-bencana').then((m) => m.LaporBencana),
    canActivate: [authGuard],
  },
  {
    path: 'laporan-saya',
    loadComponent: () => import('./verifikasi-alert/laporan-saya').then((m) => m.LaporanSaya),
    canActivate: [authGuard],
  },
  {
    path: 'verifikasi-alert',
    loadComponent: () => import('./verifikasi-alert/daftar-laporan').then((m) => m.DaftarLaporan),
    canActivate: [authGuard],
  },
  {
    path: 'detail-laporan/:id',
    loadComponent: () => import('./verifikasi-alert/detail-laporan').then((m) => m.DetailLaporan),
    canActivate: [authGuard],
  },

  // ── Asesmen Kondisi Bencana, 5 aspek (#19–#29) ──
  {
    path: 'layanan-kritis',
    loadComponent: () =>
      import('./asesmen-bencana/layanan-kritis').then((m) => m.LayananKritisPage),
    canActivate: [authGuard],
  },
  {
    path: 'asesmen-bencana',
    loadComponent: () => import('./asesmen-bencana/form-asesmen').then((m) => m.FormAsesmen),
    canActivate: [authGuard],
  },
  {
    path: 'daftar-asesmen',
    loadComponent: () => import('./asesmen-bencana/daftar-asesmen').then((m) => m.DaftarAsesmen),
    canActivate: [authGuard],
  },
  {
    path: 'detail-asesmen/:id',
    loadComponent: () => import('./asesmen-bencana/detail-asesmen').then((m) => m.DetailAsesmen),
    canActivate: [authGuard],
  },

  // ── Dashboard Monitor SC & Sumber Daya (#30–#35) ──
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./dashboard/dashboard-ringkasan').then((m) => m.DashboardRingkasan),
    canActivate: [authGuard],
  },
  {
    path: 'dashboard-safety-check',
    loadComponent: () =>
      import('./dashboard/dashboard-safety-check').then((m) => m.DashboardSafetyCheck),
    canActivate: [authGuard],
  },
  {
    path: 'dashboard-asesmen-masuk',
    loadComponent: () =>
      import('./dashboard/dashboard-asesmen-masuk').then((m) => m.DashboardAsesmenMasuk),
    canActivate: [authGuard],
  },
  {
    path: 'dashboard-aspek',
    loadComponent: () => import('./dashboard/dashboard-aspek').then((m) => m.DashboardAspek),
    canActivate: [authGuard],
  },
  {
    path: 'dashboard-layanan',
    loadComponent: () => import('./dashboard/dashboard-layanan').then((m) => m.DashboardLayanan),
    canActivate: [authGuard],
  },
  {
    path: 'dashboard-unit/:unitId',
    loadComponent: () => import('./dashboard/dashboard-unit').then((m) => m.DashboardUnit),
    canActivate: [authGuard],
  },

  // ── Notifikasi (#43–#45) ──
  {
    path: 'notifikasi',
    loadComponent: () => import('./notifikasi/notifikasi').then((m) => m.NotifikasiPage),
    canActivate: [authGuard],
  },

  { path: '**', component: HalamanTidakDitemukan },
];
