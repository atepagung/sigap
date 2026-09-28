# DEFINITION_OF_DONE

Acuan review internal sebelum submit ke BaTII (PLAYBOOK P6.5). Satu modul dianggap **selesai** bila
kesepuluh butir di bagian 2 terpenuhi untuknya. Checklist per *pull request* ada di
[CONTRIBUTING.md](CONTRIBUTING.md) bagian 5; berkas ini menjawab pertanyaan yang lebih besar: "modul ini
sudah pantas diserahkan?"

Prinsipnya: **setiap butir punya penegak.** Yang ditegakkan mesin cukup dibuktikan dengan menjalankan
gerbang di bagian 1 — tidak perlu diperiksa ulang dengan mata. Yang tidak bisa diperiksa mesin ditandai
👁 dan punya pertanyaan review di bagian 5. Butir yang "sudah benar" tetapi tanpa penegak dianggap
**belum** selesai: tanpa penegak, ia akan rusak diam-diam pada perubahan berikutnya.

---

## 1. Gerbang — satu kali jalan untuk semua modul

Jalankan dari akar repo, semuanya harus hijau. Hasil terakhir dicatat di bagian 4.

```bash
dotnet test apps/sigap-api/sigap-api.slnx                          # butir 1, 4-8 (sigap-api)
dotnet format apps/sigap-api/sigap-api.slnx --verify-no-changes    # butir 2 (sigap-api)
npm run check:web                                                   # butir 1-3, 9-10 (sigap-web)
npm run periksa:repo                                                # lintas platform
npm run test:skrip                                                  # pemeriksa repo
node scripts/verifikasi-linux.mjs semua                             # semua di atas, di Linux
npm run verifikasi:container                                        # image dibangun dan dijalankan
```

`node scripts/verifikasi-siap-produksi.mjs` **bukan** gerbang DoD: ia sengaja gagal selama dummy platform
aktif (DUMMY_REGISTRY bagian 8), dan penyerahan ke BaTII memang dilakukan dengan dummy.

---

## 2. Butir checklist

| # | Butir | Berlaku | Ditegakkan oleh | Gagal bila |
| --- | --- | --- | --- | --- |
| 1 | **Unit test lulus** | api, web | `dotnet test`, Vitest (`check:web`) | ada tes merah; atau modul tanpa tes untuk logikanya (👁 bagian 5) |
| 2 | **Lint bersih** | api, web | analyzer + `TreatWarningsAsErrors`, `dotnet format`; ESLint, Stylelint, Prettier | satu peringatan pun |
| 3 | **Tanpa hardcode warna** | web | Stylelint `color-no-hex`/`color-named`/`function-disallowed-list` (.scss, `<style>`, `style=""`); ESLint `no-restricted-syntax` (string TypeScript). Pengecualian tunggal: `libs/keu-ui-dummy/styles/_tokens.scss` | hex, `rgb()`, `hsl()`, nama warna di luar berkas token |
| 4 | **Semua endpoint ber-`[KemenkeuAuthorize]`** | api (+ web) | `ProteksiEndpointTests`: seluruh routing host dibaca, permission = kolom API_CONTRACT, tanpa token 401, tanpa permission 403; `Izin*Tests`: matriks peran per domain; `IzinTests`: `Izin.cs` = kebijakan = PERMISSION_MAP. Web: `*hasPermission` membungkus aksi tulis 👁 | aksi tanpa atribut, permission beda dari kontrak, `[AllowAnonymous]` di endpoint bisnis |
| 5 | **Scope di level query** | api | tes yang membaca SQL tercatat (`App.Sql`: `"unitId" = ANY (@UnitIds)` di klausa WHERE), `Scope/Keberadaan*Tests` (di luar lingkup = 404, bukan 403), `LingkupSafetyCheckTests` (lingkup dikosongkan → tidak ada data). Aturan: `GetScope` dan `[KemenkeuAuthorize]` memakai permission yang sama; tanpa `.ToList()` sebelum `ApplyScope` 👁 | data di luar lingkup terbaca, atau disaring di memori |
| 6 | **Sieve pada field sensitif** | api (+ web) | tes per kunci Sieve di `iam-policy.sigap.json` (5 kunci, bagian 3); field tetap ada dengan nilai `null`. Web: `null` tampil "tidak tersedia", bukan galat 👁 | field sensitif terkirim ke peran yang tidak berhak |
| 7 | **Dokumentasi OpenAPI terisi** | api | `OpenApiTests`: setiap operasi tercatat di kontrak, punya `summary`, skema respons sukses, dan 401/403 sebagai `problem+json`; `OpenApi*Tests` per domain untuk kode galat spesifik | operasi tanpa ringkasan, tanpa skema, atau galat tak terdokumentasi |
| 8 | **Audit trail tercatat** | api | interseptor `SaveChanges` (otomatis, satu transaksi); `ArsitekturAuditTests` (setiap `ExecuteUpdate` wajib mencatat, setiap tabel diputuskan diaudit/tidak); `CatatAksesFilter` + `ArsitekturAksesTests` (akses data per pegawai → `DIAKSES`); tes jejak per aksi bernama | tulisan tanpa jejak; aksi bisnis tanpa nama/rujukan; data per pegawai dibaca tanpa jejak |
| 9 | **Routing flat tanpa nested** | web | ESLint `no-restricted-syntax` (`children`, `loadChildren`); satu entri per halaman di `app.routes.ts` | `children`/`loadChildren` |
| 10 | **Komponen memakai katalog SCSS** | web | Stylelint (butir 3) menjaga sumber warna. Pemakaian kelas katalog (`.page-header`, `.stats-row`, `.table-card`, …), tanpa `::ng-deep`/`!important`, tanpa `@use` berkas katalog di komponen 👁 | komponen visual yang menduplikasi katalog |

---

## 3. Status per modul (28 Sep 2026)

✔ = terpenuhi dan ditegakkan mesin · 👁 = terpenuhi, diperiksa manual · — = tidak berlaku · ⚠ = terbuka (bagian 6)

### 3.1 sigap-api

| Modul (endpoint) | 1 Tes | 4 Lapis 1 | 5 Scope di query | 6 Sieve | 7 OpenAPI | 8 Audit |
| --- | --- | --- | --- | --- | --- | --- |
| Safety Check (#1–#6) | ✔ `SafetyCheck/*` | ✔ + `IzinSafetyCheckTests` | ✔ `LingkupSafetyCheckTests`, `KeberadaanSafetyCheckTests` | ✔ `lokasiTerakhir`, `keterangan`, `dicatatOleh` — `RekapSafetyCheckApiTests` | ✔ + `OpenApiSafetyCheckTests` | ✔ respons/`DICATATKAN` (koordinat & keterangan disamarkan), `DIAKSES` #4 — `JejakAksesRekapTests` |
| Laporan & verifikasi (#7, #9, #10, #17, #18) | ✔ `Laporan/*` | ✔ + `IzinLaporanTests` | ✔ SQL di `BacaLaporanTests`, `KeberadaanLaporanTests` | — (PERMISSION_MAP bagian 6) | ✔ + `OpenApiLaporanTests` | ✔ `DIBUAT`, `DIVERIFIKASI`/`DITOLAK` — `JejakAuditTests` |
| Lampiran (#8, #11, #23) | ✔ `Lampiran/*` | ✔ | ✔ `IKUT_INDUK`, milik orang lain 404 — `LampiranTests` | — (`storageKey`/`url` tidak pernah diproyeksikan) | ✔ | ✔ `"Attachment"` `DIBUAT` |
| Broadcast (#12–#16) | ✔ `Broadcast/*` | ✔ + `IzinBroadcastTests` | ✔ SQL di `BacaBroadcastTests`, daftar sasaran per lingkup pembaca | — | ✔ + `OpenApiBroadcastTests` | ✔ `DIPICU` (peran/lingkup/unit), `DIAKHIRI` |
| Asesmen, layanan kritis, tanggap darurat (#19–#29) | ✔ `Asesmen/*` | ✔ + `IzinAsesmenTests` | ✔ SQL di `BacaAsesmenTests`, `KeberadaanAsesmenTests` | ✔ `asesmen.sdm.*` — `BacaAsesmenTests` | ✔ + `OpenApiAsesmenTests` | ✔ `DIBUAT`, `DIREVISI`, `DISETUJUI` — `JejakAsesmenTests` |
| Monitor (#30–#35) | ✔ `Monitor/*` | ✔ (403 per peran di `MonitorTests`) | ✔ SQL di `MonitorTests`, `KeberadaanMonitorTests` | ✔ `asesmen.sdm.*` di #35 — `MonitorTests` | ✔ | — (hanya agregat) |
| Konteks saya (#36) | ✔ `PerakitanTests` | ✔ `[Authorize]` tanpa permission (kontrak) | — (identitas sendiri) | — | ✔ | — |
| Referensi (#37–#42) | ✔ `Referensi/*` | ✔ | ✔ SQL di `ReferensiTests` (#37/#38 tanpa Scope, kontrak) | — | ✔ + `OpenApiReferensiTests` | — (baca saja) |
| Notifikasi (#43–#45) | ✔ `Notifikasi/*` | ✔ | ✔ per jenis peringatan — `NotifikasiTests` | — (kunci push tidak pernah diproyeksikan) | ✔ | ✔ langganan `DIBUAT`/`DIHAPUS`, kunci disamarkan — `GudangLanggananAuditTests` |
| Info bencana (#48) | ✔ `Integrasi/InfoBencanaTests` | ✔ | — (data publik, kontrak) | — | ✔ | — |
| Health (#46–#47) | ✔ `PerakitanTests` | — (publik, kontrak) | — | — | — | — |

Butir 2 (lint) berlaku untuk seluruh sigap-api sekaligus: ✔.

### 3.2 sigap-web

Butir 2, 3, dan 9 ditegakkan untuk seluruh remote sekaligus lewat `npm run check:web`: ✔.

| Feature folder | 1 Tes (`*.spec.ts` / berkas) | 4 `*hasPermission` pada aksi | 6 Sieve `null` → "tidak tersedia" | 10 Katalog SCSS |
| --- | --- | --- | --- | --- |
| `safety-check/` | ✔ 6 / 10 | 👁 trigger, catatkan, selesai, jawab | 👁 rekap | 👁 |
| `verifikasi-alert/` | ✔ 4 / 6 | 👁 lapor, unggah, verifikasi | — | 👁 |
| `asesmen-bencana/` | ✔ 4 / 6 | 👁 formulir (create/update dinamis), setujui, layanan kritis, selesai | 👁 detail asesmen | 👁 |
| `dashboard/` | ✔ 6 / 8 | — (baca saja) | 👁 detail unit | 👁 |
| `notifikasi/` | ✔ 3 / 6 | 👁 langganan push | — | 👁 |
| `beranda/`, `masuk/` | ✔ 1 / 1, 1 / 1 | 👁 kartu menu dari `permission[]` | — | 👁 |
| Info bencana (#48) | ⚠ belum dibangun | — | — | — |

👁 terakhir diperiksa menyeluruh 24 Sep 2026 per peran di peramban (MIGRATION_NOTES bagian 5.2, "Seeder
skenario"): seluruh aksi tulis dibungkus, Sieve tampil "Tidak tersedia". Tanpa `::ng-deep`, `!important`,
maupun `@use` katalog di komponen (grep 28 Sep 2026).

---

## 4. Hasil gerbang terakhir

| Tanggal | Gerbang | Hasil |
| --- | --- | --- |
| 28 Sep 2026 | `dotnet test` | 2.065 tes lulus, 0 gagal |
| 28 Sep 2026 | `dotnet format --verify-no-changes` | bersih |
| 28 Sep 2026 | `npm run check:web` | 34 berkas tes, 161 tes lulus; lint, stylelint, prettier bersih |
| 28 Sep 2026 | `node scripts/verifikasi-linux.mjs api` | lulus (10 tes MinIO dilewati, perlu MinIO) |

Perbarui tabel ini sebelum tiap penyerahan. Hasil yang tidak ditulis di sini dianggap belum dijalankan.

---

## 5. Review manual (👁) — pertanyaan per modul

Mesin tidak bisa menjawab pertanyaan berikut. Jawab "ya" untuk semuanya sebelum sebuah modul dinyatakan selesai.

**Semua modul**
- Apakah logika yang ditambahkan punya tes yang **pernah terbukti gagal** tanpa kodenya (uji mutasi)?
- Apakah fitur yang menyentuh perilaku sudah **dijalankan sungguhan** (API hidup + peramban), bukan hanya dikompilasi?
- Apakah tebakan tentang platform ditandai `[ASUMSI]` dan tercatat di DUMMY_REGISTRY?

**sigap-api**
- Use case memanggil `GetScope` dengan permission yang **sama** dengan `[KemenkeuAuthorize]` endpointnya (pengecualian terdokumentasi: `IKUT_INDUK` lampiran, baca ulang hasil tulis sendiri)?
- Tidak ada `.ToList()`/`.AsEnumerable()` sebelum `ApplyScope`, dan tidak ada `.Where()` di memori untuk membatasi lingkup?
- `unitId`/`userId` penulisan dari `ICurrentUserContext` atau dari baris yang lolos Scope — tidak dari body?
- Field baru yang memuat data pribadi sudah diputuskan: Sieve, disamarkan di jejak (`RingkasanJejak`), atau aman? (KANDIDAT_SCOPE_SIEVE)
- DTO baru yang membawa data keadaan per pegawai bertanda `IAksesTercatat` (tes arsitektur menangkap yang membawa `RekapBarisDto`; bentuk data baru perlu diputuskan)?

**sigap-web**
- Setiap tombol/formulir aksi tulis dibungkus `*hasPermission` dengan string yang sama persis dengan PERMISSION_MAP — dan tidak ada keputusan tampilan berdasar nama peran?
- Komponen memakai kelas katalog; kebutuhan di luar katalog **dilaporkan**, bukan dibangun (DUMMY_REGISTRY bagian 2)?
- Galat memuat dan galat aksi tampil ke pengguna (`PenampungGalat`), bukan layar kosong?
- Tidak ada NIP/nama/koordinat di URL, `localStorage`, atau `console`?

---

## 6. Butir terbuka sebelum penyerahan (28 Sep 2026)

| Butir | Modul | Keterangan |
| --- | --- | --- |
| Halaman info bencana (#48) belum ada | sigap-web | Endpoint siap; atribusi BMKG/BNPB wajib tampil (API_CONTRACT #48) |
| Mixin `shared/styles/_form-card.scss` tidak dipakai | sigap-web | Hapus, atau laporkan ke BaTII bahwa katalog belum punya kelas isi form di dalam `.table-card` |
| 409 `LANGGANAN_MILIK_PERANGKAT_LAIN` belum ditangani UI | sigap-web | UI langganan Web Push menunggu Lampiran E #13 |
| Perubahan P6.3 (R6–R10), P6.4, P6.5 belum di-commit | repo | Lihat `git status` |
