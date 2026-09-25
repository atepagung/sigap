import { kunciAplikasiDari } from './vapid-kunci';
import { KUNCI_UJI } from '../shared/testing/kunci-vapid-uji';

describe('kunciAplikasiDari', () => {
  it('mengubah base64url menjadi 65 byte berawalan 0x04', () => {
    const byte = kunciAplikasiDari(KUNCI_UJI);

    expect(byte.length).toBe(65);
    expect(byte[0]).toBe(0x04);
  });

  it('menerima spasi di tepi dan mengembalikan byte yang sama', () => {
    expect(kunciAplikasiDari(`  ${KUNCI_UJI}\n`)).toEqual(kunciAplikasiDari(KUNCI_UJI));
  });

  it('menolak teks kosong dan karakter di luar base64url', () => {
    expect(() => kunciAplikasiDari('')).toThrow('base64url');
    expect(() => kunciAplikasiDari('bukan kunci!')).toThrow('base64url');
  });

  it('menolak panjang yang salah dan awalan selain 0x04', () => {
    expect(() => kunciAplikasiDari('AAAA')).toThrow('65 byte');
    const tanpaAwalan = btoa(String.fromCharCode(...new Uint8Array(65)))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    expect(() => kunciAplikasiDari(tanpaAwalan)).toThrow('0x04');
  });
});
