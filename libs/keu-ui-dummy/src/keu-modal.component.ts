import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

/**
 * [ASUMSI] Selector, nama kelas, nama @Input/@Output — SELURUHNYA tebakan kita.
 * Paket resmi bisa memakai nama lain. Lihat DUMMY_REGISTRY.md.
 *
 * Komponen ini sengaja TIDAK punya style sendiri: tampilannya sepenuhnya dari
 * katalog global (class .modal-backdrop/.modal). Jadi saat katalog resmi masuk,
 * tampilannya ikut berubah tanpa mengubah komponen.
 *
 * Dipakai SIGAP untuk popup broadcast safety check dan dialog konfirmasi.
 */
@Component({
  selector: 'keu-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="modal-backdrop" *ngIf="open" (click)="onBackdrop()">
      <div
        class="modal"
        [class.modal--wide]="wide"
        role="dialog"
        aria-modal="true"
        (click)="$event.stopPropagation()"
      >
        <div class="modal__header">
          <h2 class="modal__title">{{ title }}</h2>
          <button
            *ngIf="dismissible"
            type="button"
            class="button button--ghost"
            aria-label="Tutup"
            (click)="closed.emit()"
          >
            &times;
          </button>
        </div>
        <div class="modal__body">
          <ng-content></ng-content>
        </div>
        <div class="modal__footer">
          <ng-content select="[keuModalFooter]"></ng-content>
        </div>
      </div>
    </div>
  `,
})
export class KeuModalComponent {
  @Input() open = false;
  @Input() title = '';
  @Input() wide = false;

  /**
   * Bila false, modal tidak bisa ditutup pengguna — dipakai broadcast safety
   * check yang wajib dijawab.
   */
  @Input() dismissible = true;

  @Output() closed = new EventEmitter<void>();

  onBackdrop(): void {
    if (this.dismissible) {
      this.closed.emit();
    }
  }
}
