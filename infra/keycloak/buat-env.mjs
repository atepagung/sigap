// Membuat/melengkapi .env di root repo dengan kredensial LOKAL acak untuk Keycloak dummy.
// Nilai tidak pernah dicetak ke layar dan tidak pernah menimpa nilai yang sudah ada.
// Jalankan dari root repo:  node infra/keycloak/buat-env.mjs
import { randomBytes } from 'node:crypto';
import { existsSync, readFileSync, appendFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ENV = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '.env');
const acak = () => randomBytes(24).toString('base64url');

const WAJIB = {
  KC_BOOTSTRAP_ADMIN_USERNAME: () => 'admin-lokal',
  KC_BOOTSTRAP_ADMIN_PASSWORD: acak,
  SIGAP_UJI_PASSWORD: acak,
  SIGAP_API_DEV_CLIENT_SECRET: acak,
};

const isi = existsSync(ENV) ? readFileSync(ENV, 'utf8') : '';
const sudahAda = new Set(isi.split(/\r?\n/).map((baris) => baris.split('=')[0].trim()).filter(Boolean));
const kurang = Object.keys(WAJIB).filter((kunci) => !sudahAda.has(kunci));

if (kurang.length === 0) {
  console.log('.env sudah lengkap; tidak ada yang diubah.');
} else {
  const awalan = isi === '' ? '# Kredensial LOKAL acak untuk dummy. Jangan di-commit, jangan dipakai di luar mesin ini.\n' : '';
  const pemisah = isi !== '' && !isi.endsWith('\n') ? '\n' : '';
  appendFileSync(ENV, pemisah + awalan + kurang.map((kunci) => `${kunci}=${WAJIB[kunci]()}`).join('\n') + '\n');
  console.log(`Ditambahkan ke .env: ${kurang.join(', ')}`);
}
