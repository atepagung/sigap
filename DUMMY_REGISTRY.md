# DUMMY_REGISTRY

Daftar seluruh komponen dummy yang meniru platform ICS Keuangan, beserta asumsi yang perlu
diverifikasi ke BaTII. Format mengikuti `docs/PLAYBOOK.md` Lampiran D.

**Perbarui berkas ini tiap ada dummy baru atau asumsi baru.** Kolom asumsi adalah isi paling
berharga: itu bahan siap pakai untuk bertanya ke BaTII secara spesifik, bukan sekadar "boleh
minta dokumentasi design system?".

---

## 1. Registry

### 1.1 `libs/keu-ui-dummy`

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `libs/keu-ui-dummy` |
| **Menggantikan** | `@danarakca/keu-ui` |
| **Kontrak ditiru dari** | `docs/Standar-Arsitektur-ICS.pdf` (slide arsitektur) — hanya 3 nama class dan 3 warna yang benar-benar tertulis di sana |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 2 di bawah (38 butir, dikelompokkan) |
| **Pemicu penukaran** | Kredensial registry npm internal |
| **Perkiraan beban** | Sedang — penyesuaian visual per halaman |
| **Status** | **Aktif** (dibuat 18 Sep 2026, P3.1) |
| **Kunci penukaran** | `compilerOptions.paths` di `tsconfig.base.json` (sisi TypeScript) + `styles` & `stylePreprocessorOptions.includePaths` di `angular.json` (sisi SCSS). Langkah lengkap di [libs/keu-ui-dummy/README.md](libs/keu-ui-dummy/README.md) |

### 1.2 `apps/shell-dummy`

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `apps/shell-dummy` |
| **Menggantikan** | Shell ICS Keuangan (`apbn.web`, "Lobi") |
| **Kontrak ditiru dari** | Slide arsitektur ICS: anatomi Lobi/Lantai, Golden Rule penamaan, prinsip perakitan runtime, "Silent Location Strategy" |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 3 di bawah |
| **Pemicu penukaran** | Akses shell ICS (Lampiran E #1) |
| **Perkiraan beban** | Ringan bagi kode aplikasi — shell dummy tinggal dibuang. Yang perlu disesuaikan hanya plumbing di `apps/sigap-web/src/federation/` kalau kontrak integrasinya meleset |
| **Status** | **Aktif** (dibuat 18 Sep 2026, P3.2) |
| **Catatan** | Shell dummy membaca `apps/sigap-web/remote-identity.json` langsung. Ketergantungan lintas-app ini hanya ada di dummy; shell asli mengambil daftar remote dari registry sisi server |

### 1.3 Identitas remote & scaffold `apps/sigap-web`

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `apps/sigap-web/remote-identity.json` (identitas PROVISIONAL) + `apps/sigap-web/src/federation/` (plumbing federation) + scaffold `ng new` |
| **Menggantikan** | `remoteName` & port resmi dari BaTII; template `starter.mfe` |
| **Kontrak ditiru dari** | Golden Rule (slide): `remoteUserManagement` → element, function, selector, route path, display name |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 3 di bawah |
| **Pemicu penukaran** | Identitas: BaTII menetapkan nilai resmi (Lampiran E #2). Plumbing & scaffold: akses `starter.mfe` (Lampiran E #1) |
| **Perkiraan beban** | Identitas: ringan — ubah satu berkas, jalankan `npm test`. Plumbing: sedang — P7.2 |
| **Status** | **Aktif** (dibuat 18 Sep 2026, P3.2) |
| **Kunci penukaran** | Identitas: hanya `remote-identity.json`. Plumbing: seluruh isi `src/federation/` dikarantina dari kode fitur di `src/app/` |

### 1.4 `libs/iam-dummy` (backend)

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `libs/iam-dummy` (proyek `Kemenkeu.Iam.Dummy`, .NET 10) |
| **Menggantikan** | `iam.plugin` — keamanan tiga lapis |
| **Kontrak ditiru dari** | Slide arsitektur ICS: `[KemenkeuAuthorize("app:resource:action")]`, Scope di klausa WHERE, Sieve = field jadi `null`, kebijakan sebagai data. Isi kebijakan dari PERMISSION_MAP.md |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 4 di bawah |
| **Pemicu penukaran** | Kredensial NuGet internal (Lampiran E #3) |
| **Perkiraan beban** | Ringan kalau kontrak dipatuhi — lihat aturan pemakaian di [libs/iam-dummy/README.md](libs/iam-dummy/README.md) |
| **Status** | **Aktif** (dibuat 18 Sep 2026, P3.3) |
| **Kunci penukaran** | Satu `ProjectReference` di csproj sigap-api → `PackageReference` iam.plugin. Plus satu `global using` dan satu baris registrasi di Program.cs bila namanya berbeda |
| **Data kebijakan** | [apps/sigap-api/iam-policy.sigap.json](apps/sigap-api/iam-policy.sigap.json) — **bukan dummy**: ini bahan pendaftaran aturan IAM ke BaTII dan tetap dipakai setelah penukaran (dengan format yang disesuaikan) |

### 1.5 `libs/iam-dummy-web` (frontend)

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `libs/iam-dummy-web` |
| **Menggantikan** | Directive `*hasPermission` platform |
| **Kontrak ditiru dari** | Slide arsitektur ICS: "Gunakan directive *hasPermission pada elemen HTML (Angular)" |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 4.3 di bawah |
| **Pemicu penukaran** | Diketahuinya paket/template asal directive dan cara platform menyerahkan permission ke remote |
| **Perkiraan beban** | Ringan |
| **Status** | **Aktif** (dibuat 18 Sep 2026, P3.3) |
| **Kunci penukaran** | Entri `@danarakca/iam` di `tsconfig.base.json` + satu baris `provideIamPermissions(...)` di `app.config.ts` |

### 1.6 Keycloak lokal (SSO)

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | Layanan `keycloak` di `docker-compose.yml` (profile `sso`, port 8081) + realm `infra/keycloak/import/kemenkeu-realm.json` |
| **Menggantikan** | SSO Kemenkeu (OIDC) |
| **Kontrak ditiru dari** | `docs/Kebutuhan-Teknis-BaTII.pdf` bagian E — pemetaan baris per baris di [infra/keycloak/README.md](infra/keycloak/README.md). Lingkup peran dari PLAYBOOK Lampiran B, **bukan** Lampiran 1 Kebutuhan Teknis yang keliru |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 5 di bawah |
| **Pemicu penukaran** | Client OIDC didaftarkan BaTII (Lampiran E) |
| **Perkiraan beban** | Ringan — ganti `Iam:Authority` (issuer) dan client id di konfigurasi |
| **Status** | **Aktif** (dibuat dan diverifikasi 18 Sep 2026, P3.4): 18/18 pemeriksaan spesifikasi (`infra/keycloak/verifikasi.mjs`) dan 13/13 uji ujung ke ujung token asli → `libs/iam-dummy` (`infra/keycloak/uji-ujung-ke-ujung.cs`) |
| **Kredensial** | Hanya di `.env` (tidak masuk git), dibuat acak oleh `node infra/keycloak/buat-env.mjs`. Realm JSON hanya berisi placeholder `${...}` |

### 1.7 `infra/kantor-bmn-seed` (koordinat KantorBmn)

Berbeda dari entri lain di registry ini: **datanya sendiri asli** (1.431 gedung kantor dari Master
Aset BMN), bukan tiruan platform. Yang dummy hanya **koordinat lintang/bujur**, karena SIMAN belum
menyediakannya.

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `infra/kantor-bmn-seed/` — koordinat gedung, bukan datanya |
| **Menggantikan** | Koordinat resmi dari SIMAN (PLAYBOOK Lampiran E #16) |
| **Kontrak ditiru dari** | Model Prisma `KantorBmn` di prototipe (skema-first, apa adanya) |
| **Asumsi/keputusan yang perlu diverifikasi** | Lihat [infra/kantor-bmn-seed/README.md](infra/kantor-bmn-seed/README.md) bagian "Batas & asumsi" |
| **Pemicu penukaran** | Koordinat gedung kantor tersedia dari SIMAN |
| **Perkiraan beban** | Ringan — jalankan ulang seeder dengan sumber yang sudah berisi koordinat asli; baris berkoordinat asli otomatis `isKoordinatDummy = false` dan tidak digenerate ulang |
| **Status** | **Aktif** (dibuat dan diverifikasi 18 Sep 2026, P3.5): 1.431/1.431 baris tersimpan, 7 tes generator lulus, idempotensi dan tiga penjaga dev-only terbukti lewat pengujian sungguhan, bukan asumsi |
| **[DISETUJUI 18 Sep 2026]** | Kolom baru `"KantorBmn"."isKoordinatDummy"` (boolean) — perubahan struktur tabel di luar 32 tabel prototipe, disetujui pemilik proyek sebelum dikerjakan, sesuai aturan mutlak #5 |
| **Kewajiban P4** | UI peta wajib membaca `isKoordinatDummy` dan menampilkan banner peringatan selama ada baris bertanda begitu — belum bisa diimplementasikan karena UI-nya sendiri belum dibangun |
| **[DIPERBAIKI 21 Sep 2026, P4.2]** | Seeder sempat membuat kolom `"ditarikPada"`/`"createdAt"` bertipe `timestamptz`, menyimpang dari Prisma (`TIMESTAMP(3)`) tanpa persetujuan. `infra/skema/terapkan.mjs` mengembalikannya ke bentuk Prisma dan `schema.sql` seeder dibetulkan; 1.431 baris terbukti cocok 100% dengan sumbernya (hanya mikrodetik yang dibulatkan ke milidetik). Foreign key `"unitId"` → `"Unit"` kini terpasang lewat `infra/skema` |

### 1.8 `libs/notifikasi-dummy`

Berbeda dari entri lain: yang dummy **hanya kanal log dan pengisi port sementara**. Abstraksi
kanal notifikasi beserta kanal `dalam-aplikasi` dan `web-push` ada di `libs/notifikasi` dan
**bukan dummy** — lapisan itu tetap dipakai apa pun jawaban BaTII, karena yang berubah hanya
daftar kanal di konfigurasi. Menaruhnya di `libs/*-dummy` justru akan melanggar aturan dummy
#4, sebab kode itu memang harus ikut naik ke production.

| Kolom | Isi |
| --- | --- |
| **Nama dummy** | `libs/notifikasi-dummy` (proyek `Sigap.Notifikasi.Dummy`, .NET 10) |
| **Menggantikan** | `KanalLog`: tidak meniru apa pun, hanya untuk pengembangan. `CatatanKirimanMemori` → tabel `"KirimanPush"` lewat EF Core (P4.2). `GudangLanggananPushMemori` → tabel `"LanggananPush"` lewat EF Core (P4.2). `PengirimWebPushTiruan` → paket `WebPush` + kunci VAPID (P5.3) |
| **Kontrak ditiru dari** | Tidak ada dokumentasi platform soal notifikasi — itulah sebabnya lapisan ini berupa abstraksi, bukan tiruan. Perilaku push diambil dari prototipe `src/lib/push.ts`; bentuk pemberitahuan dari API_CONTRACT #43 |
| **Asumsi yang perlu diverifikasi** | Lihat bagian 6 di bawah |
| **Pemicu penukaran** | `DbContext` ada (P4.2); jawaban Lampiran E #13 + kunci VAPID (P5.3) |
| **Perkiraan beban** | Ringan — tiga implementasi port, tanpa menyentuh kode fitur |
| **Status** | **Aktif** (dibuat dan diverifikasi 21 Sep 2026, P3.6): 58 tes lulus |
| **Kunci penukaran** | `ProjectReference` ke proyek ini di sigap-api diberi `Condition="'$(Configuration)' == 'Debug'"`, sehingga build Release tidak menyusunnya sama sekali |
| **Tidak ada perubahan skema** | Idempotensi memakai tabel `"KirimanPush"` yang sudah ada di antara 32 tabel. Menyimpan pemberitahuan sebagai baris tersendiri akan menuntut tabel ke-34 dan **tidak dilakukan** — API_CONTRACT #43 menetapkan peringatan dihitung saat diminta |

---

## 2. Asumsi `libs/keu-ui-dummy` yang perlu diverifikasi ke BaTII

Ditulis sebagai pertanyaan siap kirim. Tandai ★ kalau memblokir penukaran.

### 2.1 Yang TIDAK perlu ditanyakan (sudah resmi dari slide)

Supaya jelas batas antara yang kita tahu dan yang kita tebak:

- Nama paket: `@danarakca/keu-ui`
- Cara memanggil token: `@use 'index' as *`
- Nama class: `.page-header`, `.stats-row`, `.table-card`
- Warna: Navy `#003d7a`, Blue `#275EA8`, Gold `#FCB332`

Selebihnya di bawah ini **tebakan kita**.

### 2.2 Arsitektur paket (★ paling menentukan)

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 1 ★ | `_index.scss` berisi **hanya token** (variabel/mixin), tidak menghasilkan CSS | Benarkah `@use 'index' as *` hanya mengekspos token? Kalau ia juga mengandung CSS katalog, CSS-nya akan terduplikasi di tiap komponen — bagaimana platform menanganinya? |
| 2 ★ | CSS katalog komponen dimuat **sekali** secara global — **oleh shell** saat remote berjalan di dalamnya, dan lewat `styles` di angular.json saat remote berjalan mandiri | Bagaimana cara resmi memuat CSS katalog komponen? Apakah shell ICS sudah menyuntikkannya untuk semua remote? *(Terbukti di P3.2: saat dimuat lewat Native Federation, `styles` global milik remote **tidak ikut termuat** — jadi kalau shell tidak menyediakan katalog, remote tidak punya tampilan sama sekali.)* |
| 3 ★ | Paket mengekspor komponen Angular, diimpor lewat barrel `@danarakca/keu-ui` | Apakah paket ini berisi komponen Angular, atau murni CSS + token? Kalau ada komponen, apa nama entry point-nya? |
| 4 | Paket dipasang sebagai dependency npm biasa dari registry internal | Apakah pemasangannya lewat npm registry internal, atau mekanisme lain (git submodule, artifact feed)? |
| 5 | Nama folder `styles/` di dalam paket | Apa struktur folder paket resminya, supaya `includePaths` diarahkan dengan benar? |

### 2.3 Nama class yang kita karang (★ — dipakai langsung di template aplikasi)

Enam nama class tingkat atas berikut **tidak ada di slide**. Kalau platform sudah punya
padanannya dengan nama lain, sebutkan namanya supaya kami ganti sebelum menulis banyak halaman.

| # | Class tebakan | Kebutuhan SIGAP |
| --- | --- | --- |
| 6 ★ | `.form-field` | Form asesmen 5 aspek, form trigger broadcast, form laporan potensi bencana |
| 7 ★ | `.button` (varian `--secondary`, `--ghost`, `--danger`, `--success`, `--block`) | Approve/reject, kirim asesmen, akhiri broadcast |
| 8 ★ | `.status-badge` (varian `--neutral`, `--info`, `--success`, `--warning`, `--danger`) | Status 5 aspek (Normal/Terganggu/Berhenti Total) & kondisi safety check |
| 9 ★ | `.tabs` (`__list`, `__tab`, `__tab--active`, `__count`, `__panel`) | Rekap safety check per kondisi — wajib tab, koreksi stakeholder #11 |
| 10 ★ | `.modal` + `.modal-backdrop` (`__header`, `__title`, `__body`, `__footer`, `--wide`) | Popup broadcast safety check yang wajib dijawab |
| 11 ★ | `.stat-card` (anak dari `.stats-row`) | Kartu angka di dashboard Monitor SC & Sumber Daya |

### 2.4 Struktur anak dari class yang resmi

Nama induknya resmi, tapi seluruh nama anak di bawah ini kita karang dengan gaya BEM.

| # | Induk (resmi) | Anak yang kita karang |
| --- | --- | --- |
| 12 | `.page-header` | `__title`, `__subtitle`, `__actions` |
| 13 | `.stats-row` | (tidak punya anak; isinya `.stat-card`) |
| 14 | `.stat-card` | `__label`, `__value`, `__context`, `--success`, `--warning`, `--danger` |
| 15 | `.table-card` | `__header`, `__title`, `__toolbar`, `__empty`, `__footer` |
| 16 | `.form-field` | `__label`, `__required`, `__hint`, `__error`, `--invalid` |

Pertanyaan ke BaTII: **apakah platform memakai konvensi BEM (`blok__elemen--varian`)?** Kalau
konvensinya berbeda (mis. utility class atau `.page-header .title`), seluruh baris di atas
berubah.

### 2.5 Nama token SCSS

Seluruh **nama** variabel token adalah tebakan — termasuk untuk tiga warna resmi, karena slide
menyebut nilainya tapi tidak menyebut nama variabelnya.

| # | Kelompok | Nama tebakan kita |
| --- | --- | --- |
| 17 ★ | Warna merek | `$color-navy`, `$color-blue`, `$color-gold` |
| 18 | Turunan merek | `$color-navy-dark`, `$color-blue-light`, `$color-gold-light` |
| 19 | Permukaan & garis | `$color-surface`, `$color-surface-muted`, `$color-border`, `$color-border-strong` |
| 20 | Teks | `$color-text`, `$color-text-muted`, `$color-text-inverse` |
| 21 | Status | `$color-success`, `$color-warning`, `$color-danger`, `$color-info` + varian `-light` / `-dark` |
| 22 | Jarak | `$space-xs` … `$space-xl` |
| 23 | Radius | `$radius-sm`, `$radius-md`, `$radius-lg` |
| 24 | Bayangan | `$shadow-card`, `$shadow-modal` |
| 25 | Tipografi | `$font-family-base`, `$font-size-sm/base/lg/title`, `$font-weight-*`, `$line-height-base` |
| 26 | Breakpoint | `$breakpoint-sm/md/lg` |
| 27 | Mixin | `focus-ring($color)`, `media-up($breakpoint)` |

Pertanyaan ★: **apakah token diekspos sebagai variabel SCSS, CSS custom property (`--color-navy`),
atau map SCSS?** Karena `@use 'index' as *` menyiratkan variabel SCSS, kami berasumsi variabel.

### 2.6 Nilai token turunan

| # | Asumsi |
| --- | --- |
| 28 | Seluruh nilai warna turunan di `_tokens.scss` (surface `#ffffff`, border `#d8dee7`, text `#1c2430`, dst) kami karang agar kontras cukup — **bukan** dari dokumentasi |
| 29 | Palet status: sukses `#1d7a4c`, peringatan `#b26a00`, bahaya `#b3261e` |
| 30 | Skala jarak 4/8/16/24/32 px |
| 31 | Font `Inter` dengan fallback `Segoe UI` — platform kemungkinan punya font resmi sendiri |
| 32 | Ukuran font dasar 14px |

### 2.7 Nama komponen Angular

| # | Nama tebakan | Bentuk |
| --- | --- | --- |
| 33 ★ | `KeuModalComponent` | `<keu-modal [open] [title] [wide] [dismissible] (closed)>` |
| 34 ★ | `KeuTabsComponent` | `<keu-tabs>` |
| 35 ★ | `KeuTabComponent` | `<keu-tab [label] [count]>` |
| 36 | Prefix selector `keu-` | Platform mungkin memakai `kmk-`, `ics-`, atau lainnya |
| 37 | Komponen `standalone: true` | Perlu dipastikan versi Angular & gaya modul yang dipakai platform |
| 38 | `@Input()`/`@Output()` klasik, bukan signal `input()`/`output()` | Kami pilih yang paling kompatibel lintas versi; sesuaikan kalau platform menetapkan versi Angular tertentu |

### 2.8 Yang sengaja TIDAK kami buat

Supaya tidak menambah nama tebakan tanpa perlu, komponen berikut **tidak** dibuat sebagai
komponen Angular meski dipakai SIGAP — cukup class CSS:

kartu statistik, header halaman, tabel, badge status, form field, tombol.

**Paginasi** juga belum dibuat, padahal di prototipe ia komponen yang paling sering dipakai ulang
(31+ lokasi, lihat [COMPONENT_INVENTORY.md](COMPONENT_INVENTORY.md)). Pertanyaan ★ ke BaTII:
**apakah design system platform menyediakan komponen paginasi?** Kalau tidak, ini satu-satunya
komponen visual yang terpaksa dibangun SIGAP sendiri, dan itu perlu persetujuan karena melanggar
prinsip "jangan menduplikasi katalog".

---

## 3. Asumsi shell, identitas remote & integrasi yang perlu diverifikasi ke BaTII

Bernomor lanjutan dari bagian 2. Tandai ★ kalau memblokir penukaran.

### 3.1 Yang TIDAK perlu ditanyakan (sudah resmi dari slide)

- Shell hanya mengurus sidebar, otentikasi, dan routing; dilarang ada logika bisnis.
- Remote punya router sendiri yang sinkron dua arah dengan URL browser ("Silent Location Strategy").
- Routing remote: path relatif, flat, dilarang nested.
- Perakitan saat runtime dengan Native Federation; remote dimuat lazy saat menunya diklik.
- Pola Golden Rule: `remoteUserManagement` → `remote-user-management-element`,
  `defineRemoteUserManagementElement`, `app-remote-user-management-entry`, `/user-management`,
  `User Management`. Port shell 4200, remote berurutan setelahnya.

### 3.2 Identitas remote SIGAP (★ — nilai PROVISIONAL)

| # | Nilai kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 39 ★ | `remoteName` = `remoteSigapBencana`, beserta seluruh turunannya | Apa `remoteName` resmi SIGAP? (Lampiran E #2) |
| 40 ★ | Port lokal 4299 | Port lokal berapa yang dialokasikan untuk SIGAP? |
| 41 | Display name `SIGAP Bencana` — satu-satunya nilai yang tidak diturunkan mekanis dari `remoteName` (akronim ditulis kapital) | Apakah display name boleh berbeda kapitalisasi dari turunan `remoteName`? |

Penukaran: ubah `apps/sigap-web/remote-identity.json` saja, lalu `npm test` — tes Golden Rule
memastikan turunannya tetap konsisten.

### 3.3 Kontrak integrasi shell ↔ remote (★ paling menentukan)

Ini tebakan yang paling mahal kalau meleset, karena menentukan apakah remote bisa dimuat shell
asli sama sekali. Kalau `starter.mfe` sudah membawa semua ini, cukup minta `starter.mfe`-nya.

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 42 ★ | Remote dirakit sebagai **custom element** (Angular Elements) — ditafsir dari trio element/function/selector di Golden Rule | Apakah remote diekspos sebagai web component, atau shell memuat komponen Angular langsung ke router-nya? |
| 43 ★ | Modul yang diekspos bernama `./web-components` di `federation.config` | Apa kunci `exposes` yang dicari shell? |
| 44 ★ | Fungsi define adalah **named export**, tanpa argumen, **async** (`Promise<void>`), idempoten. Shell meng-await-nya lalu membuat elemen | Apa signature resmi fungsi `defineRemote...Element`? Apakah menerima argumen (mis. konfigurasi, token)? |
| 45 ★ | Shell memasang remote di route path-nya dan **menyerahkan seluruh sub-path** ke router remote. Remote memakai route path sebagai `APP_BASE_HREF` | Benarkah shell meneruskan seluruh `/sigap-bencana/**` ke remote? |
| 46 ★ | "Silent Location Strategy" = (a) router remote mengabaikan perubahan URL di luar wilayahnya; (b) setelah navigasi imperatif, router yang bernavigasi mengirim `popstate` agar router lain menyelaraskan diri | Apa mekanisme resminya? Apakah disediakan `starter.mfe` (nama kelas/fungsi)? Apakah shell mengirim event saat bernavigasi, dan event apa? |
| 47 | URL ditulis tanpa garis miring di akhir (`/sigap-bencana`, bukan `/sigap-bencana/`), agar router shell dan remote tidak saling menulis ulang | Apa konvensi URL platform? |
| 48 | "Path relatif" ditafsir: kode remote tidak pernah menulis route path-nya sendiri (`/sigap-bencana/...`). Tautan ke akar remote memakai `routerLink="/"` milik router remote | Benarkah tafsiran ini? Atau `routerLink` berawalan `/` dilarang sama sekali? |
| 49 | Remote tidak mengubah `document.title`; judul milik shell | Siapa yang mengatur judul tab browser? |
| 50 | Kebijakan shared dependencies: `shareAll` singleton + `strictVersion` (bawaan schematic). **Berpotensi bertentangan** dengan prinsip "Berdiri Sendiri" (tiap tim bebas menaikkan versi framework): dengan `strictVersion`, versi Angular remote yang berbeda dari shell akan gagal dimuat | Apa kebijakan sharing resmi? Apakah Angular dibagi antar remote, dan bagaimana kalau versinya berbeda? |
| 51 ★ | Cara shell menyerahkan token ke remote — **belum ditiru sama sekali** | Lampiran E #12: service, event bus, atau storage? |

### 3.4 Shell & registry

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 52 | Registry modul disusun **saat build**; kill-switch tidak ditiru | Bagaimana remote didaftarkan ke registry sisi server, dan siapa yang mengelola kill-switch? |
| 53 | Sidebar hanya berisi satu entri per modul; menu di dalam modul urusan remote | Apakah sidebar shell menampilkan sub-menu per modul? Kalau ya, dari mana daftar sub-menu dan penyaringan per permission-nya? |
| 54 | URL remote entry `http://<host>:<port>/remoteEntry.json` (standar Native Federation) | Apa pola URL remote entry di lingkungan platform? |

### 3.5 Scaffold `apps/sigap-web` (pengganti `starter.mfe`)

| # | Pilihan kita | Direkonsiliasi di P7.2 |
| --- | --- | --- |
| 55 | Angular 22.1 (zoneless, bawaan CLI), Native Federation 22.1, Vitest | Versi Angular & test runner yang dibawa `starter.mfe` |
| 56 | Monorepo **npm workspaces** — dependensi di-hoist ke `node_modules` root, supaya `libs/` bisa me-resolve `@angular/*` | Struktur repo resmi (Lampiran E #11) |
| 57 | Nama fungsi define dan selector di-generate dari `remote-identity.json` sebelum build (`scripts/remote-identity.mjs`), karena keduanya harus statis bagi compiler | Apakah `starter.mfe` punya mekanisme sendiri untuk ini? |

---

## 4. Asumsi IAM tiga lapis yang perlu diverifikasi ke BaTII

Bernomor lanjutan dari bagian 3. Tandai ★ kalau memblokir penukaran. Pertanyaan terkait yang
sudah ada di PERMISSION_MAP bagian 8 dan API_CONTRACT bagian 9 dirujuk, tidak diulang.

### 4.1 Yang TIDAK perlu ditanyakan (sudah resmi dari slide)

- Lapis 1: `[KemenkeuAuthorize('app:resource:action')]` di endpoint C#; `*hasPermission` di Angular.
- Lapis 2: Scope difilter langsung di query database / klausa WHERE.
- Lapis 3: Sieve mengubah field JSON menjadi `null` di level respons.
- Kebijakan IAM (peran, izin, Scope) tersimpan sebagai data, bukan di kode.
- Validasi keamanan didelegasikan ke `iam.plugin` (.NET 10) terpusat.

### 4.2 Backend (`iam.plugin`)

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 58 ★ | Namespace `Kemenkeu.Iam`; paket NuGet bernama `iam.plugin` | Apa nama paket dan namespace resmi? |
| 59 ★ | `[KemenkeuAuthorize]` menerima satu string; beberapa atribut pada endpoint yang sama = **semua** wajib dipegang; turunan `AuthorizeAttribute` sehingga `[AllowAnonymous]` berlaku; tanpa token → 401, tanpa izin → 403 | Apa signature resmi dan semantik atribut ganda? |
| 60 ★ | Permission **diturunkan** dari klaim `groups` + kebijakan, bukan dibaca langsung dari klaim permission di token | Slide menyebut "token IAM berdasarkan jabatan". Apakah token itu sudah memuat daftar permission? Kalau ya, klaim apa namanya? |
| 61 ★ | `ICurrentUserContext` dengan `Nip`, `UserId`, `UnitId`, `Provinsi`, `EselonIKey`, `Roles`, `Permissions`, `HasPermission()`, `GetScope()` | Apakah plugin menyediakan konteks pengguna? Apa nama dan anggotanya? |
| 62 ★ | Scope: `pengguna.GetScope(permission)` lalu `query.ApplyScope(scope, unit:, owner:)`. Lingkup WILAYAH/ESELON_I diresolusi jadi daftar unit ID → `= ANY (@UnitIds)` | Apa bentuk API Scope resmi? Bolehkah predikat berupa subquery? (PERMISSION_MAP bagian 8 no. 4) |
| 63 | Peran ganda: gabungan **OR** atas peran yang memberi permission endpoint; peran lain diabaikan | PERMISSION_MAP bagian 8 no. 3 |
| 64 | Provinsi/Eselon I kosong → lingkup **menyempit ke UNIT**, tidak pernah melebar | Bagaimana plugin menangani data organisasi yang tidak lengkap? |
| 65 | Profil domain (`SASARAN_SAYA`, `TERSENTUH`, `PEMICU_ATAU_MENCAKUP`, `UNIT_SENDIRI`, `IKUT_INDUK`) tidak ditangani plugin; aplikasi menyusunnya dari wilayah yang sudah diresolusi | Bisakah aturan Scope semacam ini didaftarkan ke IAM, atau memang tanggung jawab aplikasi? |
| 66 ★ | Sieve ditandai `[Sieve("kunci")]` di properti DTO; siapa yang melihat ada di kebijakan (`sieve.<kunci>.terlihatUntuk`). Diterapkan saat serialisasi System.Text.Json; properti tetap ada bernilai `null` | Bagaimana field Sieve didaftarkan dan ditandai? (Lampiran E #6) |
| 67 | Sieve hanya menghitung peran pemanggil yang **memberi** permission endpoint | Sama dengan butir 63 |
| 68 ★ | Data organisasi pengguna (id, unit, provinsi, Eselon I) diambil lewat `IOrganizationResolver` yang diimplementasikan aplikasi dari tabel `"User"`/`"Unit"` | Apakah IAM menyediakan sendiri data organisasi pengguna (database identitas)? Apakah `kode_satker` memetakan ke `"Unit"."kode"`? (API_CONTRACT bagian 9 no. 5) |
| 69 | Registrasi `services.AddKemenkeuIam(configuration)`, bagian konfigurasi `"Iam"` (`Authority`, `Audience`, `PolicyFile`, `RequireHttpsMetadata`) | Bagaimana plugin dipasang dan dikonfigurasi? |
| 70 | 403 dikirim sebagai `application/problem+json` dengan `kode: TIDAK_BERWENANG` | Apa bentuk respons 403 standar platform? (API_CONTRACT bagian 9 no. 3) |
| 71 | Klaim `nip` (cadangan `preferred_username`) dan `groups`; grup berformat path (`/sigap-pegawai`) dinormalisasi | Sesuai Kebutuhan Teknis — dipastikan saat P3.4 dan pendaftaran client OIDC |
| 72 | Format berkas kebijakan `iam-policy.sigap.json`. Tiga penyempurnaan dari draf PERMISSION_MAP bagian 7: (a) `PEMICU_ATAU_MENCAKUP(X)` diberi argumen wilayah; (b) profil ditandai `generik`/`domain` + `wilayahDasar`; (c) Sieve dikunci per kunci field dan aturan `/monitor/unit/{unitId}` digabung (hasilnya identik karena butir 67) | Lampiran E #6 |
| 73 | Permission yang tidak terdaftar di kebijakan → galat 500, bukan 403 | Perilaku plugin asli terhadap permission tak dikenal? |
| 74 | Audit trail **tidak ditiru** | Lampiran E #10 |
| 101 | `DataScope` tidak punya konstruktor publik, sehingga tes unit di luar iam-dummy tidak dapat merakitnya. Kode aplikasi menyiasatinya dengan memisahkan bagian murni yang bekerja atas `ScopeGrant` (publik) — lihat `LingkupTampilan` di sigap-api. *(ditemukan 21 Sep 2026, P4.1)* | Apakah `iam.plugin` menyediakan cara merakit lingkup untuk pengujian? Tanpa itu, setiap use case yang membaca `DataScope` hanya dapat diuji lewat tes integrasi berpipeline penuh |

### 4.3 Frontend (`*hasPermission`)

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 75 ★ | Directive berasal dari paket `@danarakca/iam` | Dari paket atau template mana `*hasPermission` berasal — `starter.mfe`, `@danarakca/keu-ui`, atau paket sendiri? |
| 76 ★ | Argumen satu string: `*hasPermission="'sigap:laporan:verify'"`. Tanpa `else`, tanpa array | Apa bentuk argumen resminya? Apakah menerima beberapa permission (any/all) dan template `else`? |
| 77 ★ | Daftar permission dipasang lewat `provideIamPermissions(loader)`; sebelum tiba atau bila gagal, semua tersembunyi | Bagaimana remote mendapatkan daftar permission pengguna — dari shell (Lampiran E #12) atau endpoint plugin? (PERMISSION_MAP bagian 8 no. 5) |

---

## 5. Asumsi SSO yang perlu diverifikasi ke BaTII

Bernomor lanjutan dari bagian 4. Kolom "Diisi BaTII" di Kebutuhan Teknis bagian E masih kosong,
jadi seluruh nilai di dummy adalah **usulan kita**.

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 78 ★ | Realm `kemenkeu`, issuer dev `https://sso-dev.kemenkeu.go.id/realms/kemenkeu` | Issuer & discovery resmi lingkungan development? |
| 79 | Username = NIP, sehingga `preferred_username` dan `nip` bernilai sama | Apakah username SSO memang NIP? |
| 80 ★ | `kode_satker` = `"Unit"."kode"`, `kode_eselon1` = `"Unit"."eselonIKey"` (mis. `djp`, `setjen`) | Apa format resmi kedua klaim (kode numerik?) dan bagaimana memetakannya ke tabel `"Unit"`? (API_CONTRACT bagian 9 no. 5; Kebutuhan Teknis bagian VIII) |
| 81 | Scope `groups` dan `satker` tidak dimodelkan sebagai scope opsional; klaimnya selalu dibawa | Apakah klaim itu hanya muncul bila scope diminta? Scope apa yang wajib diminta remote? |
| 82 | Klaim `groups` berisi nama grup tanpa path (`sigap-pegawai`) | Format klaim grup: nama, path (`/sigap-pegawai`), atau `realm_access.roles`? (dummy IAM menerima nama maupun path) |
| 83 | Refresh token 8 jam dimodelkan sebagai sesi SSO idle & maksimum 8 jam | Kebijakan sesi SSO platform? |
| 84 ★ | Client `sigap-web-dev` untuk remote module | Di arsitektur ICS, login dilakukan **shell** (Lobi). Apakah remote tetap butuh client OIDC sendiri, atau cukup menerima token dari shell? (Lampiran E #12) |
| 85 | Redirect URI lokal `localhost:4200/*` & `4299/*`; usulan resmi `.../sigap/callback` | Berlaku hanya kalau jawaban butir 84 = remote login sendiri |
| 86 | `sigap-api-dev` hanya service account (client credentials). Terbukti: token service account-nya ditolak sigap-api (401) karena `aud` bukan `sigap-api` | Untuk apa client microservice dipakai: introspeksi token, panggilan ke layanan lain, atau hanya audience? |
| 87 | Client ketiga `sigap-uji-lokal` (password grant) — **khusus lokal, tidak diminta** | — |
| 88 | Keycloak 26.0 mode `start-dev`, data realm tidak persisten (impor ulang tiap container baru) | — |

---

## 6. Asumsi notifikasi yang perlu diverifikasi ke BaTII

Bernomor lanjutan dari bagian 5. Berbeda dari bagian lain, di sini **tidak ada satu pun
kontrak platform yang ditiru** — slide arsitektur ICS tidak menyebut notifikasi sama sekali.
Itulah sebabnya bentuknya abstraksi: ia menyerap jawaban apa pun tanpa mengubah kode fitur.

### 6.1 Dua pertanyaan yang menentukan

| # | Asumsi kita | Pertanyaan ke BaTII |
| --- | --- | --- |
| 89 ★ | Platform **tidak** menyediakan layanan notifikasi bersama, sehingga SIGAP mengantar sendiri | Apakah ada layanan notifikasi bersama di antara 20 layanan data/API? (Lampiran E #9) Bila ada, ia menjadi satu kanal tambahan dan abstraksinya tidak berubah |
| 90 ★ | Web Push **belum** diizinkan, jadi kanalnya disiapkan tetapi dimatikan | Apakah Web Push diizinkan di domain platform? Kalau tidak, penggantinya apa? (Lampiran E #13) |

### 6.2 Yang kita tetapkan sendiri

| # | Pilihan kita | Catatan |
| --- | --- | --- |
| 91 | Nama kanal `dalam-aplikasi`, `web-push`, `log` | Tidak ada konvensi platform yang diketahui |
| 92 | Bentuk konfigurasi: `Notifikasi:Kanal` berupa daftar nama, seluruhnya dijalankan bersamaan | Bukan rantai cadangan — untuk sistem kedaruratan, dua jalur paralel lebih andal daripada satu rantai yang bergantung pada deteksi kegagalan |
| 93 | Konfigurasi wajib memuat minimal satu kanal **tahan luring**, kalau tidak proses menolak mulai | Menegakkan syarat PLAYBOOK P5.3 di tingkat konfigurasi, bukan lewat disiplin |
| 94 | Muatan Web Push memakai bentuk satu butir `GET /notifikasi` | **Sengaja berbeda dari prototipe**, yang menaruh `tautan` (rute halaman) di muatan. API_CONTRACT #43: `terkait` menunjuk sumber daya, pemetaan ke halaman urusan Angular. Rute yang tertanam di muatan peladen akan basi tiap kali rute berubah |
| 95 | TTL 3600 detik; `Urgency: high` hanya untuk tingkat `GENTING` | TTL dari prototipe; pemetaan urgency kita tetapkan |
| 96 | Panjang maksimum judul 200 dan pesan 500 karakter | API_CONTRACT belum menetapkannya |
| 97 | Pemberitahuan `GENTING` wajib menyertakan `terkait` | Peringatan paling genting yang tidak dapat dibuka penerimanya tidak ada gunanya |
| 98 | Idempotensi memakai `"KirimanPush"."kunci"` yang sudah ada, untuk **semua** kanal sekaligus | Tanpa perubahan skema. Ditangani di lapisan pengirim supaya satu keadaan tidak lolos di satu kanal dan tertahan di kanal lain |
| 99 | Kunci VAPID hanya lewat environment variable atau vault | Siapa menerbitkan dan memutar kunci VAPID bila Web Push disetujui? |
| 100 | Jejak audit pemberitahuan **tidak ditiru** | Senada butir 74, menunggu Lampiran E #10 |

---

## 7. Urutan penukaran (Lampiran D PLAYBOOK)

| No | Dummy | Lokasi | Dipicu oleh | Beban | Status |
| --- | --- | --- | --- | --- | --- |
| 1 | `remoteName` + port | `apps/sigap-web/remote-identity.json` | BaTII menetapkan nilai resmi | Ringan, satu file konfigurasi | **Aktif** |
| 2 | Design system | `libs/keu-ui-dummy` | Kredensial registry npm internal | Sedang | **Aktif** |
| 3 | `iam.plugin` + `*hasPermission` | `libs/iam-dummy`, `libs/iam-dummy-web` | Kredensial NuGet internal; paket asal directive | Ringan kalau kontrak dipatuhi | **Aktif** |
| 4 | SSO | Keycloak lokal (`infra/keycloak/`) | Client OIDC didaftarkan BaTII | Ringan, ganti issuer & client id | **Aktif** |
| 5 | Shell + starter.mfe | `apps/shell-dummy`, `apps/sigap-web/src/federation/` | Akses shell ICS + template | Sedang, lihat P7.2 | **Aktif** |
| 6 | Data SIMAN (koordinat) | `infra/kantor-bmn-seed/` | API SIMAN + koordinat tersedia | Ringan, ganti seeder | **Aktif** |
| 7 | Notifikasi | `libs/notifikasi-dummy` | Jawaban Web Push / layanan platform | Ringan, ganti konfigurasi kanal | **Aktif** |

---

## 8. Aturan dummy (ringkasan, sumber `docs/PLAYBOOK.md` Fase 3)

1. **Karantina** — semua dummy di `libs/*-dummy/` atau `apps/shell-dummy/`, terpisah dari kode aplikasi.
2. **Kontrak identik** — nama dan signature mengikuti dokumentasi platform.
3. **Isi sesederhana mungkin** — dummy tidak perlu benar, hanya perlu berjalan.
4. **Tidak boleh naik ke production** — build production gagal kalau dummy masih ter-resolve.
   *(Sebagian ditegakkan 21 Sep 2026, P4.1: target `LarangDummyDiPublish` di `Sigap.Api.csproj`
   menggagalkan `dotnet publish` selama masih ada rujukan ber-nama `*Dummy*`, dan
   `libs/notifikasi-dummy` bahkan tidak ikut disusun pada konfigurasi Release. Pemeriksa
   seluruh solusi di CI menyusul di P6.2.)*
5. **Tercatat di berkas ini.**

Yang ditiru adalah **kontraknya**, bukan cara kerjanya. Kode aplikasi ditulis seolah-olah platform
asli sudah ada — tidak boleh ada workaround yang menyesuaikan diri dengan keterbatasan dummy.
