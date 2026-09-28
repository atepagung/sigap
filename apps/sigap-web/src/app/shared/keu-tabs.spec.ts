import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { KeuTabComponent, KeuTabsComponent } from '@danarakca/keu-ui';

@Component({
  selector: 'app-tabs-uji',
  imports: [KeuTabsComponent, KeuTabComponent],
  template: `
    <keu-tabs>
      <keu-tab label="Satu" [count]="1"><p class="isi">isi satu</p></keu-tab>
      <keu-tab label="Dua" [count]="2"><p class="isi">isi dua</p></keu-tab>
    </keu-tabs>
  `,
})
class TabsUji {}

describe('KeuTabs (kontrak yang dipakai rekap safety check)', () => {
  async function render() {
    const fixture = TestBed.createComponent(TabsUji);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture;
  }

  const isi = (root: HTMLElement) =>
    [...root.querySelectorAll('.isi')].map((e) => e.textContent?.trim());

  it('tab pertama aktif sejak awal dan hanya panelnya yang ada', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    expect(isi(el)).toEqual(['isi satu']);
    expect(el.querySelectorAll('[role=tab]')[0].getAttribute('aria-selected')).toBe('true');
  });

  it('mengklik tab lain mengganti ISI panel, bukan hanya penanda header (aplikasi zoneless)', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    (el.querySelectorAll('[role=tab]')[1] as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(el.querySelectorAll('[role=tab]')[1].getAttribute('aria-selected')).toBe('true');
    expect(el.querySelectorAll('[role=tab]')[0].getAttribute('aria-selected')).toBe('false');
    expect(isi(el)).toEqual(['isi dua']);
  });
});
