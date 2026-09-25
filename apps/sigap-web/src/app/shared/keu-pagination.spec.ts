import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { KeuPaginationComponent } from '@danarakca/keu-ui';

@Component({
  selector: 'app-paginasi-uji',
  imports: [KeuPaginationComponent],
  template: `
    <keu-pagination
      [halaman]="halaman()"
      [ukuran]="ukuran()"
      [total]="total()"
      (halamanBerubah)="diminta.push($event)"
    />
  `,
})
class PaginasiUji {
  readonly halaman = signal(1);
  readonly ukuran = signal(20);
  readonly total = signal(45);
  readonly diminta: number[] = [];
}

describe('KeuPagination (kontrak yang dipakai daftar berhalaman)', () => {
  function render() {
    const fixture = TestBed.createComponent(PaginasiUji);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const tombol = (teks: string) =>
      [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === teks)!;
    return { fixture, el, tombol };
  }

  it('menampilkan rentang, posisi, dan jumlah halaman dari amplop koleksi', () => {
    const { el } = render();

    expect(el.textContent).toContain('1–20 dari 45');
    expect(el.textContent).toContain('Halaman 1 dari 3');
  });

  it('halaman terakhir menampilkan rentang yang dipotong oleh total', () => {
    const { fixture, el } = render();
    fixture.componentInstance.halaman.set(3);
    fixture.detectChanges();

    expect(el.textContent).toContain('41–45 dari 45');
  });

  it('di halaman pertama Sebelumnya mati, di halaman terakhir Berikutnya mati', () => {
    const { fixture, tombol } = render();
    expect(tombol('Sebelumnya').disabled).toBe(true);
    expect(tombol('Berikutnya').disabled).toBe(false);

    fixture.componentInstance.halaman.set(3);
    fixture.detectChanges();

    expect(tombol('Sebelumnya').disabled).toBe(false);
    expect(tombol('Berikutnya').disabled).toBe(true);
  });

  it('menekan tombol memberi tahu nomor halaman yang diminta, tanpa mengubah halamannya sendiri', () => {
    const { fixture, el, tombol } = render();
    fixture.componentInstance.halaman.set(2);
    fixture.detectChanges();

    tombol('Berikutnya').click();
    tombol('Sebelumnya').click();
    fixture.detectChanges();

    expect(fixture.componentInstance.diminta).toEqual([3, 1]);
    expect(el.textContent).toContain('Halaman 2 dari 3');
  });

  it('tidak menampilkan apa pun bila tidak ada data', () => {
    const { fixture, el } = render();
    fixture.componentInstance.total.set(0);
    fixture.detectChanges();

    expect(el.querySelector('nav')).toBeNull();
  });

  it('ukuran 0 atau negatif tidak membagi dengan nol: satu halaman', () => {
    const { fixture, el } = render();
    fixture.componentInstance.ukuran.set(0);
    fixture.detectChanges();

    expect(el.textContent).toContain('Halaman 1 dari 1');
  });
});
