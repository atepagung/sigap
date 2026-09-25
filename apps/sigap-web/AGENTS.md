# AGENTS.md — sigap-web

Dibaca AI coding tool **sebelum menulis kode** di remote MFE ini (Standar Arsitektur ICS
mewajibkan berkas ini pada setiap remote). **Baca dulu [../../AGENTS.md](../../AGENTS.md)** — aturan
mutlak, scope Fase 1, keamanan, dan aturan lintas platform di sana berlaku di sini juga.

Sebagian aturan di bawah **ditegakkan mesin** (ESLint/Stylelint, tabel di bagian 9). Yang ditandai
🔒 gagal di pre-commit dan CI; sisanya bergantung pada Anda.

## 1. Identitas remote

Seluruh identitas diturunkan dari **satu** `remoteName` (Golden Rule platform). Sumber satu-satunya:
[`remote-identity.json`](remote-identity.json). Nilainya **PROVISIONAL** — menunggu BaTII
(PLAYBOOK Lampiran E #2), dan berubah dengan menyunting satu berkas itu saja.

| Turunan | Nilai sekarang |
| --- | --- |
| `remoteName` | `remoteSigapBencana` |
| Element name | `remote-sigap-bencana-element` |
| Fungsi define | `defineRemoteSigapBencanaElement` |
| Selector | `app-remote-sigap-bencana-entry` |
| Route path | `/sigap-bencana` |
| Display name | `SIGAP Bencana` |
| Port lokal | `4299` (shell dummy di `4200`) |

- **Jangan menyalin nilai-nilai ini** ke berkas lain, dan jangan menulis `'/sigap-bencana'` di kode
  fitur. Baca dari `REMOTE_IDENTITY` (`src/federation/generated/remote-identity.ts`).
- Berkas di `src/federation/generated/` **dihasilkan** oleh `scripts/generate-remote-sources.mjs`
  (otomatis sebelum `build`/`test`/`watch`). **Jangan disunting tangan**, tidak masuk git.
- Setelah mengubah `remote-identity.json`: `npm test` — tes Golden Rule memastikan turunannya
  konsisten.

## 2. Struktur dan yang boleh disentuh

```
src/app/          ← KODE FITUR. Di sinilah Anda bekerja.
src/federation/   ← plumbing shell↔remote. DIKARANTINA: jangan diubah untuk fitur.
scripts/          ← skrip build (Node .mjs, lintas platform)
```

`src/federation/` meniru kontrak integrasi platform (custom element, Silent Location Strategy,
`APP_BASE_HREF`) dan **diganti utuh** saat `starter.mfe` resmi tersedia (P7.2). Kode fitur tidak
boleh bergantung pada detailnya. Asumsinya tercatat di DUMMY_REGISTRY bagian 3.

## 3. Routing — Standar ICS

- **Path relatif, flat, DILARANG nested routing.** 🔒 Tanpa `children`, tanpa `loadChildren`.
  Satu halaman = satu entri `{ path, component | loadComponent }` di `app.routes.ts`.
- Kode remote **tidak pernah menulis route path-nya sendiri** (`/sigap-bencana/...`). Tautan ke
  halaman lain di dalam remote memakai path relatif (`routerLink="laporan"`), dan ke akar remote
  `routerLink="/"` milik router remote.
- Remote sinkron dua arah dengan URL browser (Silent Location Strategy, di `src/federation/`).
  Jangan menyentuh `history`/`location` langsung dari kode fitur.
- Path tanpa garis miring di akhir. Jangan mengubah `document.title` 🔒 — itu milik shell (satu-
  satunya pengecualian: `src/federation/standalone.ts`, mode mandiri tanpa shell).
- `**` (halaman tidak ditemukan) tetap entri terakhir.

## 4. Styling — Standar ICS

- **Token lewat `@use 'index' as *`** di setiap `.scss` komponen yang butuh token. Baca
  [`libs/keu-ui-dummy/styles/_tokens.scss`](../../libs/keu-ui-dummy/styles/_tokens.scss) untuk nama
  yang tersedia; jangan menebak. *Nama variabel token masih [ASUMSI] (DUMMY_REGISTRY 2.5).*
- **DILARANG hardcode warna** 🔒: tidak ada hex, `rgb()`, `hsl()`, atau nama warna (`red`) di luar
  berkas token — di `.scss`, `<style>`, atribut `style=""`, maupun string TypeScript. Yang boleh:
  variabel token, `transparent`, `currentcolor`, `inherit`.
- Token resmi platform: **Navy `#003d7a`**, **Blue `#275EA8`**, **Gold `#FCB332`** (`$color-navy`,
  `$color-blue`, `$color-gold`). Perlu warna baru? Itu perubahan **design system** — laporkan, jangan
  menambah warna di aplikasi.
- **Pakai katalog komponen platform**, jangan membuat komponen visual yang menduplikasinya:
  `.page-header`, `.stats-row`, `.table-card` (nama dari slide arsitektur), dan yang diasumsikan:
  `.form-field`, `.button`, `.status-badge`, `.tabs`, `.modal`, `.stat-card`. Komponen Angular
  `KeuModal`, `KeuTabs`, `KeuTab` dari `@danarakca/keu-ui` (namanya juga [ASUMSI]). Perlu sesuatu yang tidak ada di katalog?
  **Laporkan** — jangan membangun sendiri. (Contoh terbuka: **paginasi** belum ada di katalog;
  DUMMY_REGISTRY 2.8. Jangan dibuat sebelum diputuskan.)
- Style terkapsulasi per komponen; jangan `::ng-deep`, jangan `!important`, jangan menimpa class
  katalog dari luar.
- Katalog dimuat **sekali, global** — bukan dari komponen. Jangan `@use` berkas katalog di
  komponen (CSS-nya akan terduplikasi).

## 5. Keamanan di frontend

Keamanan sesungguhnya ada di backend (`[KemenkeuAuthorize]` + Scope + Sieve). Frontend hanya
**menyembunyikan tampilan**.

- Pakai directive **`*hasPermission`** dari `@danarakca/iam`:
  `<button *hasPermission="'sigap:laporan:verify'">`. Satu string permission, sama persis dengan
  `PERMISSION_MAP.md`. **Dilarang** memutuskan tampilan dengan `if (peran === 'SATGAS')`,
  memeriksa `groups` di token, atau menyimpan peran di storage.
- **Tombol yang disembunyikan tetap harus ditolak API** — jangan pernah menganggap
  `*hasPermission` sebagai kontrol akses.
- Menu dan lingkup tampilan dibangun dari `GET /me/konteks` (API_CONTRACT #36: `permission[]`,
  `lingkup`). `lingkup` hanya untuk label; **jangan dipakai menyaring data** di klien.
- Field yang di-Sieve datang sebagai `null`. Tampilkan sebagai "tidak tersedia", jangan sebagai
  error dan jangan mencoba mengambilnya dari endpoint lain.
- **Jangan menulis logika keamanan sendiri:** tidak ada validasi/dekode JWT di klien, tidak ada
  pembuatan header `Authorization` manual. Token dan panggilan API mengikuti mekanisme platform
  (Lampiran E #12 — belum diketahui; jangan mengarang).
- **Dilarang** menaruh data pribadi (NIP, nama, koordinat) di URL, `localStorage`, atau log konsol.

## 6. Scope Fase 1 di UI

- Bangun **hanya** fitur 2.1–2.6 (Safety Check, Laporkan Potensi Bencana, Trigger, Verifikasi,
  Asesmen 5 aspek, Dashboard Monitor) dan 1.1 (Data Bencana Nasional). Peran Fase 2 tidak punya menu.
- **Jangan porting fitur Fase 2** (dokumen MKB, simulasi, RKB, LPKB, rilis komunikasi, AI
  Assistant) dan **jangan menyalin komponen prototipe** yang berlabel Fase 2 di
  [COMPONENT_INVENTORY.md](../../COMPONENT_INVENTORY.md).
- Koreksi stakeholder yang berdampak langsung ke tampilan (MIGRATION_NOTES bagian 2):
  Safety Check hanya dua pilihan **"Saya Aman" / "Butuh Bantuan"**; rekap Safety Check tampil
  sebagai **tab per kondisi** (Aman / Butuh Bantuan / Belum Merespons); tombol asesmen
  **"Kirim"** lalu **"Update Asesmen"**; Pimpinan Satker **tidak** melihat ulang form jenis
  bencana/lokasi; tidak ada menu "Status Aset Unit"; Kepala Perwakilan **punya** tombol trigger
  tingkat wilayah.
- **Banner peringatan wajib** di setiap tampilan peta bila ada gedung `isKoordinatDummy = true`
  (DUMMY_REGISTRY 1.7) — koordinat palsu yang terbaca asli bisa mengirim tim ke lokasi salah.
- **Jangan menaruh logika bisnis di shell** — dan di remote pun, aturan bisnis milik backend.
  Klien tidak menghitung Scope, RTO, atau MMI; ia menampilkan hasil API.

## 7. Aturan penulisan

- Angular standalone, `inject()` untuk DI, signal/`@if`/`@for` (bukan `*ngIf`/`*ngFor`) 🔒.
- Selector komponen berawalan `app-`, atribut directive `app` camelCase 🔒.
- **Kode aplikasi mengimpor lewat nama paket asli** `@danarakca/keu-ui` dan `@danarakca/iam` 🔒 —
  bukan jalur ke `libs/*-dummy`. Itu yang membuat penukaran dummy cukup satu baris di
  `tsconfig.base.json`.
- `any` dilarang 🔒. Jangan menonaktifkan aturan lint dengan komentar tanpa alasan tertulis.
- Komentar menjelaskan *mengapa*. Tandai tebakan tentang platform dengan `[ASUMSI]` dan catat di
  DUMMY_REGISTRY.

## 8. Lintas platform (khusus web)

Aturan umumnya ada di [../../AGENTS.md](../../AGENTS.md) bagian 6. Yang paling relevan di sini:

- **Nama berkas dan impor persis sama, huruf kecil kebab-case** (`halaman-tidak-ditemukan.ts`).
  `import { Beranda } from './Beranda'` untuk `beranda.ts` lulus di Windows dan **gagal di Linux**.
- **Akhir baris LF.** Prettier dikonfigurasi `endOfLine: lf`. Jangan menyalin berkas dari editor
  yang menulis CRLF tanpa menjalankan `npm run format`.
- **Path pakai `/`** di `angular.json`, `tsconfig`, `federation.config.mjs`, dan skrip. Di skrip
  `.mjs`, susun path dengan `node:path` (`join`, `resolve`) — jangan gabung string.
- **Skrip `package.json`:** `rimraf` untuk menghapus, `cross-env` untuk variabel lingkungan.
  **Dilarang** `rm -rf`, `NODE_ENV=production ng build`, `cp`, `mkdir -p`, backslash 🔒
  (`node scripts/periksa-repo.mjs`).
- Jangan menjalankan `npm run build` saat dev server app yang sama masih hidup (berbagi cache).

## 9. Yang ditegakkan mesin

| Aturan | Alat | Berkas |
| --- | --- | --- |
| Tanpa hex/rgb/hsl/warna bernama di luar token (`.scss`, `.html`) | Stylelint | `stylelint.config.mjs` |
| Tanpa hex/rgb/hsl dalam string TypeScript | ESLint `no-restricted-syntax` | `eslint.config.mjs` |
| Tanpa `children` / `loadChildren` | ESLint `no-restricted-syntax` | `eslint.config.mjs` |
| Impor lewat `@danarakca/*`, bukan jalur ke dummy | ESLint `no-restricted-imports` | `eslint.config.mjs` |
| Tanpa `document.title` (kecuali mode mandiri) | ESLint `no-restricted-properties` | `eslint.config.mjs` |
| Kontrol alur `@if`/`@for`, aksesibilitas template | angular-eslint | `eslint.config.mjs` |
| Format | Prettier | `.prettierrc` |
| Skrip npm lintas platform, LF, kapitalisasi | `periksa-repo` | `../../scripts/periksa-repo.mjs` |

`_tokens.scss` di `libs/keu-ui-dummy/styles/` adalah **satu-satunya** berkas yang boleh memuat literal
warna. Saat paket asli `@danarakca/keu-ui` dipasang, token pindah ke `node_modules` dan
pengecualiannya ikut hilang.

Keterbatasan yang disadari: string yang secara sintaks hex sah ikut ditandai, termasuk `#123`.
Tulis rujukan tiket sebagai "issue 123".

## 10. Perintah

```bash
npm run start:web        # remote mandiri di port 4299   (dari root repo)
npm run start:shell      # shell dummy di port 4200, memuat remote ini

npm run check:web        # lint + stylelint + prettier + tes — jalankan sebelum commit
npm run lint:web         # ESLint
npm run lint:styles      # Stylelint
npm run format:web       # Prettier --write
npm run test:web         # Vitest
npm run build:prod --workspace apps/sigap-web
```

Pre-commit (Husky + lint-staged) menjalankan versi cepat atas berkas yang di-stage. CI
(`.github/workflows/sigap-web.yml`, Linux) menjalankan semuanya.

## 11. Menambah halaman

1. Buat `src/app/<nama>/<nama>.ts` (kebab-case, huruf kecil), komponen standalone.
2. Tambah **satu entri flat** di `src/app/app.routes.ts` sebelum `**`. Tanpa `children`.
3. Bungkus tindakan sensitif dengan `*hasPermission="'sigap:…'"` (string dari PERMISSION_MAP).
4. Gaya: `@use 'index' as *` + kelas katalog. Tanpa warna literal.
5. Tes untuk logika yang ada (`*.spec.ts`); `npm run check:web` hijau.
6. Fitur baru dari API_CONTRACT? Cek nomor endpoint-nya, dan bahwa fiturnya Fase 1.
