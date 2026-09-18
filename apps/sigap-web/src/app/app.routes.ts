import { Routes } from '@angular/router';
import { Beranda } from './beranda/beranda';
import { HalamanTidakDitemukan } from './halaman-tidak-ditemukan/halaman-tidak-ditemukan';

// Aturan platform: path RELATIF, flat, DILARANG nested routing (tanpa `children`).
export const routes: Routes = [
  { path: '', component: Beranda },
  { path: '**', component: HalamanTidakDitemukan },
];
