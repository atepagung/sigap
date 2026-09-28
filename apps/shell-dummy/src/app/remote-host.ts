import { Component, ElementRef, afterNextRender, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { loadRemoteModule } from '@angular-architects/native-federation';
import { RemoteRegistration } from '../registry/remote-registry';

/**
 * Memuat remote hanya saat rutenya dibuka (lazy), memanggil fungsi define-nya, lalu memasang
 * custom element-nya. Kalau remote gagal dimuat, hanya area konten yang menampilkan pesan —
 * shell dan modul lain tetap hidup.
 */
@Component({
  selector: 'shell-remote-host',
  template: `
    @if (gagal()) {
      <div class="table-card">
        <p class="table-card__empty">Modul {{ remote.displayName }} sedang tidak dapat dimuat.</p>
      </div>
    }
    <div #wadah></div>
  `,
})
export class RemoteHost {
  protected readonly remote: RemoteRegistration = inject(ActivatedRoute).snapshot.data['remote'];
  protected readonly gagal = signal(false);
  private readonly wadah = viewChild.required<ElementRef<HTMLElement>>('wadah');

  constructor() {
    afterNextRender(() => this.pasang());
  }

  private async pasang(): Promise<void> {
    try {
      const modul = await loadRemoteModule(this.remote.remoteName, this.remote.exposedModule);
      await modul[this.remote.defineFunction]();
      this.wadah().nativeElement.appendChild(document.createElement(this.remote.elementName));
    } catch (err) {
      console.error(`Gagal memuat remote ${this.remote.remoteName}`, err);
      this.gagal.set(true);
    }
  }
}
