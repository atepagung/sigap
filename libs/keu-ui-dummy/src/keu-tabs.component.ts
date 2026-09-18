import { CommonModule } from '@angular/common';
import {
  AfterContentInit,
  Component,
  ContentChildren,
  Input,
  QueryList,
} from '@angular/core';

/**
 * [ASUMSI] Selector, nama kelas, nama @Input — SELURUHNYA tebakan kita.
 * Lihat DUMMY_REGISTRY.md.
 *
 * Satu panel tab. Dipakai berpasangan dengan <keu-tabs>.
 */
@Component({
  selector: 'keu-tab',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="tabs__panel" *ngIf="active">
      <ng-content></ng-content>
    </div>
  `,
})
export class KeuTabComponent {
  @Input() label = '';

  /** Angka opsional di samping label, mis. jumlah pegawai per kondisi. */
  @Input() count?: number;

  active = false;
}

/**
 * [ASUMSI] Selector dan nama kelas tebakan kita.
 *
 * Tanpa style sendiri — memakai class katalog global (.tabs).
 * Dipakai SIGAP untuk rekap safety check per kondisi (koreksi stakeholder #11:
 * tab per kondisi, bukan list yang di-scroll).
 */
@Component({
  selector: 'keu-tabs',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="tabs">
      <div class="tabs__list" role="tablist">
        <button
          *ngFor="let tab of tabs; let i = index"
          type="button"
          role="tab"
          class="tabs__tab"
          [class.tabs__tab--active]="tab.active"
          [attr.aria-selected]="tab.active"
          (click)="select(i)"
        >
          {{ tab.label }}
          <span class="tabs__count" *ngIf="tab.count !== undefined">{{ tab.count }}</span>
        </button>
      </div>
      <ng-content></ng-content>
    </div>
  `,
})
export class KeuTabsComponent implements AfterContentInit {
  @ContentChildren(KeuTabComponent) tabList?: QueryList<KeuTabComponent>;

  get tabs(): KeuTabComponent[] {
    return this.tabList?.toArray() ?? [];
  }

  ngAfterContentInit(): void {
    this.select(0);
  }

  select(index: number): void {
    this.tabs.forEach((tab, i) => (tab.active = i === index));
  }
}
