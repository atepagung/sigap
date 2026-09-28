import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { KeuBarChartComponent, type KeuBarChartItem } from '@danarakca/keu-ui';

@Component({
  selector: 'app-grafik-uji',
  imports: [KeuBarChartComponent],
  template: `<keu-bar-chart judul="Kelengkapan hadir" [data]="data()" />`,
})
class GrafikUji {
  readonly data = signal<readonly KeuBarChartItem[]>([
    { label: 'PENUH_100', nilai: 3 },
    { label: 'SEBAGIAN_75', nilai: 2 },
    { label: 'SEBAGIAN_25', nilai: 0 },
  ]);
}

describe('KeuBarChart (kontrak yang dipakai histogram dashboard)', () => {
  function render() {
    const fixture = TestBed.createComponent(GrafikUji);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const lebar = () =>
      [...el.querySelectorAll<HTMLElement>('.bar-chart__batang')].map((b) => b.style.width);
    return { fixture, el, lebar };
  }

  it('menulis judul, label, dan nilai sebagai teks, bukan hanya panjang batang', () => {
    const { el } = render();
    const teks = el.textContent ?? '';

    expect(teks).toContain('Kelengkapan hadir');
    expect(teks).toContain('PENUH_100');
    expect(el.querySelector('.bar-chart__nilai')?.textContent).toBe('3');
  });

  it('batang terbesar penuh, lainnya sebanding, nol tanpa lebar', () => {
    const { lebar } = render();
    const [penuh, sebagian, nol] = lebar();

    expect(penuh).toBe('100%');
    expect(parseFloat(sebagian)).toBeCloseTo(66.667, 2);
    expect(parseFloat(nol)).toBe(0);
  });

  it('semua nilai nol tidak menghasilkan NaN', () => {
    const { fixture, lebar } = render();
    fixture.componentInstance.data.set([{ label: 'A', nilai: 0 }]);
    fixture.detectChanges();

    expect(lebar().map(parseFloat)).toEqual([0]);
  });

  it('tanpa data menampilkan pesan, bukan diagram kosong', () => {
    const { fixture, el } = render();
    fixture.componentInstance.data.set([]);
    fixture.detectChanges();

    expect(el.textContent).toContain('Belum ada data.');
    expect(el.querySelectorAll('.bar-chart__batang').length).toBe(0);
  });

  it('batang disembunyikan dari pembaca layar; angkanya tetap terbaca lewat teks', () => {
    const { el } = render();

    expect(el.querySelector('.bar-chart__jalur')?.getAttribute('aria-hidden')).toBe('true');
  });
});
