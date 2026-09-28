import { APP_BASE_HREF, LocationStrategy } from '@angular/common';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { REMOTE_IDENTITY } from './generated/remote-identity';
import { SilentLocationStrategy } from './silent-location-strategy';

/**
 * Router remote menulis URL browser sendiri (pushState). Router lain di halaman yang sama —
 * router shell — tidak tahu soal itu, jadi setelah navigasi yang dipicu remote kita kirim
 * `popstate` agar router lain menyelaraskan diri. Navigasi yang dipicu `popstate` tidak
 * dikabarkan ulang, sehingga tidak terjadi pantulan bolak-balik.
 *
 * [ASUMSI] Mekanisme kabar-lewat-popstate ini tebakan kita. Lihat DUMMY_REGISTRY.md.
 */
function announceNavigationToShell(): void {
  const router = inject(Router);
  let startedByThisRouter = false;
  router.events.subscribe((event) => {
    if (event instanceof NavigationStart) {
      startedByThisRouter = event.navigationTrigger === 'imperative';
    } else if (event instanceof NavigationEnd && startedByThisRouter) {
      window.dispatchEvent(new PopStateEvent('popstate', { state: null }));
    }
  });
}

/** Provider plumbing yang ditambahkan ke konfigurasi aplikasi remote saat didaftarkan. */
export const remoteFederationConfig: ApplicationConfig = {
  providers: [
    { provide: APP_BASE_HREF, useValue: REMOTE_IDENTITY.routePath },
    { provide: LocationStrategy, useClass: SilentLocationStrategy },
    provideAppInitializer(announceNavigationToShell),
  ],
};
