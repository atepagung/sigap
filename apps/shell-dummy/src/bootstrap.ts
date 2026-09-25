import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

bootstrapApplication(App, appConfig).catch((err) => console.error(err));

// Service worker Web Push dimiliki shell, bukan remote: harus satu origin dengan halaman (DUMMY_REGISTRY
// bagian 6.2 butir 103). Gagal mendaftar tidak boleh menghalangi shell; remote menampilkan
// "service worker belum tersedia" bila tidak ada.
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.register('/sw.js').catch((err: unknown) => console.warn('sw.js tidak terdaftar', err));
}
