# AGENTS.md — SIGAP

Dibaca AI coding tool **sebelum menulis kode** (Standar Arsitektur ICS mewajibkan berkas ini pada
setiap remote MFE). Manusia juga boleh membacanya: ini ringkasan aturan yang paling sering
dilanggar. Detail dan alasan ada di dokumen yang ditautkan.

- Bekerja di `apps/sigap-web`? Baca juga [apps/sigap-web/AGENTS.md](apps/sigap-web/AGENTS.md).
- Bekerja di `apps/sigap-api`? Baca [apps/sigap-api/README.md](apps/sigap-api/README.md).
- Bahasa dokumen, komentar, dan pesan commit: **Indonesia**. Nama kode: kosakata domain
  berbahasa Indonesia (`KirimAsesmen`, `AspekSdm`), kata teknis tetap Inggris.

## 1. Apa ini

SIGAP (Sistem Informasi Gawat Darurat & Antisipasi Pemulihan) — modul Manajemen Keberlangsungan
Bisnis (MKB) Kementerian Keuangan. Di platform **ICS Keuangan**, SIGAP adalah **satu remote module**
(Angular, Native Federation) di antara 22 modul tampilan, dan **satu microservice** (.NET 10) di
antara 20 layanan data.

Sebagian komponen platform belum bisa diakses, jadi dipakai **dummy yang meniru kontraknya persis**
supaya nanti tinggal ditukar. Daftar dummy dan asumsinya: [DUMMY_REGISTRY.md](DUMMY_REGISTRY.md).

| Folder | Isi |
| --- | --- |
| `apps/sigap-web` | Remote MFE Angular 22 |
| `apps/sigap-api` | Microservice .NET 10, Clean Architecture |
| `apps/shell-dummy` | **Dummy** shell ICS — bukan bagian produk |
| `libs/*-dummy*` | **Dummy** design system, IAM, notifikasi — dikarantina |
| `libs/notifikasi` | Abstraksi kanal notifikasi — **bukan** dummy |
| `infra/skema` | DDL 33 tabel — sumber kebenaran skema database |
| `infra/keycloak`, `infra/kantor-bmn-seed` | SSO lokal dan seeder data dev |
| `docs/` | Dokumen sumber (PLAYBOOK, UR, Standar Arsitektur ICS, masukan stakeholder) |

## 2. Aturan mutlak

1. **Tulis kode seolah-olah platform asli sudah ada.** Jangan pernah menulis workaround yang
   menyesuaikan diri dengan keterbatasan dummy. Yang ditiru dummy adalah *kontraknya* (nama,
   signature), bukan cara kerjanya. Bila dummy tidak cukup, laporkan — jangan menambal di aplikasi.
2. **Jangan membangun ulang yang sudah disediakan platform:** shell & SSO, IAM tiga lapis, design
   system, layanan data, audit trail.
3. **Struktur 32 tabel tidak berubah.** Satu-satunya pengecualian yang disetujui: tabel ke-33
   `"BroadcastSasaranUnit"` dan kolom `"KantorBmn"."isKoordinatDummy"` (keduanya di
   `infra/skema/1x-*.sql`). Perubahan struktur lain **dilaporkan dulu** ke pemilik proyek, tidak
   langsung dikerjakan. EF Core schema-first: **dilarang** `Add-Migration`, `EnsureCreated`, atau
   code-first.
4. **Jangan pernah menempel secret asli** (kunci VAPID, kredensial, token) ke kode, dokumen,
   prompt, atau commit. Lewat environment variable atau vault. `.env` tidak masuk git.
5. **Prototipe Next.js tetap hidup** sampai sistem baru lolos UAT. Ia rujukan perilaku
   (`C:\dev\MKB APPS\App`), bukan sesuatu untuk diubah dari repo ini.

## 3. Scope Fase 1

**8 role aktif:** Pegawai Umum, Tim Satgas, Pimpinan Satker, Kepala Perwakilan, Subkoordinator,
Koordinator MKB, Sekretaris Jenderal, Administrator (tanpa permission bisnis). Dua role Fase 2
(Pengembang, Impl. RKB) hanya didaftarkan — **tanpa menu, endpoint, atau permission apa pun**.

**3 alur inti:** Broadcast Safety Check (otomatis BMKG MMI ≥ V + manual) → Verifikasi Alert
Bencana (Satgas wajib approve/reject) → Asesmen Dampak Bencana & aktivasi Tanggap Darurat
(5 aspek: SDM, Aset, TIK, Arsip, Layanan).

**Kontrak yang mengikat:** [API_CONTRACT.md](API_CONTRACT.md) (47 endpoint),
[PERMISSION_MAP.md](PERMISSION_MAP.md) (23 permission). Bila prototipe berbeda dari kontrak,
**kontrak menang** (API_CONTRACT bagian 6, 12 selisih yang disengaja). Koreksi stakeholder lebih
baru daripada mockup HTML — [MIGRATION_NOTES.md](MIGRATION_NOTES.md) bagian 2.

## 4. Larangan spesifik proyek

- **Jangan porting fitur Fase 2:** dokumen MKB (ARKB, ADB, SKB, RTDB, RKBU, RPKK), simulasi/drill,
  eksekusi RKB, LPKB, rilis komunikasi, menu "Panduan & Dok. MKB", AI Assistant. Tabelnya sudah
  dipetakan di `Persistensi/PraBencana` dan `PascaBencana` — **jangan** membuat endpoint atau use
  case untuknya.
- **Jangan menaruh logika bisnis di shell.** Shell hanya sidebar, otentikasi, dan routing.
- **Jangan menulis logika keamanan sendiri** (lihat bagian 5).
- **Jangan port `wewenang.ts` / `lingkup.ts`** dari prototipe ke C# atau TypeScript. Isinya sudah
  menjadi data di `apps/sigap-api/iam-policy.sigap.json`; memportingnya = menulis ulang logika
  keamanan.
- **Jangan menambah dependensi** yang menduplikasi platform (pustaka UI, pustaka auth, mediator).
  Tanpa MediatR: satu kelas per use case.
- **Jangan membuat komponen visual** yang menduplikasi katalog design system.

## 5. Keamanan tiga lapis (didelegasikan ke `iam.plugin`)

Saat ini dipenuhi `libs/iam-dummy` (backend) dan `libs/iam-dummy-web` (`*hasPermission`).
**Jangan menulis ulang** validasi token, pemeriksaan peran, atau penyaringan lingkup.

| Lapis | Di mana | Cara |
| --- | --- | --- |
| 1. Izin masuk | Controller | `[KemenkeuAuthorize(Izin.AsesmenApprove)]` — konstanta dari `Izin`, bukan string |
| | Angular | `*hasPermission="'sigap:laporan:verify'"` — hanya tampilan, **bukan** keamanan |
| 2. Scope | Klausa `WHERE` | `pengguna.GetScope(Izin.X)` di use case, `.ApplyScope(...)` di kueri, **sebelum** materialisasi |
| 3. Sieve | DTO respons | `[Sieve("kunci")]` pada properti; nilainya di-`null`-kan, propertinya tetap ada |

Aturan yang sering dilanggar:

- **`[KemenkeuAuthorize]` dan `GetScope` memakai permission yang sama.** Lingkup ditentukan peran
  yang *memberi* permission itu, bukan peran terluas pengguna.
- **Dilarang `if (peran == ...)`**, dilarang `Roles.Contains(...)` untuk memutuskan akses.
- **Dilarang menyaring di memori:** tidak ada `.ToList()` / `.AsEnumerable()` sebelum `ApplyScope`,
  tidak ada `.Where()` LINQ-to-objects atas hasil kueri untuk membatasi lingkup.
- **`unitId` dan `userId` untuk penulisan selalu dari `ICurrentUserContext`**, tidak pernah dari
  body permintaan.
- **Di luar Scope = 404**, bukan 403 dan bukan "di luar lingkup unit Anda" (tidak membocorkan
  keberadaan data).
- **Jangan menulis DTO ber-`[Sieve]` ke log atau cache** — nilai aslinya masih ada di objek.
- **Tidak pernah diproyeksikan ke respons mana pun:** `"User"."passwordHash"`, `"email"`,
  kunci `"LanggananPush"` (`p256dh`, `auth`, `endpoint`), `"Attachment"."storageKey"` / `"url"`.
  Daftar kandidat Sieve lainnya: [KANDIDAT_SCOPE_SIEVE.md](KANDIDAT_SCOPE_SIEVE.md).
- **Menambah permission** = ubah `iam-policy.sigap.json` **dan** `Izin.cs` **dan** PERMISSION_MAP.
  Tes `IzinTests` menggagalkan build bila salah satunya tertinggal.

## 6. Lintas platform — WAJIB

Development di **Windows**, production di **container Linux**. Empat hal ini lolos di Windows lalu
meledak di Linux. `node scripts/periksa-repo.mjs` dan CI (Linux) menegakkannya.

1. **Kapitalisasi nama berkas dan impor harus persis sama.** Windows tidak membedakan huruf
   besar/kecil, Linux membedakan. `import './Beranda'` untuk berkas `beranda.ts` jalan di Windows
   dan gagal di Linux. Nama berkas Angular: `kebab-case` huruf kecil. Jangan pernah mengganti
   nama berkas hanya dengan mengubah huruf besar/kecil tanpa `git mv`.
2. **Akhir baris LF**, di semua berkas teks. `.gitattributes` menormalkan (`* text=auto eol=lf`);
   pengecualian hanya `*.cmd` dan `*.ps1`. Jangan menulis berkas dengan CRLF, dan jangan
   menyunting `.gitattributes`.
3. **Path memakai garis miring `/`** di semua konfigurasi, skrip, dan kode (`tsconfig`,
   `angular.json`, `.csproj`, skrip `.mjs`). Backslash hanya jalan di Windows.
4. **Di C#: `Path.Combine()`**, jangan gabung path dengan string (`dir + "/" + nama`,
   `"a\\b"`). Untuk URL yang memang berupa URL, beri komentar `// periksa-repo: izinkan URL`.
5. **Skrip npm harus lintas platform** (PowerShell dan bash):
   - hapus berkas → `rimraf`, bukan `rm -rf`
   - variabel lingkungan → `cross-env NODE_ENV=production ...`, bukan `NODE_ENV=production ...`
   - dilarang `cp`, `mv`, `mkdir -p`, `cat`, `export`, dan path ber-backslash di `scripts`
   - butuh logika lebih? tulis skrip Node `.mjs`, jangan rantai perintah shell.
6. **Verifikasi build di container Linux** sejak awal, jangan ditunda ke akhir (PLAYBOOK P4.8).

## 7. Konvensi kode

- **C#:** PascalCase untuk kelas, method, properti, `const`, dan `static`; `_camelCase` hanya untuk
  field *instans* privat. `file-scoped namespace`, `Nullable` aktif, `TreatWarningsAsErrors`.
  Aturannya di `apps/sigap-api/.editorconfig` dan diperiksa `dotnet format` — bukan hanya build.
- **Nilai berskala memakai kode** (`RUSAK_RINGAN`), bukan enum yang diserialkan otomatis.
- **Galat aturan bisnis:** lempar `AturanBisnisException(kode, ...)`; jangan mengarang status HTTP
  di controller. Kode galat ada di `KodeGalat`.
- **TypeScript/Angular:** lihat `apps/sigap-web/AGENTS.md`.
- **Komentar menjelaskan *mengapa*, bukan *apa*.** Tandai tebakan tentang platform dengan
  `[ASUMSI]` dan catat di DUMMY_REGISTRY.

## 8. Perintah

```bash
docker compose up -d postgres
node infra/skema/terapkan.mjs --yes-development     # pasang 33 tabel ke database dev

dotnet test apps/sigap-api/sigap-api.slnx           # backend: tes + analyzer
dotnet format apps/sigap-api/sigap-api.slnx --verify-no-changes

npm run check:web                                   # frontend: lint + stylelint + prettier + tes
npm run periksa:repo                                # aturan lintas platform
npm run test:skrip                                  # tes pemeriksa repo
```

**Selesai berarti:** tes hijau, `dotnet format` / `npm run check:web` bersih, dan — bila menyentuh
perilaku — dijalankan sungguhan, bukan hanya dikompilasi. Tes yang tidak pernah ditunjukkan bisa
gagal tidak membuktikan apa-apa.

## 9. Cara bekerja dengan agen AI di repo ini

- **Tanyakan dulu bila permintaan bisa dibaca lebih dari satu cara** — dengan pilihan terfokus,
  contoh konkret dari domain, dan rekomendasi. Pekerjaan non-desain (verifikasi, dokumentasi)
  langsung dikerjakan.
- **Perubahan struktur tabel selalu dilaporkan dulu.**
- **Jangan menambah/mengganti remote git, jangan push, jangan mengubah konfigurasi git**
  (termasuk `core.hooksPath`). Itu dijalankan pemilik repo. Commit hanya bila diminta.
- **Laporkan hasil apa adanya:** tes gagal, langkah dilewati, atau asumsi yang belum terbukti —
  sebut jelas.

## 10. Rujukan

[docs/PLAYBOOK.md](docs/PLAYBOOK.md) (Lampiran A = standar platform) ·
[MIGRATION_NOTES.md](MIGRATION_NOTES.md) (scope, koreksi stakeholder, status) ·
[API_CONTRACT.md](API_CONTRACT.md) · [PERMISSION_MAP.md](PERMISSION_MAP.md) ·
[DUMMY_REGISTRY.md](DUMMY_REGISTRY.md) · [KANDIDAT_SCOPE_SIEVE.md](KANDIDAT_SCOPE_SIEVE.md) ·
[CONTRIBUTING.md](CONTRIBUTING.md)
