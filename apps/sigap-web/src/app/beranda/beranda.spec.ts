import { flushAsync } from '../shared/testing/flush-async';
import { provideHttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideIamPermissions } from '@danarakca/iam';
import { Beranda } from './beranda';
import { TokenProvider } from '../core/auth/token-provider';

@Component({ selector: 'app-halaman-kosong-uji', template: '' })
class HalamanKosong {}

async function render(permissions: readonly string[]) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideRouter([{ path: 'masuk', component: HalamanKosong }]),
      provideIamPermissions(() => Promise.resolve(permissions)),
    ],
  });
  const fixture = TestBed.createComponent(Beranda);
  fixture.detectChanges();
  await flushAsync();
  fixture.detectChanges();
  return fixture;
}

describe('Beranda', () => {
  it('hanya menampilkan tautan sesuai permission yang dipegang', async () => {
    const fixture = await render(['sigap:monitor:read']);
    const teks = fixture.nativeElement.textContent as string;

    expect(teks).toContain('Buka Dashboard');
    expect(teks).not.toContain('Laporkan Potensi Bencana');
    expect(teks).not.toContain('Kirim/Perbarui Asesmen');
  });

  it('kartu tanpa satu pun tombol yang boleh dilihat punya wadah kosong (CSS menyembunyikannya)', async () => {
    const fixture = await render(['sigap:monitor:read']);
    const kartu = [...fixture.nativeElement.querySelectorAll('.beranda__kartu')] as HTMLElement[];
    const kosong = (k: HTMLElement) => k.querySelector('.beranda__isi')?.matches(':empty') ?? false;

    expect(kartu.length).toBe(5);
    expect(kartu.filter((k) => !kosong(k)).map((k) => k.querySelector('h2')?.textContent)).toEqual([
      'Dashboard Monitor SC & Sumber Daya',
    ]);
  });

  it('tanpa permission apa pun semua kartu kosong', async () => {
    const fixture = await render([]);
    const isi = [...fixture.nativeElement.querySelectorAll('.beranda__isi')] as HTMLElement[];

    expect(isi.length).toBe(5);
    expect(isi.every((e) => e.matches(':empty'))).toBe(true);
  });

  it('Keluar mengosongkan token dan mengarahkan ke /masuk', async () => {
    const fixture = await render([]);
    const tokenProvider = TestBed.inject(TokenProvider);
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    const tombol = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Keluar'),
    ) as HTMLButtonElement;
    tombol.click();
    await flushAsync();

    expect(tokenProvider.sudahMasuk).toBe(false);
    expect(navigateSpy).toHaveBeenCalledWith(['/masuk']);
  });
});
