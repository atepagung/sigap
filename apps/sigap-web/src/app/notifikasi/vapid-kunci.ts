/**
 * Kunci publik VAPID base64url -> `applicationServerKey`. Kunci sah berupa titik P-256 tak terkompresi:
 * tepat 65 byte dan berawalan 0x04. Yang lain ditolak di sini dengan pesan jelas, karena `subscribe`
 * hanya menjawab "AbortError" tanpa sebab bila kuncinya salah.
 */
export function kunciAplikasiDari(base64url: string): Uint8Array<ArrayBuffer> {
  const biasa = base64url.trim().replace(/-/g, '+').replace(/_/g, '/');
  if (biasa.length === 0 || !/^[A-Za-z0-9+/]*={0,2}$/.test(biasa)) {
    throw new Error('Kunci VAPID bukan base64url yang sah.');
  }

  const teks = atob(biasa + '='.repeat((4 - (biasa.length % 4)) % 4));
  const byte = new Uint8Array(new ArrayBuffer(teks.length));
  for (let i = 0; i < teks.length; i++) {
    byte[i] = teks.charCodeAt(i);
  }

  if (byte.length !== 65 || byte[0] !== 0x04) {
    throw new Error('Kunci VAPID harus titik P-256 tak terkompresi (65 byte, berawalan 0x04).');
  }

  return byte;
}
