// Verifikasi realm kemenkeu terhadap Kebutuhan Teknis bagian E. Jalankan dari root: node infra/keycloak/verifikasi.mjs
// Kredensial dibaca dari .env dan TIDAK pernah dicetak; token juga tidak.
import { readFileSync } from 'node:fs';

const env = Object.fromEntries(
  readFileSync(new URL('../../.env', import.meta.url), 'utf8').split(/\r?\n/).filter((l) => l.includes('=') && !l.startsWith('#'))
    .map((l) => [l.slice(0, l.indexOf('=')), l.slice(l.indexOf('=') + 1)]),
);
const ISSUER = 'http://localhost:8081/realms/kemenkeu';
const TOKEN_URL = `${ISSUER}/protocol/openid-connect/token`;
const hasil = [];
const cek = (lulus, pesan) => hasil.push(`${lulus ? 'OK   ' : 'GAGAL'} ${pesan}`);
const payload = (jwt) => JSON.parse(Buffer.from(jwt.split('.')[1], 'base64url').toString());
const form = (o) => new URLSearchParams(o);

const akun = [
  ['900000000000000001', 'sigap-pegawai', 'DEMO-KPP-MADYA-PKU', 'djp'],
  ['900000000000000002', 'sigap-satgas', 'DEMO-KPP-MADYA-PKU', 'djp'],
  ['900000000000000003', 'sigap-pimpinan', 'DEMO-KPP-MADYA-PKU', 'djp'],
  ['900000000000000004', 'sigap-perwakilan', 'DEMO-KANWIL-RIAU', 'setjen'],
  ['900000000000000005', 'sigap-subkoordinator', 'djp', 'djp'],
  ['900000000000000006', 'sigap-koordinator', 'setjen.biro-organisasi-dan-ketatalaksanaan', 'setjen'],
  ['900000000000000007', 'sigap-sekjen', 'setjen', 'setjen'],
  ['900000000000000008', 'sigap-admin', 'DEMO-KANWIL-RIAU', 'setjen'],
  ['900000000000000009', 'sigap-pengembang', 'DEMO-KPP-MADYA-PKU', 'djp'],
  ['900000000000000010', 'sigap-impl-rkb', 'DEMO-KPP-MADYA-PKU', 'djp'],
];

// --- 2. Klaim token kesepuluh akun ---
for (const [nip, grup, satker, eselon1] of akun) {
  const res = await fetch(TOKEN_URL, { method: 'POST', body: form({
    grant_type: 'password', client_id: 'sigap-uji-lokal', username: nip, password: env.SIGAP_UJI_PASSWORD, scope: 'openid' }) });
  const body = await res.json();
  if (!res.ok) { cek(false, `${grup}: token gagal (${body.error})`); continue; }
  const t = payload(body.access_token);
  const aud = [].concat(t.aud);
  const salah = [];
  if (t.nip !== nip) salah.push('nip');
  if (t.preferred_username !== nip) salah.push('preferred_username');
  if (t.kode_satker !== satker) salah.push('kode_satker');
  if (t.kode_eselon1 !== eselon1) salah.push('kode_eselon1');
  if (JSON.stringify(t.groups) !== JSON.stringify([grup])) salah.push(`groups=${JSON.stringify(t.groups)}`);
  if (!aud.includes('sigap-api')) salah.push(`aud=${JSON.stringify(t.aud)}`);
  if (t.exp - t.iat !== 900) salah.push(`umur=${t.exp - t.iat}`);
  if (body.refresh_expires_in !== 28800) salah.push(`refresh=${body.refresh_expires_in}`);
  if (t.iss !== ISSUER) salah.push('iss');
  cek(salah.length === 0, `${grup.padEnd(21)} ${salah.length ? 'SELISIH: ' + salah.join(', ') : `klaim lengkap, aud ${JSON.stringify(aud)}, 900 dtk / refresh 28800 dtk`}`);
}

// Kata sandi salah harus ditolak
{
  const res = await fetch(TOKEN_URL, { method: 'POST', body: form({
    grant_type: 'password', client_id: 'sigap-uji-lokal', username: akun[0][0], password: 'bukan-kata-sandinya' }) });
  cek(res.status === 401, `kata sandi salah ditolak (HTTP ${res.status})`);
}

// --- 3. Perilaku client ---
{
  const res = await fetch(TOKEN_URL, { method: 'POST', body: form({
    grant_type: 'password', client_id: 'sigap-web-dev', username: akun[0][0], password: env.SIGAP_UJI_PASSWORD }) });
  const body = await res.json();
  cek(!res.ok && body.error === 'unauthorized_client', `sigap-web-dev menolak password grant (${body.error})`);
}
const auth = (params) => fetch(`${ISSUER}/protocol/openid-connect/auth?${form({
  client_id: 'sigap-web-dev', response_type: 'code', scope: 'openid', ...params })}`, { redirect: 'manual' });
{
  const res = await auth({ redirect_uri: 'http://localhost:4200/sigap-bencana', code_challenge: 'x'.repeat(43), code_challenge_method: 'S256' });
  const html = await res.text();
  cek(res.status === 200 && html.includes('kc-form-login'), `sigap-web-dev + PKCE + redirect shell 4200 → halaman login (HTTP ${res.status})`);
}
{
  const res = await auth({ redirect_uri: 'http://localhost:4299/sigap-bencana', code_challenge: 'x'.repeat(43), code_challenge_method: 'S256' });
  cek(res.status === 200, `sigap-web-dev redirect remote mandiri 4299 diterima (HTTP ${res.status})`);
}
{
  const res = await auth({ redirect_uri: 'http://localhost:4200/sigap-bencana' });
  const lokasi = res.headers.get('location') ?? '';
  cek(res.status === 302 && lokasi.includes('error=invalid_request') && lokasi.includes('code_challenge'), `sigap-web-dev tanpa PKCE ditolak (${decodeURIComponent(new URL(lokasi).searchParams.get('error_description') ?? '')})`);
}
{
  const res = await auth({ redirect_uri: 'https://situs-lain.invalid/curi', code_challenge: 'x'.repeat(43), code_challenge_method: 'S256' });
  cek(res.status === 400, `sigap-web-dev menolak redirect ke situs lain (HTTP ${res.status})`);
}
{
  const res = await fetch(TOKEN_URL, { method: 'POST', body: form({
    grant_type: 'client_credentials', client_id: 'sigap-api-dev', client_secret: env.SIGAP_API_DEV_CLIENT_SECRET }) });
  const body = await res.json();
  const t = res.ok ? payload(body.access_token) : {};
  cek(res.ok && t.azp === 'sigap-api-dev' && t.nip === undefined, `sigap-api-dev client credentials dengan secret dari .env (azp ${t.azp}, tanpa klaim pegawai)`);
}
{
  const res = await fetch(TOKEN_URL, { method: 'POST', body: form({
    grant_type: 'client_credentials', client_id: 'sigap-api-dev', client_secret: 'secret-salah' }) });
  cek(res.status === 401, `sigap-api-dev dengan secret salah ditolak (HTTP ${res.status})`);
}

console.log(hasil.join('\n'));
console.log(`\n${hasil.filter((h) => h.startsWith('OK')).length}/${hasil.length} lulus`);
