# Playbook SIGAP BENCANA
### Dari prototipe Next.js ke modul resmi di platform ICS Keuangan

Satu dokumen, satu urutan. Kerjakan dari atas ke bawah. Tiga blocker dari BaTII tidak menghentikan pekerjaan: bagian platform yang belum bisa diakses dibuat versi dummy-nya dulu, dengan kontrak yang sama persis, lalu ditukar saat yang asli tiba.

---

# BAGIAN I — CARA PAKAI

## Simpan playbook ini di repo

Salin file ini ke `docs/PLAYBOOK.md` di repo kamu. Semua prompt di bawah merujuk ke lampiran dokumen ini, jadi Claude Code perlu bisa membacanya.

## Alur satu sesi kerja

Satu sesi = satu prompt. Jangan tempel dua-tiga prompt sekaligus.

1. Buka terminal di folder repo, jalankan `claude`
2. Atur model dan effort sesuai yang tertulis di bagian itu: `/model` lalu `/effort high`
3. Tempel **prompt onboarding (P0)** — wajib diulang tiap sesi baru, karena Claude Code mulai tanpa ingatan sesi sebelumnya
4. Tempel prompt yang mau dikerjakan, setelah mengganti semua teks dalam `[ ]` dengan nilai asli
5. Review hasilnya, jalankan, verifikasi sendiri
6. Commit sebelum lanjut prompt berikutnya

Kalau belum tahu nilai untuk `[ ]`, tanya Claude Code dulu ("di mana file aturan bisnis di prototipe ini?") daripada menebak.

## Di antara prompt

Beberapa prompt sengaja berakhir dengan "tampilkan dulu rancangannya untuk saya review". Itu bukan basa-basi — rancangan yang kamu tidak pahami akan jadi kode yang kamu tidak bisa rawat.

Kalau hasil meleset, jangan minta ulang dari nol. Koreksi spesifik lebih efektif: "endpoint ini memfilter data di memori, pindahkan ke klausa WHERE sesuai standar Scope."

## Tiga titik henti

Berhenti dan bawa ke diskusi, jangan putuskan sendiri di tengah sesi coding:

- Claude Code minta mengubah struktur 32 tabel database
- Hasilnya bertentangan dengan standar platform ICS (Lampiran A)
- Ada kebutuhan visual yang tidak tercakup katalog design system

## Aturan mutlak sepanjang proyek

1. **Prototipe Next.js tetap hidup** sampai sistem baru lolos UAT. Tim SOPB masih memakainya untuk uji dan sosialisasi.
2. **Kode aplikasi ditulis seolah-olah platform asli sudah ada.** Jangan pernah menulis workaround yang menyesuaikan diri dengan keterbatasan dummy.
3. **Jangan membangun ulang apa yang disediakan platform.** SSO, IAM, design system, gateway, audit trail, template modul sudah ada. Tugas kita menambah satu modul di atasnya.
4. **Jangan pernah tempel secret asli ke dalam prompt.** Client secret, password DB, connection string, access key — semua lewat environment variable atau vault.
5. **Struktur 32 tabel database tidak berubah.** Dibaca apa adanya lewat EF Core.

## Windows di laptop, Linux di production

Development di Windows, production di container Linux (OpenShift/Kubernetes milik BaTII). Perbedaan keduanya punya kebiasaan buruk: tidak terasa saat coding, lalu meledak saat build image atau di pipeline BaTII. Lima aturan berikut mencegah itu.

**Kapitalisasi nama file itu mutlak.** Windows menganggap `SafetyCheck.ts` dan `safetycheck.ts` file yang sama; Linux tidak. Import dengan kapitalisasi keliru jalan mulus di laptopmu lalu gagal di container. Ini penyebab nomor satu dari "di komputer saya jalan kok". Konvensi kita: file Angular kebab-case (`safety-check.component.ts`), file C# PascalCase (`SafetyCheckService.cs`), dan import harus persis sama dengan nama file.

**Akhir baris LF, bukan CRLF.** Skrip shell dan Dockerfile yang terkonversi CRLF akan gagal di container dengan pesan menyesatkan (`command not found` padahal perintahnya ada). Diatasi dengan `.gitattributes` di F0.2, dan harus dibuat **sebelum** commit pertama.

**Path selalu pakai forward slash.** Di konfigurasi, script, dan kode: `apps/sigap-web/src`, bukan `apps\sigap-web\src`. Backslash tidak berarti apa-apa di Linux. Di C#, pakai `Path.Combine()`, jangan gabung string manual.

**Script npm harus lintas platform.** `rm -rf dist` dan `NODE_ENV=production ng build` gagal di PowerShell. Pakai `rimraf` dan `cross-env`, atau konsisten bekerja di Git Bash.

**Verifikasi di container Linux sejak awal, bukan di akhir.** Ini mitigasi paling ampuh: begitu sigap-api bisa dijalankan, bangun dan jalankan image Docker-nya secara lokal (lihat P4.8). Masalah Linux yang ketahuan minggu ini adalah satu file yang perlu di-rename; masalah yang sama ketahuan saat deployment ke BaTII adalah rapat tambahan.

Opsi lain: bekerja sepenuhnya di dalam WSL2, yang menghilangkan empat dari lima masalah di atas sekaligus. Docker Desktop sudah membutuhkannya, jadi setengah jalan sudah terpasang. Tapi registry npm internal Kemenkeu kemungkinan hanya bisa diakses lewat VPN kantor, dan kombinasi VPN korporat dengan jaringan WSL2 termasuk yang sering bermasalah. Untuk sekarang, Windows-native dengan lima aturan di atas lebih aman. Kalau nanti ternyata terlalu banyak friksi, WSL2 selalu bisa dipertimbangkan ulang.

---

# BAGIAN II — URUTAN PENGERJAAN

Centang saat selesai.

**Fase 0 — Persiapan** (1–2 hari)
- [ ] F0.1 Install perangkat
- [ ] F0.2 Siapkan repo dan dokumen
- [ ] F0.3 **Kirim pertanyaan ke BaTII** (Lampiran E) — lakukan hari pertama

**Fase 1 — Rapikan prototipe** (1–2 minggu)
- [ ] P1.1 Terapkan 13 koreksi stakeholder
- [ ] P1.2 Sembunyikan fitur Fase 2
- [ ] P1.3 Pisahkan aturan bisnis dari tampilan

**Fase 2 — Desain & kontrak** (3–5 hari)
- [ ] P2.1 Kontrak API + peta izin
- [ ] P2.2 Inventaris komponen UI

**Fase 3 — Bangun lapisan dummy** (1–2 minggu)
- [ ] P3.1 Dummy design system
- [ ] P3.2 Dummy shell + Native Federation
- [ ] P3.3 Dummy IAM tiga lapis
- [ ] P3.4 Dummy SSO (Keycloak lokal)
- [ ] P3.5 Dummy data master aset
- [ ] P3.6 Dummy notifikasi

**Fase 4 — Bangun aplikasi** (berlangsung lama, berulang)
- [ ] P4.1 Struktur solusi .NET
- [ ] P4.2 EF Core ke 32 tabel
- [ ] P4.3 Setup tooling, lint, CI, AGENTS.md
- [ ] P4.4 Porting aturan bisnis (ulang per domain)
- [ ] P4.5 Endpoint .NET (ulang per endpoint)
- [ ] P4.6 Komponen Angular (ulang per komponen)
- [ ] P4.7 Unit test tambahan
- [ ] P4.8 Verifikasi di container Linux (ulangi berkala, jangan ditunda)

**Fase 5 — Integrasi eksternal**
- [ ] P5.1 BMKG/BNPB + parsing MMI
- [ ] P5.2 Object storage lampiran
- [ ] P5.3 Logika notifikasi

**Fase 6 — Keamanan & kesiapan tukar** (berkala, jangan sekali di akhir)
- [ ] P6.1 Audit kepatuhan kontrak
- [ ] P6.2 Pemeriksa otomatis
- [ ] P6.3 Audit keamanan tiga lapis
- [ ] P6.4 Audit trail
- [ ] P6.5 Definition of done

**Fase 7 — Penukaran** (saat blocker terbuka)
- [ ] P7.1 Tukar satu dummy (ulang per dummy, urutan di Lampiran D)
- [ ] P7.2 Penukaran starter.mfe
- [ ] P7.3 Deployment

---

# BAGIAN III — PROMPT

## P0. Onboarding (jalankan di awal TIAP sesi)
**Model:** Sonnet 5 · **Effort:** medium

```
Repo ini monorepo: remote MFE Angular di apps/sigap-web, microservice .NET 10 di
apps/sigap-api, shell dummy di apps/shell-dummy, library dummy di libs/, dokumen di docs/.

PENTING: sebagian komponen platform Kemenkeu belum bisa diakses, jadi kita pakai versi dummy
yang MENIRU KONTRAKNYA PERSIS supaya nanti tinggal ditukar. Baca docs/PLAYBOOK.md (terutama
Lampiran A soal standar platform) dan DUMMY_REGISTRY.md sebelum menulis kode. Aturan mutlak:
kode aplikasi ditulis seolah-olah platform asli sudah ada. Jangan pernah menulis workaround
yang menyesuaikan diri dengan keterbatasan dummy.

Baca juga AGENTS.md kalau sudah ada, dan dokumen di docs/: UR-SistemMKB.docx,
Catatan-Masukan-Probis.xlsx (sheet "Fitur & Data per Role" dan sheet masukan/Ucob),
Standar-Arsitektur-ICS.pdf, desain-probis-v15.html.

Buatkan ringkasan: scope Fase 1 (8 role aktif, 3 alur proses bisnis inti, fitur in-scope vs
out-of-scope), koreksi stakeholder, dan standar platform yang wajib dipatuhi. Simpan di
MIGRATION_NOTES.md kalau belum ada; kalau sudah ada, baca saja.
```

---

## FASE 0 — PERSIAPAN

### F0.1 Install perangkat

Jalankan **PowerShell sebagai Administrator**:

```powershell
winget install Git.Git
winget install CoreyButler.NVMforWindows
winget install Microsoft.DotNet.SDK.10
winget install Docker.DockerDesktop
winget install Microsoft.VisualStudioCode
```

Tutup dan buka ulang PowerShell, lalu pasang Node (LTS terbaru, jalur Node 24):

```powershell
nvm install lts
nvm use lts
npm install -g @anthropic-ai/claude-code
```

Ekstensi VS Code: C# Dev Kit, Angular Language Service, ESLint, Prettier, Docker. Opsional: DBeaver atau pgAdmin untuk melihat isi database.

Angular CLI belum diperlukan sampai Fase 3.

**Docker Desktop butuh WSL2.** Kalau muncul peringatan saat pertama dijalankan, jalankan `wsl --install` sebagai Administrator lalu restart. Cukup sekali.

**Terminal yang dipakai.** PowerShell cukup untuk menjalankan `claude`, tapi banyak perintah ditulis untuk bash. **Git Bash** (ikut terpasang bersama Git) menerima keduanya dengan lebih ramah — klik kanan di dalam folder repo → "Open Git Bash here". Di VS Code, atur terminal default ke Git Bash lewat dropdown di panel terminal.

Verifikasi (satu per baris — PowerShell 5.1 tidak mengenali `&&`):

```powershell
git --version
node -v
dotnet --version
docker --version
claude --version
```

Kalau ada yang tidak dikenali padahal sudah terpasang, hampir selalu karena terminal belum dibuka ulang.

**.NET 10, bukan 9.** Platform memakai .NET 10 untuk iam.plugin, dan .NET 9 habis masa dukungan November 2026.

### F0.2 Siapkan repo dan dokumen

**Lokasi repo.** Jangan taruh di folder yang disinkronkan OneDrive (Documents, Desktop, atau folder OneDrive). `node_modules` berisi puluhan ribu file kecil; OneDrive akan berusaha menyinkronkan semuanya, membuat sinkronisasi macet, build lambat, dan file terkunci saat npm sedang menulis. Pakai `C:\dev\sigap` atau lokasi lain di luar jangkauan OneDrive.

**Konfigurasi Git untuk Windows** — jalankan sekali, sebelum commit pertama:

```powershell
git config --global core.autocrlf input
git config --global core.longpaths true
```

`core.autocrlf input` membuat file tersimpan dengan akhir baris LF di repo. `core.longpaths` mencegah npm gagal install karena batas path 260 karakter Windows.

**Buat repo:**

```powershell
mkdir C:\dev\sigap
cd C:\dev\sigap
git init
mkdir docs, apps, libs
```

Struktur target:
```
sigap/
├── apps/
│   ├── sigap-web/        # remote MFE Angular
│   ├── sigap-api/        # microservice .NET 10
│   └── shell-dummy/      # shell pengganti sementara
├── libs/
│   ├── keu-ui-dummy/
│   ├── iam-dummy/
│   └── iam-dummy-web/
├── docs/                 # termasuk PLAYBOOK.md ini
├── .gitattributes
├── docker-compose.yml
├── AGENTS.md
└── DUMMY_REGISTRY.md
```

**`.gitattributes`** — buat sekarang, sebelum ada file lain masuk repo. Kalau dibuat belakangan, file yang sudah terlanjur CRLF harus dinormalisasi manual:

```gitattributes
* text=auto eol=lf

*.sh      text eol=lf
Dockerfile text eol=lf
*.yml     text eol=lf
*.yaml    text eol=lf
*.json    text eol=lf
*.ts      text eol=lf
*.html    text eol=lf
*.scss    text eol=lf
*.cs      text eol=lf
*.csproj  text eol=lf

*.cmd     text eol=crlf
*.ps1     text eol=crlf

*.png     binary
*.jpg     binary
*.pdf     binary
*.xlsx    binary
*.docx    binary
```

Rename dokumen saat menyalin ke `docs/`:

| Nama asli | Rename jadi |
| --- | --- |
| `01. Dokumen UR SistemMKB.docx` | `UR-SistemMKB.docx` |
| `Catatan Masukan Probis Sistem MKB.xlsx` | `Catatan-Masukan-Probis.xlsx` |
| `SIGAP - tanggap darurat.html` | `mockup-sigap-tanggap-darurat.html` |
| `SIKUAD_v17.html` | `mockup-sikuad-v17.html` |
| `SIKUAD_Desain_Probis_V15.html` | `desain-probis-v15.html` |
| `Kebutuhan Teknis Deploy SIGAP di Batii.pdf` | `Kebutuhan-Teknis-BaTII.pdf` |
| `Arsitektur_ICS_Keuangan_Slide.pdf` | `Standar-Arsitektur-ICS.pdf` |

`docker-compose.yml` untuk layanan lokal:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: sigap_dev
      POSTGRES_USER: sigap_app
      POSTGRES_PASSWORD: lokal_saja_ganti_ini
    ports: ["5432:5432"]
    volumes: ["pgdata:/var/lib/postgresql/data"]
  redis:
    image: redis:7
    ports: ["6379:6379"]
  minio:
    image: quay.io/minio/minio
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: lokal_saja_ganti_ini
    ports: ["9000:9000", "9001:9001"]
  keycloak:
    image: quay.io/keycloak/keycloak:26.0
    command: start-dev --http-port=8081
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: lokal_saja_ganti_ini
    ports: ["8081:8081"]
    profiles: ["sso"]
volumes:
  pgdata:
```

Keycloak sengaja di port 8081, bukan 8080, karena 8080 dipakai sigap-api. Keycloak juga ditaruh di profile terpisah supaya tidak ikut jalan otomatis — dia memakan sekitar 1 GB RAM dan hanya diperlukan saat menguji alur login:

```powershell
docker compose up -d                    # tanpa Keycloak
docker compose --profile sso up -d      # dengan Keycloak
```

Commit pertama:

```powershell
git add .
git commit -m "chore: setup repo, playbook, dan dokumen sumber"
```

### F0.3 Kirim pertanyaan ke BaTII

**Lakukan hari pertama.** Waktu tunggunya di luar kendalimu, dan semakin cepat jawaban datang semakin kecil dummy yang terlanjur dibangun salah. Daftar lengkap di Lampiran E.

---

## FASE 1 — RAPIKAN PROTOTIPE

Kenapa ini duluan: 13 koreksi stakeholder belum diterapkan, jadi tim SOPB sekarang menguji versi yang sudah diketahui salah — dan kamu akan membangun sistem baru berdasar desain yang belum tervalidasi.

### P1.1 Terapkan 13 koreksi stakeholder
**Model:** Sonnet 5 · **Effort:** high

```
Terapkan 13 koreksi hasil review stakeholder di Lampiran C docs/PLAYBOOK.md ke prototipe
Next.js yang sedang berjalan. Sumber aslinya docs/Catatan-Masukan-Probis.xlsx (sheet masukan
& Ucob). Koreksi ini LEBIH BARU daripada mockup HTML — kalau berbeda, koreksi yang menang.

Kerjakan bertahap, satu koreksi per commit, laporkan tiap selesai.

Kalau ada koreksi yang menuntut perubahan struktur tabel database, JANGAN langsung ubah —
laporkan dulu ke saya, karena struktur 32 tabel ini dipakai apa adanya di sistem final.
```

### P1.2 Sembunyikan fitur Fase 2
**Model:** Sonnet 5 · **Effort:** medium

```
Prototipe masih menampilkan fitur Fase 2 yang belum masuk scope. Sembunyikan (hide, jangan
hapus kodenya) hal berikut supaya uji coba tim SOPB fokus:
- Role Tim Pengembang Dokumen MKB dan Tim Implementasi RKB
- Menu dokumen MKB: ARKB, ADB, SKB, RTDB, RKBU, RPKK
- Menu "Panduan & Dok. MKB" pada role Pegawai Umum
- Fitur komunikasi kebencanaan dan eksekusi RKB

Pakai flag konfigurasi supaya gampang dinyalakan lagi di Fase 2, jangan comment-out kode.
Urutkan menu sidebar semua role: Pra-Bencana dulu, baru Saat Bencana.
```

### P1.3 Pisahkan aturan bisnis dari tampilan
**Model:** Sonnet 5 · **Effort:** high

```
Prototipe ini akan jadi rujukan utama saat migrasi, jadi strukturnya perlu jelas dulu.
Kumpulkan seluruh logika bisnis (perhitungan, validasi, aturan status, penentuan lingkup
data) ke satu folder terpisah yang tidak mengimpor apa pun dari React/Next.js.

Jangan mengubah perilaku apa pun — ini murni refactor. Setelah selesai, buat daftar file
hasil pemisahan beserta ringkasan satu kalimat isi tiap file, simpan di
BUSINESS_RULES_INDEX.md. Daftar itu jadi peta porting ke C# nanti.
```

Ini prompt dengan nilai tertinggi di seluruh playbook. Kebutuhan Teknis menyebut logika bisnis saat ini "menyatu dengan tampilan", dan itulah yang membuat porting mahal. Memisahkannya sekarang di JavaScript (bahasa yang kamu sudah kenal) jauh lebih murah daripada memisahkannya sambil sekaligus menerjemahkan ke C#.

---

## FASE 2 — DESAIN & KONTRAK

Artefak di fase ini bukan kode, tapi tanpanya Fase 4 jadi tebak-tebakan. Dua dokumen hasilnya juga yang nanti kamu kirim ke BaTII untuk pendaftaran aturan IAM.

### P2.1 Kontrak API + peta izin
**Model:** Opus 5 · **Effort:** xhigh

```
Rancang kontrak REST API sigap-api mengikuti PERSIS matriks di
docs/Catatan-Masukan-Probis.xlsx sheet "Fitur & Data per Role", kode fitur Fase Tanggap
Darurat:

2.1 Safety Check/SOS — Pegawai Umum: tambah/simpan respons, lihat riwayat sendiri
2.2 Laporkan Potensi Bencana — Pegawai Umum: tambah/simpan laporan, lihat riwayat sendiri
2.3 Trigger Safety Check Unit — Tim Satgas (unit), Kepala Perwakilan (wilayah provinsi),
    Subkoordinator (Eselon I), Koordinator MKB (nasional); semua bisa lihat riwayat
2.4 Verifikasi Alert Bencana — Tim Satgas: lihat laporan masuk, approve/reject
2.5 Asesmen Kondisi Bencana — Tim Satgas: tambah/simpan/ubah 5 aspek (SDM, Aset, TIK, Arsip,
    Layanan); Pimpinan Satker: read only + approve, TANPA re-entry jenis bencana/lokasi
2.6 Dashboard Monitor SC & Sumber Daya — Kepala Perwakilan/Subkoordinator/Koordinator MKB/
    Sekretaris Jenderal: read only + lihat detail, filter sesuai lingkup

Untuk tiap endpoint tentukan: method, URL, request/response shape, plus tiga hal yang nanti
diserahkan ke platform (lihat Lampiran A soal keamanan 3 lapis):
- permission string format app:resource:action (mis. sigap:asesmen:approve)
- aturan Scope: kolom dan kondisi yang memfilter baris untuk tiap role
- field yang perlu Sieve (masking) untuk role tertentu

Lingkup data per role ada di Lampiran B. Role berstatus "-" di matriks tidak boleh punya
akses sama sekali; role "Read Only" tidak boleh diberi endpoint tulis.

Kebutuhan Teknis memperkirakan 60-80 endpoint; itu perkiraan, bukan target yang dikejar.
Untuk struktur menu dan data per role lihat juga docs/desain-probis-v15.html.

Simpan hasilnya sebagai API_CONTRACT.md dan PERMISSION_MAP.md. Tampilkan ringkasannya untuk
saya review sebelum disimpan.
```

### P2.2 Inventaris komponen UI
**Model:** Sonnet 5 · **Effort:** medium

```
Inventarisasi 69 komponen React di prototipe: nama, fungsi, dipakai di halaman mana, dan
seberapa sering dipakai ulang. Kelompokkan jadi tiga:
(a) kemungkinan besar sudah ada padanannya di design system Kemenkeu (tabel, kartu statistik,
    header halaman, form field standar)
(b) khusus SIGAP, harus dibangun sendiri (mis. peta sebaran, widget rekap safety check per
    kondisi)
(c) sebaiknya dihapus karena tidak dipakai atau duplikatif

Simpan sebagai COMPONENT_INVENTORY.md. Jangan menulis kode Angular apa pun.
```

---

## FASE 3 — BANGUN LAPISAN DUMMY

**Prinsip:** yang ditiru adalah **kontraknya**, bukan cara kerjanya. Nama paket, nama atribut, nama directive, nama class SCSS, bentuk claim token — semuanya persis seperti dokumentasi platform. Isinya boleh sederhana sekali.

Kalau dummy dibuat seenaknya (`[CekIzin("approve_asesmen")]` alih-alih `[KemenkeuAuthorize("sigap:asesmen:approve")]`), kamu tidak sedang membuat dummy, kamu sedang membuat utang teknis.

**Lima aturan dummy:**
1. Karantina — semua dummy di `libs/*-dummy/` atau `apps/shell-dummy/`, terpisah dari kode aplikasi
2. Kontrak identik — nama dan signature mengikuti dokumentasi platform
3. Isi sesederhana mungkin — dummy tidak perlu benar, hanya perlu berjalan
4. Tidak boleh naik ke production — build production gagal kalau dummy masih terpasang
5. Tercatat di `DUMMY_REGISTRY.md` (format di Lampiran D)

### P3.1 Dummy design system
**Model:** Opus 5 · **Effort:** high

```
Buat library dummy pengganti @danarakca/keu-ui di libs/keu-ui-dummy.

KUNCI PENUKARAN: petakan alias path "@danarakca/keu-ui" ke library dummy ini lewat tsconfig
paths. Dengan begitu seluruh kode aplikasi mengimpor dengan nama paket ASLI, dan saat paket
sungguhan tersedia kita cukup menghapus satu baris alias lalu npm install — tanpa mengubah
satu pun import di kode aplikasi.

Isi library:
- File _index.scss yang mengekspos token, supaya `@use 'index' as *` bekerja persis seperti
  di platform. Token warna resmi: Kemenkeu Navy #003d7a, Blue #275EA8, Gold #FCB332. Tambahkan
  token turunan yang wajar (surface, border, text, status sukses/peringatan/bahaya) dan TANDAI
  di komentar bahwa nilai turunan ini asumsi kita, bukan dokumentasi resmi.
- Katalog komponen SCSS dengan nama class yang disebut slide: .page-header, .stats-row,
  .table-card. Tambahkan yang jelas dibutuhkan SIGAP (form field, button, badge status, modal,
  tab) dengan penamaan bergaya sama, tandai sebagai asumsi.
- Komponen Angular pembungkus seperlunya saja.

Semua nilai dan nama yang kita tebak WAJIB dicatat di DUMMY_REGISTRY.md sebagai asumsi yang
perlu diverifikasi ke BaTII.

Kode aplikasi dilarang hardcode warna dan dilarang mendefinisikan komponen visualnya sendiri,
sesuai standar platform.
```

### P3.2 Dummy shell + Native Federation
**Model:** Opus 5 · **Effort:** high

```
Buat shell dummy di apps/shell-dummy sebagai pengganti sementara shell ICS Keuangan, dan
konfigurasikan apps/sigap-web sebagai remote module di atasnya.

Identitas remote (PROVISIONAL, diganti saat BaTII menetapkan yang resmi). Turunkan sesuai
Golden Rule platform dari remoteName `remoteSigapBencana`:
- element: remote-sigap-bencana-element
- function: defineRemoteSigapBencanaElement
- selector: app-remote-sigap-bencana-entry
- route path: /sigap-bencana
- display name: SIGAP Bencana
- port lokal: 4299 (sengaja di luar deret 4200-an milik platform supaya jelas sementara)

Taruh SELURUH nilai di atas di satu file konfigurasi terpusat, jangan tersebar. Penukaran
nanti harus cukup mengubah satu file.

Shell dummy wajib ikut aturan emas platform: sangat ringan, hanya mengurus sidebar,
otentikasi, dan routing. DILARANG ada logika bisnis di dalamnya.

Routing di remote WAJIB path relatif dan flat. DILARANG nested routing. Router remote sinkron
dua arah dengan URL browser.
```

### P3.3 Dummy IAM tiga lapis
**Model:** Opus 5 · **Effort:** high

```
Buat dummy pengganti iam.plugin. Ini dummy paling penting: kalau kontraknya benar, saat plugin
asli tiba kode aplikasi tidak berubah sama sekali.

Backend (.NET), di libs/iam-dummy:
- Atribut [KemenkeuAuthorize("app:resource:action")] dengan nama dan signature PERSIS seperti
  platform. Implementasinya membaca permission dari token dan menolak 403 kalau tidak cocok.
- Interface ICurrentUserContext: identitas, daftar permission, dan lingkup data (unit, wilayah
  provinsi, Eselon I, atau nasional).
- Mekanisme Scope: helper yang menyisipkan kondisi filter ke query EF Core. Filter WAJIB masuk
  klausa WHERE di database, BUKAN menyaring hasil di memori. Kalau dummy-nya menyaring di
  memori, kita terbiasa dengan pola yang salah dan gagal saat audit VT.
- Mekanisme Sieve: attribute/konvensi menandai field yang di-null-kan di response untuk role
  tertentu.

Frontend (Angular), di libs/iam-dummy-web:
- Directive *hasPermission dengan nama PERSIS seperti platform, menyembunyikan elemen kalau
  pengguna tidak punya permission yang diminta.

Kebijakan izin (peta role → permission → scope) disimpan sebagai DATA di file JSON, bukan
hardcode, sesuai standar platform. File itu jadi bahan pendaftaran aturan IAM nanti.

DILARANG membuat sistem login, tabel user, atau manajemen sesi sendiri.
```

### P3.4 Dummy SSO (Keycloak lokal)
**Model:** Opus 5 · **Effort:** high

```
Konfigurasikan Keycloak lokal (sudah ada di docker-compose, port 8081, profile "sso")
sebagai pengganti SSO Kemenkeu. Konfigurasinya meniru persis apa yang kita minta ke BaTII di
Kebutuhan Teknis, sehingga dummy ini sekaligus berfungsi sebagai spesifikasi yang bisa
ditunjukkan ke mereka.

- Realm: kemenkeu
- Dua client: sigap-web-dev (public, remote module) dan sigap-api-dev (confidential,
  microservice), audience sigap-api
- Claim sesuai permintaan di Kebutuhan Teknis: nip (atau preferred_username), kode_satker,
  kode_eselon1, groups
- Masa berlaku: access token 15 menit, refresh token 8 jam
- Sepuluh akun uji, satu tiap rule, dengan grup: sigap-pegawai, sigap-satgas, sigap-pimpinan,
  sigap-perwakilan, sigap-subkoordinator, sigap-koordinator, sigap-sekjen, sigap-admin,
  sigap-pengembang, sigap-impl-rkb

Lingkup data tiap role ada di Lampiran B. Dua akun terakhir dibuat untuk kelengkapan Fase 2
tapi TIDAK diberi permission atau menu apa pun di Fase 1.

Kredensial akun uji adalah nilai lokal sembarangan, taruh di .env yang tidak masuk git. Jangan
pernah memakai nilai yang mirip kredensial asli.
```

### P3.5 Dummy data master aset
**Model:** Sonnet 5 · **Effort:** medium

```
Buat seeder data master gedung kantor untuk development, dari salinan xlsx berisi 1.431 gedung
kantor di [path file salinan SIMAN].

Koordinat gedung BELUM tersedia dari SIMAN dan itu satu-satunya penghalang fitur peta.
Generate koordinat dummy yang masuk akal (tersebar di wilayah provinsi yang sesuai) supaya
fitur peta bisa dikembangkan dan diuji.

WAJIB: setiap baris dengan koordinat generate diberi penanda kolom is_koordinat_dummy = true,
dan tampilkan banner peringatan di UI peta selama masih ada koordinat dummy. Untuk aplikasi
tanggap darurat, koordinat palsu yang terlanjur dianggap asli bisa berarti tim dikirim ke
lokasi yang salah.

Seeder hanya jalan di environment development, tidak pernah di production.
```

### P3.6 Dummy notifikasi
**Model:** Opus 5 · **Effort:** high

```
Status izin Web Push di domain platform belum dijawab BaTII, dan belum jelas apakah platform
menyediakan layanan notifikasi bersama di antara 20 layanan data/API-nya.

Rancang abstraksi channel notifikasi (interface/strategy) dengan tiga implementasi:
- dummy console/log untuk development
- in-app notification + polling sebagai fallback realistis
- Web Push, disiapkan tapi dinonaktifkan lewat konfigurasi

Pemilihan channel lewat konfigurasi, bukan hardcode.
```

---

## FASE 4 — BANGUN APLIKASI

Mulai titik ini, kode ditulis seolah-olah platform asli sudah ada. Prompt di bagian ini sengaja tidak menyebut kata "dummy".

### P4.1 Struktur solusi .NET
**Model:** Opus 5 · **Effort:** high

```
Buat solusi .NET 10 untuk apps/sigap-api dengan Clean Architecture (API, Application, Domain,
Infrastructure), di dalam tiap layer dikelompokkan per domain: SDM, Aset, TIK, Arsip, Layanan
(5 aspek asesmen), plus modul inti Auth dan Notifikasi. Jangan satu controller/service besar
yang menangani semua aspek.

JANGAN menulis ulang logika keamanan di sigap-api — validasi didelegasikan ke iam.plugin
terpusat (lihat Lampiran A). Rancang bagaimana integrasinya.

Tentukan konvensi penamaan (PascalCase kelas/method), .editorconfig + analyzer .NET, dan
dokumentasi OpenAPI/Swagger otomatis. Petakan bagaimana ~3.700 baris aturan bisnis yang sudah
dipisahkan di BUSINESS_RULES_INDEX.md akan didistribusikan ke domain di atas.

Jangan menulis kode dulu — tampilkan rancangan strukturnya untuk saya review.
```

### P4.2 EF Core ke 32 tabel
**Model:** Opus 5 · **Effort:** high

```
Baca struktur 32 tabel PostgreSQL dari [path file migration/schema di repo prototipe]. Jangan
minta saya menempelkan connection string berisi kredensial — baca dari file di repo.

Implementasikan EF Core entity classes dan DbContext yang memetakan langsung ke skema itu
tanpa mengubah struktur tabel (schema-first, bukan code-first). Kelompokkan entity per domain.
Sambungkan ke PostgreSQL lokal via docker compose.

Tandai juga, dalam dokumen terpisah (bukan kode): kolom mana yang dipakai memfilter baris per
lingkup role (kandidat Scope), dan field mana yang perlu di-mask untuk sebagian role, terutama
koordinat dan keberadaan pegawai (kandidat Sieve). Bandingkan dengan PERMISSION_MAP.md.
```

### P4.3 Tooling, lint, CI, AGENTS.md
**Model:** Sonnet 5 · **Effort:** medium

```
Setup dasar kualitas kode:

1. AGENTS.md di root dan di apps/sigap-web. Platform ICS mensyaratkan setiap remote MFE punya
   file ini, dibaca AI coding tool sebelum menulis kode. Isinya: identitas remote dan
   turunannya, aturan routing (relatif, flat, dilarang nested), aturan styling (@use 'index'
   as *, katalog komponen, larangan hardcode warna, token Navy #003d7a / Blue #275EA8 /
   Gold #FCB332), aturan keamanan ([KemenkeuAuthorize], *hasPermission, Scope di query, Sieve,
   larangan menulis logika keamanan sendiri), batasan scope Fase 1, dan larangan spesifik
   proyek (jangan porting fitur Fase 2, jangan ubah struktur 32 tabel, jangan taruh logika
   bisnis di shell).

   WAJIB sertakan juga bagian lintas platform, karena development di Windows tapi production
   di container Linux: kapitalisasi nama file dan import harus persis sama (Linux
   case-sensitive, Windows tidak); akhir baris LF; path pakai forward slash di semua
   konfigurasi, script, dan kode; di C# pakai Path.Combine(), jangan gabung string path
   manual; script npm harus lintas platform.
2. apps/sigap-web: ESLint + Prettier + Husky/lint-staged. Pasang rimraf dan cross-env supaya
   script npm jalan di PowerShell maupun bash — jangan tulis `rm -rf` atau
   `NODE_ENV=production ng build` langsung di package.json.
3. apps/sigap-api: .editorconfig (end_of_line = lf), analyzer .NET, Swagger otomatis.
4. Lint rule yang menolak warna hex di SCSS di luar file token.
5. CI dengan path-filtering: pipeline sigap-web hanya trigger kalau ada perubahan di
   apps/sigap-web/**, sigap-api hanya kalau ada perubahan di apps/sigap-api/**. CI berjalan di
   Linux, jadi sekaligus berfungsi sebagai deteksi dini masalah kapitalisasi dan akhir baris.
6. CONTRIBUTING.md berisi konvensi tim dan format commit (Conventional Commits).
```

### P4.4 Porting aturan bisnis (ulang per domain)
**Model:** Sonnet 5 untuk fungsi rutin, Opus 5 untuk yang kompleks · **Effort:** medium/high

```
Pakai BUSINESS_RULES_INDEX.md sebagai peta. Port fungsi [nama fungsi] dari prototipe ke C# di
layer Domain/Application yang sesuai.

Logikanya harus identik termasuk edge case dan validasi. Kalau ada bagian yang sebenarnya
kontrol akses (menentukan siapa boleh melihat/mengubah apa), JANGAN diporting sebagai kode
bisnis — catat di ACCESS_RULES.md untuk diterjemahkan ke permission/Scope/Sieve, dan beri
tahu saya.

Tulis unit test yang membandingkan output fungsi lama dan baru untuk beberapa skenario data
uji. Test inilah jaring pengaman utama bahwa migrasi tidak mengubah perilaku.
```

Jalankan berulang, satu domain per sesi, urut dari yang paling sedikit dependensinya.

### P4.5 Endpoint .NET (ulang per endpoint)
**Model:** Sonnet 5 · **Effort:** medium

```
Buatkan endpoint [nama endpoint] sesuai kontrak di API_CONTRACT.md. Taruh di domain/folder
yang sesuai, bukan controller umum. Wajib:
- atribut [KemenkeuAuthorize("[permission string dari PERMISSION_MAP.md]")]
- aturan Scope diterapkan di query database (klausa WHERE), bukan filter di memori
- Sieve untuk field sensitif sesuai kontrak
- validasi input, error handling lewat middleware terpusat (bukan try-catch lokal)
- dokumentasi OpenAPI (summary dan response type)
- unit test, termasuk kasus role yang TIDAK berhak mengakses endpoint ini
```

### P4.6 Komponen Angular (ulang per komponen)
**Model:** Sonnet 5 · **Effort:** medium

```
Buatkan komponen Angular [nama komponen] yang menggantikan halaman [nama halaman] di
prototipe. Taruh di feature module yang sesuai (safety-check/, verifikasi-alert/,
asesmen-bencana/, atau dashboard/). Wajib:
- pakai katalog komponen dari @danarakca/keu-ui dan token lewat @use 'index' as *
- TANPA hardcode warna dan TANPA membuat komponen visual sendiri di luar katalog
- elemen yang tidak berhak diakses dibungkus directive *hasPermission
- routing flat dan path relatif, tanpa nested routing
- penamaan file kebab-case, JSDoc untuk logika non-trivial
- hubungkan ke endpoint [nama endpoint] di sigap-api

Kalau ada kebutuhan visual yang tidak tercakup katalog, JANGAN bikin sendiri di kode aplikasi
— laporkan ke saya supaya ditambahkan ke katalog dan dicatat sebagai kebutuhan yang perlu
dikonfirmasi ke BaTII.
```

Poin terakhir penting: setiap kebutuhan visual yang tidak tercakup adalah informasi berharga. Entah katalog asli punya padanan yang belum kita tahu, atau ini komponen khusus SIGAP yang harus diajukan. Keduanya perlu tercatat, bukan diam-diam ditambal di halaman.

### P4.7 Unit test tambahan
**Model:** Sonnet 5 · **Effort:** medium

```
Tinjau [nama file/fungsi], tambahkan unit test untuk skenario yang belum tercakup: kondisi
batas, input tidak valid, dan pelanggaran batas lingkup data antar role (pastikan Scope
benar-benar memblokir akses lintas unit/wilayah/Eselon I).
```

### P4.8 Verifikasi di container Linux
**Model:** Sonnet 5 · **Effort:** medium

Jalankan segera setelah sigap-api bisa dijalankan, lalu ulangi setiap beberapa hari. Jangan ditunda sampai Fase 7.

```
Development kita di Windows, tapi production berjalan di container Linux milik BaTII.
Buatkan Dockerfile sementara untuk apps/sigap-api dan apps/sigap-web supaya kita bisa
menjalankan keduanya di container Linux secara lokal dan menangkap masalah lintas platform
sejak dini.

Ini versi kerja untuk verifikasi, bukan versi production — nanti diganti/dibandingkan dengan
Dockerfile standar dari starter.mfe. Base image Linux, build context scoped ke folder app
masing-masing.

Tambahkan juga script npm/PowerShell sederhana yang: build kedua image, jalankan, dan
laporkan kalau ada yang gagal.

Kalau build gagal, kemungkinan besar penyebabnya salah satu dari ini — periksa berurutan:
kapitalisasi nama file atau import yang tidak cocok, akhir baris CRLF di file skrip, atau
path yang memakai backslash. Laporkan temuan beserta file yang perlu diperbaiki.
```

Masalah Linux yang ketahuan minggu ini adalah satu file yang perlu di-rename. Masalah yang sama ketahuan saat deployment ke BaTII adalah rapat tambahan dan penundaan jadwal.

---

## FASE 5 — INTEGRASI EKSTERNAL

### P5.1 BMKG/BNPB + parsing MMI
**Model:** Opus 5 · **Effort:** high

Aturan bisnis paling kritis di seluruh sistem: salah parsing berarti Safety Check tidak terpicu saat gempa sungguhan.

```
Buat service di sigap-api (domain Integrasi/Notifikasi) yang menarik data dari
data.bmkg.go.id (autogempa.json dan gempadirasakan.json), www.bmkg.go.id (peringatan dini
cuaca CAP), dan data.bnpb.go.id (rekap kejadian bencana), diperbarui tiap 5 menit.

ATURAN BISNIS TRIGGER OTOMATIS:
- Parsing field "Dirasakan" untuk mengekstrak nilai MMI per wilayah. Formatnya tidak baku,
  contoh nyata: "III-IV Cianjur, II-III Kota Sukabumi".
- Broadcast Safety Check otomatis dipicu HANYA jika MMI suatu wilayah mencapai V, ke seluruh
  pegawai unit kerja di wilayah tersebut, tanpa persetujuan manual dari role mana pun.
- MMI di bawah V dicatat sebagai referensi tapi TIDAK memicu broadcast.
- Unit test untuk variasi format string "Dirasakan": rentang (III-IV), nama wilayah berspasi,
  banyak wilayah dalam satu string, respons kosong/malformed.

Simpan beberapa respons asli BMKG sebagai file fixture di repo — format aslinya tidak
terdokumentasi rapi, jadi contoh nyata lebih berharga daripada asumsi.

Batas rate BMKG 60 permintaan per menit per IP. Wajib cantumkan atribusi BMKG sesuai ketentuan
data terbuka mereka.

Cache di Redis dengan prefix "sigap:". HANYA data publik BMKG/BNPB — jangan simpan data
pengguna, keberadaan, atau koordinat pegawai. Fallback ke cache terakhir kalau API eksternal
down, log kejadiannya secara terstruktur.
```

### P5.2 Object storage lampiran
**Model:** Sonnet 5 · **Effort:** medium

```
Buat service upload/download lampiran ke object storage berprotokol S3 di layer
Infrastructure, sambungkan ke MinIO lokal. Jenis lampiran Fase 1: foto/video/rekaman suara
pada Laporan Potensi Bencana, dan foto kerusakan pada Asesmen Kondisi Bencana. (Dokumen MKB
dan berita acara baru relevan di Fase 2.)

Validasi: tipe file jpg, png, pdf, docx, xlsx; batas 10 MB per file. Prototipe sudah mendukung
S3 di [path kode upload existing] — tiru konfigurasinya, jangan tulis ulang dari nol.

Jangan buat URL publik yang bisa diakses tanpa autentikasi; akses download melewati pengecekan
permission dan Scope.
```

### P5.3 Logika notifikasi
**Model:** Opus 5 · **Effort:** high

```
Implementasikan logika broadcast Safety Check di atas abstraksi channel yang sudah dibuat.
Bagian ini ditulis sungguhan dan diuji karena tidak tergantung channel:
- penentuan penerima berdasarkan lingkup yang dipicu
- deduplikasi kalau beberapa role memicu untuk lingkup yang beririsan (lihat koreksi no. 12 di
  Lampiran C)
- pegawai offline saat broadcast dikirim: notifikasi tetap muncul saat kembali online
Unit test untuk ketiga kasus.
```

---

## FASE 6 — KEAMANAN & KESIAPAN TUKAR

Jalankan berkala, jangan sekali di akhir. Pelanggaran kontrak jauh lebih murah diperbaiki saat masih satu-dua file.

### P6.1 Audit kepatuhan kontrak
**Model:** Opus 5 · **Effort:** high

```
Audit seluruh kode aplikasi (di luar libs/*-dummy/ dan apps/shell-dummy/), cari pelanggaran:

- import yang menunjuk langsung ke library dummy, bukan nama paket resmi
- warna hex hardcode di SCSS/HTML di luar library design system
- komponen visual yang didefinisikan di kode aplikasi, bukan diambil dari katalog
- pengecekan izin manual (if role == "...") yang menduplikasi [KemenkeuAuthorize] atau
  *hasPermission
- filter lingkup data yang dilakukan di memori setelah query, bukan di klausa WHERE
- nested routing atau path absolut di konfigurasi route
- referensi ke Keycloak lokal, port 4299, atau nilai provisional lain yang bocor ke kode
  aplikasi alih-alih tinggal di file konfigurasi terpusat

Untuk tiap temuan: lokasi, kenapa itu masalah saat penukaran, dan perbaikannya. Urutkan dari
yang paling menyulitkan penukaran.
```

### P6.2 Pemeriksa otomatis
**Model:** Sonnet 5 · **Effort:** medium

```
Buat pemeriksaan otomatis di CI:
- build production GAGAL kalau library dummy masih ter-resolve (cek alias tsconfig dan
  referensi paket .NET)
- lint rule menolak warna hex di SCSS di luar file token
- lint rule menolak import langsung ke libs/*-dummy dari kode aplikasi
- startup banner mencolok di development yang menampilkan daftar dummy yang sedang aktif,
  supaya tidak ada yang lupa bahwa keamanan belum sungguhan
- skrip yang mencetak isi DUMMY_REGISTRY.md beserta status tiap dummy

Pemeriksaan lintas platform (development Windows, production Linux):
- deteksi import yang kapitalisasinya tidak cocok dengan nama file sebenarnya
- deteksi file berakhir baris CRLF yang seharusnya LF (skrip, Dockerfile, yml, kode sumber)
- deteksi path dengan backslash di file konfigurasi dan script
- jalankan build image Docker di CI, karena CI berjalan di Linux dan akan menangkap masalah
  yang tidak muncul di laptop Windows
```

### P6.3 Audit keamanan tiga lapis
**Model:** Opus 5 · **Effort:** high

```
Audit apps/sigap-api dan apps/sigap-web terhadap standar 3 lapis platform:

Lapis 1 — SETIAP endpoint punya [KemenkeuAuthorize] dengan permission string yang benar; tidak
ada endpoint lolos tanpa proteksi. Di UI, elemen sensitif dibungkus *hasPermission.
Lapis 2 — Scope difilter di query database, bukan di memori setelah data diambil. Cari query
yang mengambil semua baris lalu menyaring di aplikasi; itu pelanggaran.
Lapis 3 — Sieve diterapkan pada field yang seharusnya di-mask.

Periksa juga: validasi input di semua endpoint, penanganan secret (tidak ada hardcode),
security header, dan tidak ada logika keamanan buatan sendiri yang menduplikasi iam.plugin.

VERIFIKASI KHUSUS data keberadaan dan koordinat pegawai, data paling sensitif di aplikasi ini:
tidak bocor lintas lingkup unit/wilayah/Eselon I, tidak ikut ter-log, tidak masuk cache Redis,
dan di-Sieve untuk role yang hanya butuh angka agregat.

Daftar temuan beserta tingkat keparahan, urutkan dari yang paling kritis.
```

### P6.4 Audit trail
**Model:** Opus 5 · **Effort:** high

```
Platform ICS mensyaratkan jejak audit persisten untuk setiap pembuatan, pengubahan, DAN akses
data. Kebutuhan Teknis juga menjadikannya syarat kelulusan VT.

Pastikan aksi berikut tercatat: trigger Safety Check (siapa, lingkup apa, kapan), respons
pegawai, verifikasi/penolakan alert oleh Satgas, submit dan update asesmen, approve/pending
oleh Pimpinan. Catat aktor, waktu, nilai sebelum/sesudah. Pakai pola terpusat
(interceptor/middleware), bukan manual di tiap endpoint.

Koordinat pegawai dicatat HANYA kalau memang bagian dari data yang berubah, dan aksesnya tetap
dibatasi Scope.

Catatan: saat iam.plugin asli tiba, cek apakah audit trail sudah tersedia bawaan. Kalau ya,
punya kita diganti dengan milik platform.
```

### P6.5 Definition of done
**Model:** Sonnet 5 · **Effort:** medium

```
Buatkan checklist "definition of done" per modul: unit test lulus, lint bersih, tidak ada
hardcode warna, semua endpoint punya [KemenkeuAuthorize], Scope terverifikasi di level query,
Sieve diterapkan pada field sensitif, dokumentasi OpenAPI terisi, audit trail tercatat, routing
flat tanpa nested, komponen memakai katalog SCSS. Saya pakai ini sebagai acuan review internal
sebelum submit ke BaTII.
```

---

## FASE 7 — PENUKARAN

Tukar satu per satu, jangan sekaligus. Urutan dan perkiraan beban di Lampiran D.

### P7.1 Tukar satu dummy
**Model:** Opus 5 · **Effort:** high

```
Tukar dummy [nama dummy] dengan komponen platform yang asli.

1. Bandingkan dulu kontrak dummy kita dengan dokumentasi/paket asli. Buat daftar selisihnya
   sebelum mengubah apa pun, tunjukkan ke saya.
2. Pasang komponen asli, cabut alias/registrasi yang menunjuk ke dummy.
3. Jalankan seluruh test. Test yang gagal menunjukkan di mana asumsi dummy kita meleset.
4. Perbaiki kode aplikasi HANYA di titik yang memang berbeda kontraknya.
5. Hapus folder dummy yang sudah tidak terpakai, perbarui DUMMY_REGISTRY.md.

Kalau selisihnya besar dan menyentuh banyak file, berhenti dan laporkan dulu.
```

### P7.2 Penukaran starter.mfe
**Model:** Opus 5 · **Effort:** xhigh

```
Kita sudah punya apps/sigap-web hasil scaffold sendiri. Sekarang starter.mfe resmi sudah bisa
diakses.

JANGAN langsung menimpa. Clone starter.mfe ke folder terpisah, bandingkan dengan scaffold
kita: struktur folder, konfigurasi federation, build config, Dockerfile, AGENTS.md, setup
lint, dan mekanisme audit trail bawaan.

Buat daftar perbedaannya. Untuk tiap perbedaan tentukan: ikut starter.mfe (default, karena itu
standar resmi) atau pertahankan punya kita (hanya kalau ada alasan khusus SIGAP yang kuat).
Tunjukkan daftar itu sebelum eksekusi.

Rencana yang benar biasanya: pindahkan kode fitur kita ke dalam struktur starter.mfe, bukan
menambal starter.mfe agar cocok dengan struktur kita.
```

### P7.3 Deployment
**Model:** Opus 5 · **Effort:** high

```
Cek dulu apakah starter.mfe sudah membawa Dockerfile dan manifest standar platform — kalau ya,
ikuti itu, jangan buat sendiri.

Kalau belum: Dockerfile untuk apps/sigap-web (Angular hasil build, file statis, port 8080,
health check GET /healthz) dan apps/sigap-api (.NET 10, port 8080, health check GET
/health/live dan GET /health/ready). Build context masing-masing scoped ke folder app-nya
sendiri meski satu repo.

Manifest deployment untuk namespace sigap-dev, spesifikasi resource sesuai lampiran UR:
sigap-web request 50m CPU/128Mi, limit 200m/256Mi, 2 replika; sigap-api request 300m CPU/512Mi,
limit 1000m/1Gi, 2 replika. Kedua container tidak butuh persistent volume.

Buat juga .env.example per app TANPA nilai asli — hanya nama variabel dan deskripsi, siap
diisi dari vault.

Pastikan remote terdaftar di registry modul platform sesuai tata kelola ICS, supaya kill-switch
terpusat berlaku untuk modul ini.
```

---

# LAMPIRAN

## Lampiran A — Standar platform ICS Keuangan

Wajib dipatuhi, jangan diimprovisasi. Sumber: `docs/Standar-Arsitektur-ICS.pdf`.

**Posisi SIGAP.** ICS Keuangan adalah pondasi bersama; di atasnya ada Core APBN (perencanaan, pelaksanaan, pelaporan anggaran) dan Non Core APBN (HRIS, persuratan, perjadin). SIGAP adalah satu remote module di antara 22 modul tampilan, dengan satu microservice di antara 20 layanan data. Komponen bersama yang dipakai ulang: Shell & SSO, IAM 3 lapis, Design System, 20 layanan data/API, gudang data.

**Scaffolding.** Modul baru dimulai dengan clone `starter.mfe`, bukan `ng new`. Template sudah membawa standar keamanan, struktur, dan konfigurasi federation.

**Golden Rule penamaan.** Dari satu `remoteName`, seluruh identitas diturunkan. Contoh platform dengan `remoteUserManagement`:

| Turunan | Nilai |
| --- | --- |
| Element name | `remote-user-management-element` |
| Function | `defineRemoteUserManagementElement` |
| Selector | `app-remote-user-management-entry` |
| Route path | `/user-management` |
| Display name | `User Management` |

Port dialokasikan rapi (4200 Shell, 4201 Dashboard, dst). Nama remote dan port SIGAP diminta ke BaTII.

**Routing.** Path relatif, flat routes, dilarang nested routing. Remote punya router sendiri tapi sinkron dua arah dengan URL browser (Silent Location Strategy). Shell harus sangat ringan, tidak boleh ada logika bisnis.

**Styling.** Token dipanggil dengan `@use 'index' as *`. Pakai katalog komponen SCSS platform (`.page-header`, `.stats-row`, `.table-card`, dll). Dilarang membuat komponen sendiri yang menduplikasi katalog, dilarang hardcode warna. Token: Navy `#003d7a`, Blue `#275EA8`, Gold `#FCB332`. Style terkapsulasi supaya tidak bocor ke modul lain.

**Keamanan tiga lapis (Zero Trust).** Jangan menulis ulang logika keamanan; delegasikan ke `iam.plugin` (.NET 10).

1. **Izin masuk (endpoint)** — `[KemenkeuAuthorize('app:resource:action')]` di endpoint C#; `*hasPermission` di elemen HTML Angular.
2. **Cakupan data (Scope)** — pembatasan baris difilter langsung di klausa WHERE database, bukan disaring di memori.
3. **Penyamaran kolom (Sieve)** — field sensitif diubah jadi `null` di level response.

Kebijakan IAM disimpan sebagai data, bukan hardcode.

**Database.** Tiap domain punya database sendiri. Silo dipecah di level akses lewat IAM.

**Audit trail.** Jejak persisten untuk setiap pembuatan, pengubahan, dan akses data.

**AGENTS.md.** Setiap remote MFE dilengkapi file ini, dibaca AI coding tool sebelum menulis kode.

**AI Assistant.** Read-only lewat server MCP, tanpa akses tulis/hapus. Untuk SIGAP statusnya opsional ("boleh menyusul").

## Lampiran B — Role dan lingkup data

Sudah dikonfirmasi pemilik proses bisnis. Mengikuti Tabel Pengguna dokumen UR.

| Rule | Sebutan | Lingkup data | Grup SSO |
| --- | --- | --- | --- |
| PEGAWAI | Pegawai Umum | Dirinya sendiri & unitnya | `sigap-pegawai` |
| SATGAS | Tim Satgas Tanggap Darurat | Unit kerja | `sigap-satgas` |
| PIMPINAN | Pimpinan Satker | Unit kerja | `sigap-pimpinan` |
| PERWAKILAN | Kepala Perwakilan | Wilayah provinsi, filter kab/kota & unit lintas Eselon I | `sigap-perwakilan` |
| SUBKOORDINATOR | Subkoordinator | Eselon I, filter provinsi/kab-kota | `sigap-subkoordinator` |
| KOORDINATOR | Koordinator MKB | Nasional, filter unit/provinsi/kab-kota | `sigap-koordinator` |
| SEKJEN | Sekretaris Jenderal | Nasional, filter unit/provinsi/kab-kota | `sigap-sekjen` |
| ADMIN | Administrator Sistem | Nasional, termasuk data sistem | `sigap-admin` |
| PENGEMBANG | Tim Pengembang Dokumen MKB | Unit — **Fase 2** | `sigap-pengembang` |
| IMPL_RKB | Tim Implementasi RKB | Unit — **Fase 2** | `sigap-impl-rkb` |

Dua grup terakhir **didaftarkan** ke SSO sekarang supaya tidak perlu pengajuan ulang di Fase 2, tetapi **tidak dibangunkan** menu, endpoint, maupun permission apa pun di Fase 1.

**Penting:** Lampiran 1 Kebutuhan Teknis menulis berbeda untuk Subkoordinator (wilayah) dan Koordinator MKB (unit Eselon I). Itu **keliru**; jangan dipakai. Koreksi ini perlu disampaikan ke BaTII saat pendaftaran grup SSO.

**Scope Fase 1:** hanya Safety Check/SOS, Laporkan Potensi Bencana, Trigger Safety Check Unit, Verifikasi Alert Bencana, Asesmen Kondisi Bencana (5 aspek), dan Dashboard Monitor SC & Sumber Daya. Modul dokumen MKB (ARKB/ADB/SKB/RTDB/RKBU/RPKK), komunikasi kebencanaan, dan eksekusi RKB **tidak** dibangun.

## Lampiran C — 13 koreksi stakeholder

Sumber: `docs/Catatan-Masukan-Probis.xlsx` (sheet masukan & Ucob). **Lebih baru daripada mockup HTML** — kalau berbeda, koreksi ini yang menang.

1. Safety Check/SOS hanya melekat pada role Pegawai Umum sebagai fitur personal. Role lain tidak punya menu Safety Check sendiri, hanya melihat rekapnya lewat dashboard.
2. Form Broadcast Safety Check seragam: pegawai cuma pilih "Saya Aman" atau "Butuh Bantuan". Jangan tambah field lain meski pegawai skip/telat merespons.
3. Verifikasi Alert Bencana oleh Tim Satgas wajib berupa proses approve/reject, bukan notifikasi read-only.
4. Gabungkan "Rekap Safety Check" ke dalam alur Asesmen Kondisi Bencana, hapus menu terpisah. Hapus menu "Status Aset Unit" karena sudah tercakup di aspek Aset.
5. Form Asesmen Aspek SDM butuh field "Catatan Kondisi Pegawai" dan "Catatan Tambahan Aspek SDM", selain rekap Safety Check otomatis.
6. Form Asesmen Aspek Aset butuh field: Konstruksi Bangunan Kantor, Akses ke Lokasi Kantor, Kondisi Peralatan, Jumlah Peralatan Tersedia, Kondisi Perlengkapan, Jumlah Perlengkapan Tersedia, Kendaraan Laik Operasi, Jumlah Kendaraan Tersedia, Catatan Aset.
7. Aspek Layanan Terdampak: input manual status per layanan kritis (Normal/Terganggu/Berhenti Total) oleh Tim Satgas. Fase 1 manual, bukan ditarik dari dokumen ADB.
8. Saat Pimpinan Satker approve, jangan tampilkan ulang form jenis bencana/lokasi. Pimpinan hanya meninjau 5 aspek lalu approve/pending.
9. Tombol submit pertama berlabel "Kirim"; kalau Satgas update setelah pengiriman pertama, label berubah jadi "Update Asesmen".
10. Kepala Perwakilan harus punya tombol trigger broadcast Safety Check tingkat wilayah (filter provinsi/kabupaten-kota/jenis unit), bukan cuma monitoring.
11. Widget rekap Safety Check tampil sebagai tab per kondisi (Aman / Butuh Bantuan / Belum Merespons), bukan list yang harus di-scroll.
12. Prinsip trigger "siapa pun yang lebih dulu tahu, lebih dulu memicu": kalau satu role sudah memicu untuk suatu lingkup, role lain tidak perlu memicu ulang tapi tetap diizinkan memicu manual. Trigger otomatis BMKG tidak menghalangi trigger manual.
13. Form trigger memuat: kategori ancaman, jenis ancaman, pesan ke pegawai, plus target lingkup sesuai role.

## Lampiran D — Registry dummy dan urutan penukaran

Buat `DUMMY_REGISTRY.md` di root repo, perbarui tiap ada dummy atau asumsi baru:

| Kolom | Isi |
| --- | --- |
| Nama dummy | mis. `libs/keu-ui-dummy` |
| Menggantikan | `@danarakca/keu-ui` |
| Kontrak ditiru dari | Slide Arsitektur ICS |
| Asumsi yang perlu diverifikasi | Nama class di luar `.page-header`, `.stats-row`, `.table-card`; seluruh token turunan |
| Pemicu penukaran | Kredensial registry npm internal |
| Perkiraan beban | Sedang |
| Status | Aktif / Sudah ditukar |

Kolom asumsi paling berharga: itu bahan siap pakai saat berkoordinasi dengan BaTII. Alih-alih bertanya "boleh minta dokumentasi design system?", kamu bisa bertanya spesifik tentang 15 nama class yang benar-benar dibutuhkan.

**Urutan penukaran:**

| No | Dummy | Dipicu oleh | Beban |
| --- | --- | --- | --- |
| 1 | `remoteName` + port | BaTII menetapkan nilai resmi | Ringan, satu file konfigurasi |
| 2 | Design system | Kredensial registry npm internal | Sedang, penyesuaian visual per halaman |
| 3 | `iam.plugin` | Kredensial NuGet internal | Ringan kalau kontrak dipatuhi |
| 4 | SSO | Client OIDC didaftarkan BaTII | Ringan, ganti issuer dan client id |
| 5 | Shell + starter.mfe | Akses shell ICS + template | Sedang, lihat P7.2 |
| 6 | Data SIMAN | API SIMAN + koordinat tersedia | Ringan, ganti seeder |
| 7 | Notifikasi | Jawaban Web Push / layanan platform | Ringan, ganti konfigurasi channel |

## Lampiran E — Pertanyaan ke BaTII

★ = memblokir penukaran, kirim secepatnya.

**Platform ICS:**
1. ★ Lokasi repo `starter.mfe` dan hak akses untuk clone
2. ★ `remoteName` resmi untuk SIGAP dan alokasi port lokalnya
3. ★ Kredensial registry npm internal (`@danarakca/keu-ui`) dan NuGet internal (`iam.plugin`)
4. Katalog lengkap komponen SCSS design system dan dokumentasinya
5. Format permission string: apakah `sigap:resource:action` sesuai konvensi platform?
6. Cara mendaftarkan aturan Scope dan Sieve ke IAM: UI admin, file konfigurasi, atau API?
7. SIGAP masuk jalur Core APBN atau Non Core APBN?
8. Penamaan: Kebutuhan Teknis menyebut `sigap-web`/`sigap-api`, slide ICS memakai pola `<nama>.mfe` dan `<nama>.plugin`. Mana yang berlaku?
9. Apakah platform menyediakan layanan notifikasi bersama di antara 20 layanan data/API?
10. Apakah audit trail tersedia bawaan dari iam.plugin/template?
11. Struktur repo resmi: satu repo per modul (web+api) atau dipisah?
12. Cara shell menyerahkan token ke remote module: service, event bus, atau storage?

**Infrastruktur:**
13. Web Push diizinkan di domain platform? Kalau tidak, penggantinya apa?
14. Siapa menjalankan migrasi skema database: tim kita pakai EF Core migrations, atau skrip dijalankan pengelola database?
15. Kriteria kelulusan dan checklist VT, termasuk threshold scanning
16. Koordinat gedung kantor dari SIMAN

**Sudah diputuskan, tinggal disampaikan:** lingkup data Subkoordinator (Eselon I) dan Koordinator MKB (nasional) mengikuti dokumen UR. Lampiran 1 Kebutuhan Teknis keliru di dua role ini.

## Lampiran F — Prioritas dokumen sumber

Kalau dokumen saling bertentangan:

1. **Standar Arsitektur ICS** (cara membangun)
2. **Masukan/revisi stakeholder** (xlsx, Lampiran C)
3. **Dokumen UR** (scope & lingkup data)
4. **Kebutuhan Teknis** (parameter infrastruktur)
5. **Mockup HTML** (referensi visual saja, bukan sumber kebenaran fitur)

Mockup juga menampilkan fitur Fase 2 yang belum masuk scope — jangan ikut diporting.
