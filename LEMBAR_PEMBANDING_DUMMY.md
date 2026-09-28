# LEMBAR_PEMBANDING_DUMMY

Bahan langkah 1 penukaran dummy (PLAYBOOK Fase 7): **permukaan kontrak setiap dummy yang benar-benar
dipakai aplikasi**, supaya saat paket atau dokumentasi platform asli tiba, pembandingan cukup mengisi kolom
"Asli" — bukan menelusuri ulang kode. Disusun 28 Sep 2026 dari kode, bukan dari ingatan; angka "berkas"
dihitung dari `src/` aplikasi (tanpa tes) dan wajib dihitung ulang sesaat sebelum penukaran.

Asumsi lengkap dan pertanyaan ke BaTII tetap di [DUMMY_REGISTRY.md](DUMMY_REGISTRY.md); berkas ini hanya
memetakan **jangkauan** tiap asumsi di kode.

**Asal** · `RESMI` = tertulis di dokumen platform (slide Standar Arsitektur ICS) · `ASUMSI` = tebakan yang
tercatat di DUMMY_REGISTRY · `EKSTENSI` = ditambahkan ke dummy demi kebutuhan aplikasi, tidak berasal dari
dokumen mana pun — **paling mungkin tidak ada di paket asli**.

---

## 0. Cara memakai saat paket asli tiba

1. Isi kolom **Asli** tiap baris dari dokumentasi/paket. Baris yang sama persis cukup diberi ✔.
2. Jumlahkan kolom **Berkas** untuk baris yang berbeda. Lebih dari ±10 berkas, atau menyentuh baris
   `EKSTENSI` yang tidak punya padanan, = **berhenti dan laporkan dulu** (aturan prompt P7).
3. Baru setelah itu pasang paket, cabut alias/registrasi, dan jalankan seluruh tes. Kolom **Diandalkan**
   menyebut tes yang akan menangkap perilaku yang meleset.

---

## 1. Ringkasan jangkauan

| Dummy | Menggantikan | Permukaan yang dipakai | Jangkauan bila meleset | Kunci penukaran |
| --- | --- | --- | --- | --- |
| `libs/iam-dummy` | `iam.plugin` | 12 tipe publik | **terbesar**: `ICurrentUserContext` 29 berkas, `DataScope` 27; isi `DataScope` (profil domain) **9 berkas** | `ProjectReference` di `Sigap.Application.csproj` + `AddKemenkeuIam` di `Program.cs` |
| `libs/iam-dummy-web` | `*hasPermission` | 3 ekspor | 12 template; pemuat permission 1 berkas | alias `@danarakca/iam` di `tsconfig.base.json` |
| `libs/keu-ui-dummy` | `@danarakca/keu-ui` | 29 dari 73 kelas katalog, 2 dari 47 token, 4 komponen Angular | nama elemen BEM: hingga **24 template**; komponen 8 berkas; token 5 berkas | alias + `styles`/`includePaths` di `angular.json` |
| Keycloak lokal | SSO Kemenkeu | issuer, audience, klaim `nip`/`groups` | **0 berkas aplikasi** — klaim hanya dibaca `iam.plugin` | konfigurasi `Iam:Authority`/`Iam:Audience` |
| `apps/shell-dummy` + `src/federation/` | shell ICS + `starter.mfe` | custom element, Silent Location Strategy, `REMOTE_IDENTITY` | kode fitur: 1 berkas (`REMOTE_IDENTITY`); seam token **5 berkas + halaman `masuk`** | `src/federation/` diganti utuh (P7.2) |
| `remote-identity.json` | identitas dari BaTII | 1 berkas | 1 berkas (turunan di-generate) | berkas itu sendiri |
| `libs/notifikasi-dummy` | — (bukan tiruan platform) | `KanalLog` (DEBUG) + test double | 0 — port sudah ditukar (P5.3) | `ProjectReference` ber-`Condition` Debug |
| `infra/kantor-bmn-seed` | koordinat SIMAN | data, bukan kode | 0 berkas kode | jalankan ulang seeder |

---

## 2. `libs/iam-dummy` → `iam.plugin`

| Nama | Asal | Berkas | Diandalkan (perilaku + penjaga) | Asli | Selisih/tindakan |
| --- | --- | --- | --- | --- | --- |
| `[KemenkeuAuthorize("app:resource:action")]` | `RESMI` (namespace `ASUMSI`) | 11 | tanpa token 401, tanpa permission 403 `problem+json` — `ProteksiEndpointTests` | | |
| `ICurrentUserContext` (`Nip`, `UserId`, `UnitId`, `Provinsi`, `EselonIKey`, `Roles`, `Permissions`, `IsAuthenticated`) | `ASUMSI` (DR 61) | 29 | `UserId`/`UnitId` `null` bila tak ada di `"User"` → 403 (`IdentitasPemanggil`); `Roles` hanya untuk tampilan `/me` dan jejak audit | | |
| `.HasPermission(p)` | `ASUMSI` (DR 61) | 1 | `BacaPeringatan` memilih jenis peringatan | | |
| `.GetScope(p)` → `DataScope` | `ASUMSI` (DR 62) | 21 | lingkup dari peran yang **memberi** permission itu — `Izin*Tests`, `Keberadaan*Tests` | | |
| `query.ApplyScope(scope, unit:, owner:)` | `ASUMSI` (DR 62) | 9 | di klausa WHERE, `= ANY (@UnitIds)`; OR antar grant; profil domain dilewati; kosong = tanpa baris — tes `App.Sql` | | |
| `DataScope.Grants` → `ScopeGrant(Role, Profile, Area, IsGeneric, Note)`, `ScopeArea(IsNational, UnitIds, OwnerUserId)` | `ASUMSI` (DR 65, 106) | **9** | profil domain disusun aplikasi: `TERSENTUH` (`BroadcastStore`), `SASARAN_SAYA` (`LingkupSafetyCheck`), `PEMICU_ATAU_MENCAKUP` (`AkhiriBroadcast`), kandidat trigger (`PembangunSasaran`), label lingkup (`KonteksSaya`, `BacaMonitor`) | | Bila plugin tidak membuka isi grant: **berhenti** — kesembilan berkas dirancang ulang |
| `DataScope.Terluas()` | **`EKSTENSI`** (DR 106) | 5 | grant generik terluas untuk trigger/pratinjau (#12/#13) dan peringatan gempa nasional | | Tanpa padanan: pindahkan ke aplikasi (fungsi atas `Grants`) |
| `DataScope.IsEmpty` | `ASUMSI` | 0 | — | | |
| `[Sieve("kunci")]` | konsep `RESMI` (nama `ASUMSI`) | 2 | nilai `null`, properti tetap ada — `RekapSafetyCheckApiTests`, `BacaAsesmenTests`, `MonitorTests` | | |
| `IOrganizationResolver` / `UserOrganization` (diimplementasikan aplikasi) | `ASUMSI` (DR 68) | 2 | data organisasi dari `"User"`/`"Unit"` | | Bila platform menyediakan: cabut `OrganisasiDariTabelUserUnit` |
| `IServiceIdentity.AssumeAsync(nip)` | **`EKSTENSI`** (DR 102) | 1 | worker BMKG berjalan atas nama akun layanan (ACCESS_RULES A11) | | Bergantung jawaban identitas layanan |
| `AddKemenkeuIam(configuration)` + bagian `Iam:*` | `ASUMSI` | 1 | satu baris di `Program.cs` | | |
| Berkas kebijakan `iam-policy.sigap.json` (format, profil `generik`/`domain`, `sieve`) | `ASUMSI` (DR 72) | — | `IzinTests` (policy = `Izin.cs` = PERMISSION_MAP) | | Bila kebijakan didaftarkan di platform: pindahkan isinya, bukan formatnya |

Tes yang ikut bergantung pada `Kemenkeu.Iam`: 5 berkas di `apps/sigap-api/tests` (fikstur identitas, termasuk
`IdentitasUji` dan pengait `LingkupDikosongkan`).

## 3. `libs/iam-dummy-web` → `@danarakca/iam`

| Nama | Asal | Berkas | Diandalkan | Asli | Selisih/tindakan |
| --- | --- | --- | --- | --- | --- |
| Directive `*hasPermission="'sigap:…'"` | `RESMI` (paket asal `ASUMSI` DR 75, bentuk argumen DR 76) | 12 template, 11 impor `HasPermissionDirective` | satu string permission, sama dengan PERMISSION_MAP; hanya tampilan | | |
| `provideIamPermissions(load)` / `IamPermissions` | `ASUMSI` (DR 77) | 1 | permission dimuat dari `GET /me/konteks` | | Cara plugin mengetahui permission pengguna belum diketahui |

## 4. `libs/keu-ui-dummy` → `@danarakca/keu-ui`

| Nama | Asal | Berkas | Asli | Selisih/tindakan |
| --- | --- | --- | --- | --- |
| Blok `.page-header`, `.stats-row`, `.table-card` | `RESMI` | 24, 7, 23 | | |
| Elemen `page-header__title` / `__subtitle` / `__actions` | `ASUMSI` (DR 12) | 24 / 13 / 7 | | **Jangkauan terluas** — layak ★ di DUMMY_REGISTRY |
| Elemen `table-card__empty` / `__header` / `__title` / `__toolbar` | `ASUMSI` (DR 15) | 16 / 14 / 14 / 3 | | idem |
| `.button`, `.status-badge` (+ varian), `.form-field` (+ elemen), `.stat-card` (+ elemen) | `ASUMSI` (DR 6–8, 11) | 17, 11, 7, 7 | | |
| `.tabs`, `.modal`, `.pagination`, `.bar-chart` | `ASUMSI` (DR 9, 10, 104, 105) | 0 langsung — hanya lewat komponen di bawah | | Ikut komponennya |
| `KeuModalComponent` | `ASUMSI` (DR 33) | 2 | | |
| `KeuTabsComponent` / `KeuTabComponent` | `ASUMSI` (DR 34, 35) | 1 | | `active` sebagai signal — bug zoneless 24 Sep 2026 (`shared/keu-tabs.spec.ts`) |
| `KeuPaginationComponent` | `ASUMSI`, dummy keputusan pemilik (DR 104) | 4 | | Belum ada di katalog resmi |
| `KeuBarChartComponent` / `KeuBarChartItem` | `ASUMSI`, dummy keputusan pemilik (DR 105) | 1 | | Belum ada di katalog resmi |
| `@use 'index' as *` | `RESMI` | 5 | | |
| Token `$space-md`, `$space-sm` (45 token lain tidak dipakai) | nama `ASUMSI` | 5 | | Warna resmi (`#003d7a`, `#275EA8`, `#FCB332`) tidak dipakai langsung |
| Mixin aplikasi `shared/styles/_form-card.scss` | — | 0 (tak terpakai) | | Hapus sebelum penukaran (DEFINITION_OF_DONE bagian 6) |

## 5. Shell, `starter.mfe`, dan penyerahan token

| Nama | Asal | Berkas | Asli | Selisih/tindakan |
| --- | --- | --- | --- | --- |
| Identitas turunan `remoteName` (element, fungsi define, selector, route) | Golden Rule `RESMI`, nilai `ASUMSI` | `remote-identity.json` + 1 berkas fitur (`REMOTE_IDENTITY` di `beranda`) | | |
| Custom element + `defineRemoteElement`, `SilentLocationStrategy`, `APP_BASE_HREF` | konsep `RESMI`, implementasi `ASUMSI` | hanya `src/federation/` (dikarantina) | | Diganti utuh oleh `starter.mfe` (P7.2) |
| Penyerahan token shell → remote | tidak diketahui (DR 51) | **5**: `token-provider`, `auth.interceptor` (`token`), `auth.guard` (`sudahMasuk`), `beranda` dan `penampung-galat` (`keluar()`); plus halaman `masuk` dan rutenya (`masukAsync`) | | Sesi milik shell: arti `keluar()` dan penanganan 401 ikut berubah |
| `API_BASE_URL`, `KEYCLOAK_ISSUER_URL`, `KEYCLOAK_DEV_CLIENT_ID` (token injeksi) | `ASUMSI` | 10 layanan memakai `API_BASE_URL` (nilai saja); dua lainnya hanya seam | | Nilai dari konfigurasi platform |

## 6. SSO (Keycloak lokal)

Kode aplikasi tidak membaca klaim token secara langsung, baik di sigap-api maupun sigap-web (diperiksa 28 Sep
2026) — `nip`, `groups`, issuer, dan audience hanya dibaca `iam.plugin`. Penukaran = `Iam:Authority`,
`Iam:Audience`, dan pemetaan grup→peran di kebijakan (`peran.*.grupSso`). Uji sungguhan:
`infra/keycloak/uji-ujung-ke-ujung.cs`.

---

## 7. Yang ditemukan saat menyusun lembar ini

- `DataScope.Terluas()` dan bentuk `ScopeGrant`/`ScopeArea` dipakai 9 berkas tetapi belum tercatat sebagai
  asumsi tersendiri → DUMMY_REGISTRY bagian 4 butir 106 (ditambahkan 28 Sep 2026).
- Klaim DUMMY_REGISTRY butir 51 ("yang berubah hanya `token-provider` dan `auth.interceptor`") meleset:
  lima berkas plus halaman `masuk` → dibetulkan di butir 51.
- Elemen BEM `page-header__*` dan `table-card__*` adalah asumsi berjangkauan terluas di sisi web, tetapi di
  DUMMY_REGISTRY (butir 12, 15) tidak bertanda ★. Diusulkan dijadikan pertanyaan prioritas ke BaTII.

---

## 8. P7.2 — scaffold `apps/sigap-web` vs `starter.mfe` (sisi kita, 28 Sep 2026)

Disiapkan sebelum template tiba (Lampiran E #1 belum terjawab; tidak ditemukan salinan starter.mfe di mesin
maupun rujukan alamatnya di repo). Saat template tiba: clone ke folder terpisah di luar `apps/`, isi kolom
**starter.mfe**, lalu tunjukkan daftar selisih sebelum eksekusi — sesuai prompt P7.2.

**Aturan keputusan:** bawaannya **ikut starter.mfe** (standar resmi). "Pertahankan" hanya untuk baris yang
kolom *Alasan SIGAP*-nya terisi, dan bentuknya **ditambahkan ke struktur starter.mfe**, bukan menambal
starter.mfe agar mirip struktur kita. Kode fitur dipindahkan ke dalam struktur starter.mfe.

### 8.1 Tujuh aspek

| Aspek | Punya kita (fakta dari kode) | Alasan SIGAP bila dipertahankan | Keputusan bawaan | starter.mfe |
| --- | --- | --- | --- | --- |
| **Struktur folder** | `src/app/` (kode fitur: 8 folder halaman/fitur + `core/`, `shared/`), `src/federation/` (plumbing, dikarantina), `scripts/` (3 skrip `.mjs`), `public/`; berkas di akar: `angular.json`, `federation.config.mjs`, `remote-identity.json`, 4 `tsconfig*`, `eslint.config.mjs`, `stylelint.config.mjs`, `.prettierrc`, `Dockerfile`, `nginx.conf`. Anggota **npm workspaces** monorepo (lockfile di akar, `tsconfig.base.json` di akar) | Keanggotaan workspace dan `tsconfig.base.json` dibutuhkan **selama** `@danarakca/*` masih dummy di `libs/` (alias). Hilang sendiri setelah penukaran design system/IAM | Ikut starter.mfe; pertahankan keanggotaan workspace sampai dummy web ditukar | |
| **Federation** | `@angular-architects/native-federation ~22.1.3` + orchestrator; `federation.config.mjs`: `name` dari `remote-identity.json`, satu `exposes` `./web-components` (custom element), `shareAll` singleton/strict + `@angular/core` `includeSecondaries`, `denseChunking`. `src/federation/`: `defineRemoteElement`, `RemoteEntryComponent`, `SilentLocationStrategy`, `APP_BASE_HREF` = route path, `announceNavigationToShell`, mode mandiri. `main.ts` → `initFederation` → `bootstrap.ts`. Identitas di-generate `scripts/generate-remote-sources.mjs` (sebelum build/test/watch) | — (seluruhnya tiruan kontrak; DUMMY_REGISTRY 1.3 menyatakan diganti utuh) | **Ikut starter.mfe seluruhnya**; `src/federation/` dan `generate-remote-sources.mjs` dibuang. Tes Golden Rule (`remote-identity.spec.ts`) dipertahankan hanya bila starter tidak punya penjaga setara | |
| **Build config** | `angular.json`: builder `native-federation:build` membungkus `@angular/build:application` (`esbuild`); `serve` lewat `scripts/serve.mjs`, port 4299; budgets produksi 500 kB/1 MB, komponen 4/8 kB; `styles` + `includePaths` ke `libs/keu-ui-dummy`; `polyfills` hanya `es-module-shims` (**zoneless**, tanpa `zone.js`); TypeScript `~6.0`; Angular `^22.1` | `styles`/`includePaths` menunjuk dummy — diganti saat penukaran design system (LEMBAR bagian 4), bukan dipertahankan | Ikut starter.mfe (builder, budgets, port, zoneless/zone). Periksa ulang `KeuTabs.active` (signal) bila starter memakai zone | |
| **Dockerfile** | Sejak P7.3 (28 Sep 2026, `[ASUMSI]`): konteks akar repo dengan `COPY` eksplisit, `node:24` → `npm ci` → `build:prod`; runtime `nginx-unprivileged` port 8080, `/healthz`, fallback rute mode mandiri, `config.json` dari environment saat mulai (DUMMY_REGISTRY bagian 9 butir 24–26). sigap-api: target `dev`/`production` | Konfigurasi runtime `config.json` dipertahankan hanya bila starter tidak punya mekanisme setara | **Ikut starter.mfe** (PLAYBOOK P4.8 sudah menyatakannya). `verifikasi-container.mjs` disesuaikan ke Dockerfile baru | |
| **AGENTS.md** | 11 bagian: identitas remote, struktur, routing ICS, styling ICS, keamanan frontend, **scope Fase 1 di UI**, aturan penulisan, **lintas platform**, tabel penegakan mesin, perintah, menambah halaman | Bagian khusus SIGAP tidak ada di template generik: scope Fase 1 dan koreksi stakeholder, aturan `*hasPermission` + PERMISSION_MAP, larangan fitur Fase 2, aturan lintas platform proyek | **Ikut AGENTS.md starter.mfe sebagai dasar**; bagian khusus SIGAP ditambahkan sebagai bagian tersendiri di akhir, tanpa menduplikasi aturan yang sudah ada di template | |
| **Lint setup** | ESLint flat config (`angular-eslint`, `typescript-eslint`, Prettier) + aturan ICS: tanpa hex/`rgb()` di string TS, tanpa `children`/`loadChildren`, impor lewat `@danarakca/*` (bukan jalur dummy), tanpa `document.title`, tanpa `any`, prefiks `app`. Stylelint `standard-scss` + larangan warna (pengecualian satu berkas token). Prettier `endOfLine: lf`. Di akar repo: Husky + lint-staged + commitlint + `periksa-repo` | Aturan-aturan itu **menegakkan standar ICS sendiri** (warna dari token, flat routing) yang tercantum di AGENTS.md; bila starter belum menegakkannya dengan mesin, aturannya ditambahkan ke konfigurasi starter. Aturan impor dummy hilang setelah penukaran | Ikut konfigurasi starter.mfe; tambahkan hanya aturan ICS yang belum ada di sana. Hook akar repo (Husky/lint-staged/commitlint/`periksa-repo`) di luar app, tidak terpengaruh | |
| **Audit trail bawaan** | **Tidak ada di sisi web** (grep 28 Sep 2026). Audit sepenuhnya di sigap-api: interseptor `SaveChanges`, `IJejakAudit`, `CatatAksesFilter` (`DIAKSES`) — DUMMY_REGISTRY bagian 9 butir 8 | — | Ikut starter.mfe bila template membawa mekanisme audit (mis. pencatatan akses halaman ke platform). Periksa tumpang tindih dengan `DIAKSES` di sigap-api supaya satu akses tidak tercatat dua kali dengan makna berbeda | |

### 8.2 Hal lain yang ikut terdampak

| Hal | Punya kita | Catatan untuk perbandingan |
| --- | --- | --- |
| Test runner | `@angular/build:unit-test` + **Vitest 4** + jsdom; 33 berkas spec, 23 memakai `vi.*` (hanya `vi.fn` ×59, `vi.spyOn` ×3) | Bila starter memakai runner lain: konversi mekanis, risiko rendah. Pola `flushAsync()` (`shared/testing`) perlu padanan |
| Penyerahan token | Seam dev `core/auth` (password grant ke Keycloak lokal), dipakai 5 berkas + halaman `masuk` (bagian 5) | Diganti mekanisme starter/shell (Lampiran E #12). `verifikasi-siap-produksi.mjs` gagal selama seam ada |
| Kode fitur yang dipindah | `asesmen-bencana` 6, `beranda` 2, `dashboard` 8, `notifikasi` 6, `safety-check` 11, `verifikasi-alert` 7, `shared` 5 berkas (non-spec); `core/` 11 (auth diganti, `galat`/`referensi`/`models`/`config` dipindah); `halaman-tidak-ditemukan` 1; `masuk` dibuang bersama seam | Dipindah apa adanya ke folder fitur starter; hanya impor jalur yang berubah. `app.routes.ts` (flat) dipindah ke tempat rute starter |
| Konfigurasi aplikasi | `app.config.ts`: `provideBrowserGlobalErrorListeners`, `provideHttpClient(withInterceptors([authInterceptor]))`, `provideRouter(routes)` | Interseptor auth ikut mekanisme token starter |

### 8.3 Urutan eksekusi yang diusulkan (setelah daftar selisih disetujui)

1. Clone starter.mfe ke folder terpisah; jalankan apa adanya (build + tes) untuk memastikan template sehat di Linux.
2. Salin kode fitur (`src/app/<fitur>/`, `shared/`, `core/` tanpa `auth`) ke struktur starter; sesuaikan jalur impor.
3. Pasang identitas remote dari nilai BaTII (Lampiran E #2) lewat mekanisme starter; buang `remote-identity.json` bila tidak dipakai.
4. Tambahkan aturan lint ICS yang belum ada di starter dan bagian khusus SIGAP di AGENTS.md.
5. Gerbang penuh (`check:web`, `verifikasi-linux web`, `verifikasi:container`) + uji peramban per peran seperti 24 Sep 2026.
6. Baru setelah hijau: hapus scaffold lama, perbarui DUMMY_REGISTRY 1.2/1.3 dan LEMBAR bagian 5.
