import { Component, inject } from '@angular/core';
import { NavigationEnd, NavigationStart, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ShellAuth } from '../auth/shell-auth';
import { REMOTE_REGISTRY } from '../registry/remote-registry';

/**
 * Shell dummy: HANYA sidebar, otentikasi, dan routing. Dilarang ada logika bisnis di sini.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  styleUrl: './app.scss',
  template: `
    <div class="shell">
      <aside class="shell__sidebar">
        <div class="shell__brand">ICS Keuangan</div>
        <nav class="shell__nav">
          <a
            class="shell__link"
            routerLink="/"
            routerLinkActive="shell__link--active"
            [routerLinkActiveOptions]="{ exact: true }"
          >Lobi</a>
          @for (remote of remotes; track remote.remoteName) {
            <a class="shell__link" [routerLink]="remote.routePath" routerLinkActive="shell__link--active">{{
              remote.displayName
            }}</a>
          }
        </nav>
      </aside>
      <div class="shell__main">
        <header class="shell__topbar">
          <span class="status-badge status-badge--warning">Shell dummy — bukan shell ICS asli</span>
          @if (auth.pengguna(); as pengguna) {
            <span>{{ pengguna.nama }}</span>
          } @else {
            <span class="shell__muted">SSO belum terpasang (P3.4)</span>
          }
        </header>
        <main class="shell__content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class App {
  protected readonly remotes = REMOTE_REGISTRY;
  protected readonly auth = inject(ShellAuth);

  constructor() {
    this.kabarkanNavigasiKeRemote();
  }

  /**
   * Router remote tidak mendengar pushState dari router shell. Setelah navigasi yang dipicu
   * shell, kirim `popstate` supaya router remote yang aktif menyelaraskan diri dengan URL.
   * [ASUMSI] Mekanisme ini tebakan kita — lihat DUMMY_REGISTRY.md.
   */
  private kabarkanNavigasiKeRemote(): void {
    let dipicuShell = false;
    inject(Router).events.subscribe((event) => {
      if (event instanceof NavigationStart) {
        dipicuShell = event.navigationTrigger === 'imperative';
      } else if (event instanceof NavigationEnd && dipicuShell) {
        window.dispatchEvent(new PopStateEvent('popstate', { state: null }));
      }
    });
  }
}
