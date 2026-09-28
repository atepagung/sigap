import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HasPermissionDirective, IamPermissions, provideIamPermissions } from '@danarakca/iam';

@Component({
  imports: [HasPermissionDirective],
  template: `
    <button id="verifikasi" *hasPermission="'sigap:laporan:verify'">Verifikasi</button>
    <button id="picu" *hasPermission="'sigap:broadcast:trigger'">Picu</button>
  `,
})
class Halaman {}

async function render(load: () => Promise<readonly string[]>) {
  TestBed.configureTestingModule({ providers: [provideIamPermissions(load)] });
  const fixture = TestBed.createComponent(Halaman);
  await fixture.whenStable();
  const ada = (id: string) => fixture.nativeElement.querySelector(`#${id}`) !== null;
  return { fixture, ada };
}

describe('*hasPermission', () => {
  it('menampilkan elemen yang permission-nya dipegang, menyembunyikan yang lain', async () => {
    const { ada } = await render(() => Promise.resolve(['sigap:laporan:verify']));
    expect(ada('verifikasi')).toBe(true);
    expect(ada('picu')).toBe(false);
  });

  it('menyembunyikan semua saat daftar belum tiba, lalu menampilkan begitu tiba', async () => {
    let kirim!: (list: readonly string[]) => void;
    const { fixture, ada } = await render(() => new Promise((resolve) => (kirim = resolve)));
    expect(ada('verifikasi')).toBe(false);

    kirim(['sigap:laporan:verify']);
    await Promise.resolve(); // biarkan .then di provideIamPermissions mengisi daftar
    await fixture.whenStable();
    expect(ada('verifikasi')).toBe(true);
  });

  it('menyembunyikan lagi saat permission dicabut', async () => {
    const { fixture, ada } = await render(() => Promise.resolve(['sigap:laporan:verify']));
    TestBed.inject(IamPermissions).replace([]);
    await fixture.whenStable();
    expect(ada('verifikasi')).toBe(false);
  });

  it('gagal memuat berarti semua tersembunyi (fail-closed)', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const { ada } = await render(() => Promise.reject(new Error('jaringan putus')));
    expect(ada('verifikasi')).toBe(false);
    expect(ada('picu')).toBe(false);
  });
});
