import { flushAsync } from '../shared/testing/flush-async';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { IamPermissions } from '@danarakca/iam';
import { KonteksSaya } from '../core/auth/konteks-saya.model';
import { KonteksSayaService } from '../core/auth/konteks-saya.service';
import { SesiPengguna } from '../core/auth/sesi-pengguna';
import { TokenProvider } from '../core/auth/token-provider';
import { Masuk } from './masuk';

@Component({ selector: 'app-halaman-kosong-uji', template: '' })
class HalamanKosong {}

const KONTEKS: KonteksSaya = {
  pengguna: { id: 'p1', nip: '900000000000000002', nama: null },
  unit: null,
  peran: ['SATGAS'],
  permission: ['sigap:laporan:verify'],
  lingkup: { jenis: 'UNIT', label: null, catatan: null },
};

async function render(tokenProvider: Pick<TokenProvider, 'masukAsync'>) {
  const konteksSaya = { bacaAsync: vi.fn().mockResolvedValue(KONTEKS) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: '', component: HalamanKosong }]),
      { provide: TokenProvider, useValue: tokenProvider },
      { provide: KonteksSayaService, useValue: konteksSaya },
    ],
  });
  const fixture = TestBed.createComponent(Masuk);
  fixture.detectChanges();

  // Field NIP/kata sandi bukan signal (dua arah lewat ngModel) — diisi langsung supaya tes tidak
  // bergantung pada urutan mikrotugas event DOM sintetik, `any` diizinkan khusus *.spec.ts.
  const instance = fixture.componentInstance as any;
  instance.nip = '900000000000000002';
  instance.kataSandi = 'rahasia';
  fixture.detectChanges();
  return { fixture, konteksSaya };
}

describe('Masuk', () => {
  it('login berhasil mengisi sesi, permission, lalu pindah ke beranda', async () => {
    const tokenProvider = { masukAsync: vi.fn().mockResolvedValue(true) };
    const { fixture } = await render(tokenProvider);
    const sesi = TestBed.inject(SesiPengguna);
    const permissions = TestBed.inject(IamPermissions);
    const permissionsSpy = vi.spyOn(permissions, 'replace');
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigate');

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await flushAsync();

    expect(tokenProvider.masukAsync).toHaveBeenCalledWith('900000000000000002', 'rahasia');
    expect(sesi.konteks()).toEqual(KONTEKS);
    expect(permissionsSpy).toHaveBeenCalledWith(['sigap:laporan:verify']);
    expect(navigateSpy).toHaveBeenCalledWith(['/']);
  });

  it('login ditolak menampilkan galat, tanpa mengubah sesi', async () => {
    const tokenProvider = { masukAsync: vi.fn().mockResolvedValue(false) };
    const { fixture, konteksSaya } = await render(tokenProvider);

    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    await flushAsync();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('ditolak');
    expect(konteksSaya.bacaAsync).not.toHaveBeenCalled();
  });
});
