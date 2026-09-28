import { isWithinBasePath } from './base-path';

describe('isWithinBasePath', () => {
  it('menerima akar dan sub-path remote', () => {
    expect(isWithinBasePath('/modul-contoh', '/modul-contoh')).toBe(true);
    expect(isWithinBasePath('/modul-contoh/', '/modul-contoh')).toBe(true);
    expect(isWithinBasePath('/modul-contoh/halaman', '/modul-contoh/')).toBe(true);
  });

  it('menolak URL milik shell atau modul lain', () => {
    expect(isWithinBasePath('/', '/modul-contoh')).toBe(false);
    expect(isWithinBasePath('/user-management', '/modul-contoh')).toBe(false);
  });

  it('menolak modul lain yang namanya berawalan sama', () => {
    expect(isWithinBasePath('/modul-contoh-lama', '/modul-contoh')).toBe(false);
  });
});
