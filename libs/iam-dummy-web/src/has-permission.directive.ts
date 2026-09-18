import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { IamPermissions } from './iam-permissions';

/**
 * Menampilkan elemen hanya bila pengguna memegang permission yang diminta.
 *
 *     <button *hasPermission="'sigap:laporan:verify'">Verifikasi</button>
 *
 * Nama directive `*hasPermission` berasal dari slide arsitektur ICS. [ASUMSI] bentuk
 * argumennya (satu string `app:resource:action`), nama kelas, dan paket asalnya.
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  readonly hasPermission = input.required<string>();

  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);
  private readonly permissions = inject(IamPermissions);
  private shown = false;

  constructor() {
    effect(() => {
      const allowed = this.permissions.has(this.hasPermission());
      if (allowed && !this.shown) {
        this.container.createEmbeddedView(this.template);
        this.shown = true;
      } else if (!allowed && this.shown) {
        this.container.clear();
        this.shown = false;
      }
    });
  }
}
