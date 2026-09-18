# MIGRATION_NOTES

Ringkasan scope Fase 1, koreksi stakeholder, dan standar platform wajib. Disusun dari
`docs/PLAYBOOK.md` (Lampiran A–F), `docs/UR-SistemMKB.docx`, `docs/Catatan-Masukan-Probis.xlsx`
(sheet "Fitur & Data per Role", "Masukan SIGAP Tanggap Darurat", "Ucob SIGAP V4"), dan
`docs/Standar-Arsitektur-ICS.pdf`. Prioritas kalau dokumen bertentangan: lihat Lampiran F
PLAYBOOK (Standar Arsitektur ICS → masukan/revisi stakeholder → Dokumen UR → Kebutuhan Teknis →
mockup HTML, referensi visual saja).

---

## 1. Scope Fase 1

### 1.1 8 role aktif

Sistem MKB dirancang untuk 10 role total; 8 aktif di Fase 1, 2 disiapkan (didaftarkan ke SSO)
tapi tanpa menu/endpoint/permission apa pun sampai Fase 2.

| Role | Lingkup data | Grup SSO |
| --- | --- | --- |
| Pegawai Umum | Dirinya sendiri & unitnya | `sigap-pegawai` |
| Tim Satgas Tanggap Darurat | Unit kerja | `sigap-satgas` |
| Pimpinan Satker | Unit kerja | `sigap-pimpinan` |
| Kepala Perwakilan | Wilayah provinsi (filter kab/kota & unit lintas Eselon I) | `sigap-perwakilan` |
| Subkoordinator | Eselon I (filter provinsi/kab-kota) | `sigap-subkoordinator` |
| Koordinator MKB | Nasional (filter unit/provinsi/kab-kota) | `sigap-koordinator` |
| Sekretaris Jenderal | Nasional (filter unit/provinsi/kab-kota) | `sigap-sekjen` |
| Administrator Sistem | Nasional, termasuk data sistem | `sigap-admin` |
| *(Fase 2)* Tim Pengembang Dokumen MKB | Unit — tidak dibangun di Fase 1 | `sigap-pengembang` |
| *(Fase 2)* Tim Implementasi RKB | Unit — tidak dibangun di Fase 1 | `sigap-impl-rkb` |

**Koreksi penting (bukan salah ketik, dua sumber saling bertentangan):** Lampiran 1 dokumen
*Kebutuhan Teknis* menulis Subkoordinator berlingkup **wilayah** dan Koordinator MKB berlingkup
**unit Eselon I** — itu **keliru**. Yang benar mengikuti dokumen UR & tabel di atas: Subkoordinator
= **Eselon I**, Koordinator MKB = **nasional**. Sudah diputuskan, tinggal disampaikan ke BaTII
saat pendaftaran grup SSO (Lampiran E PLAYBOOK, poin "Sudah diputuskan").

**Catatan konsistensi internal dokumen UR:** ringkasan "Ruang Lingkup" di UR-SistemMKB.docx
menyebut "6 peran pengguna aktif" (tidak menghitung Sekretaris Jenderal dan Administrator),
sementara bagian "Tabel Pengguna" di dokumen yang sama menandai 8 baris berstatus "Aktif" dan
menyebut eksplisit "8 peran yang aktif". **Pegang angka 8** — itu yang konsisten dengan tabel
peran, dengan matriks fitur di xlsx (Sekjen & Admin punya baris akses), dan dengan PLAYBOOK
Lampiran B.

### 1.2 3 alur proses bisnis inti

Dari UR-SistemMKB.docx bagian "Langkah-Langkah Proses Bisnis":

1. **Broadcast Safety Check** (otomatis BMKG + manual/override)
   - Trigger otomatis: parsing field "Dirasakan" dari `data.bmkg.go.id` (autogempa.json,
     gempadirasakan.json) → ekstrak nilai MMI per wilayah → broadcast otomatis **hanya** kalau
     MMI ≥ V, tanpa persetujuan manual siapa pun. MMI < V dicatat sebagai referensi saja.
   - Trigger manual/override: Tim Satgas (unit), Kepala Perwakilan (wilayah provinsi),
     Subkoordinator (Eselon I, filter provinsi/kab-kota), Koordinator MKB (nasional, filter
     unit/provinsi/kab-kota). Prinsip "siapa/apa pun lebih dulu tahu, lebih dulu memicu" —
     trigger otomatis BMKG tidak menghalangi trigger manual role mana pun.
   - Pegawai merespons hanya dengan dua pilihan: "Saya Aman" / "Butuh Bantuan".

2. **Verifikasi Alert Bencana**
   - Pegawai melapor lewat "Laporkan Potensi Bencana" (jenis bencana, level keparahan, lokasi,
     deskripsi, lampiran foto/video/suara).
   - Tim Satgas **wajib** approve/reject (bukan sekadar read-only) — kalau Valid, eskalasi ke
     Pimpinan Satker (dasar mulai broadcast Safety Check/Asesmen); kalau Tolak, laporan ditutup.

3. **Asesmen Dampak Bencana & Aktivasi Status Tanggap Darurat**
   - Tim Satgas mengisi/mengubah asesmen 5 aspek: SDM, Aset, TIK, Arsip, Layanan Terdampak
     (status Normal/Terganggu/Berhenti Total, input manual — bukan ditarik dari dokumen ADB di
     Fase 1).
   - Dikirim/diperbarui ke Pimpinan Satker via menu **Asesmen Kondisi Bencana** (bukan "deklarasi
     darurat"). Tombol "Kirim" pertama kali, "Update Asesmen" untuk pengiriman berikutnya.
   - Pimpinan Satker **hanya** review + approve/pending, **tanpa** re-entry jenis bencana/lokasi;
     tidak bisa mengubah isi 5 aspek.
   - Status Approve/Pending tampil ke Kepala Perwakilan/Subkoordinator/Koordinator MKB **segera
     setelah asesmen dikirim**, tidak menunggu approval Pimpinan selesai (paralel).
   - Dashboard Monitor SC & Sumber Daya: agregat kuantitas per lingkup, dengan filter sesuai
     role; Sekjen dan role pengawas berjenjang lainnya read-only + lihat detail.

### 1.3 Fitur in-scope vs out-of-scope

**In-scope Fase 1** (kode fitur mengikuti sheet "Fitur & Data per Role"):

| Kode | Fitur |
| --- | --- |
| 1.1–1.1.3 | Data Bencana Nasional (pra-bencana, read-only untuk semua role kecuali Koordinator MKB yang bisa tambah/modify) |
| 2.1 | Safety Check / SOS (personal, hanya Pegawai Umum) |
| 2.2 | Laporkan Potensi Bencana (Pegawai Umum) |
| 2.3 | Trigger Safety Check Unit (Satgas, Perwakilan, Subkoordinator, Koordinator) |
| 2.4 | Verifikasi Alert Bencana (Tim Satgas: approve/reject) |
| 2.5 | Asesmen Kondisi Bencana — 5 aspek (Tim Satgas isi; Pimpinan Satker read-only + approve) |
| 2.6 | Dashboard Monitor SC & Sumber Daya (Perwakilan/Subkoordinator/Koordinator/Sekjen, read-only + filter sesuai lingkup) |

**Out-of-scope Fase 1** (jangan diporting, cukup disembunyikan lewat feature flag di prototipe —
lihat P1.2 PLAYBOOK):

- Modul dokumen MKB pra-bencana: ARKB, ADB, SKB, RTDB, RKBU, RPKK, serta jadwal simulasi/drill.
- Modul pasca-bencana: eksekusi Rencana Keberlangsungan Bisnis (RKB), Laporan Penerapan
  Keberlangsungan Bisnis (LPKB), monitoring kepatuhan MKB.
- Role Tim Pengembang Dokumen MKB dan Tim Implementasi RKB (menu, endpoint, permission apa pun).
- Menu "Panduan & Dok. MKB" pada role Pegawai Umum.
- Fitur komunikasi kebencanaan dan eksekusi RKB.
- AI Assistant dan fitur analitik lanjutan (platform ICS pun statusnya "boleh menyusul", opsional
  untuk SIGAP, read-only lewat MCP server saat ada — tanpa akses tulis/hapus).

---

## 2. Koreksi stakeholder (13 item, sumber: xlsx sheet masukan & Ucob)

**Koreksi ini LEBIH BARU daripada mockup HTML** — kalau berbeda, koreksi ini yang menang.

1. Safety Check/SOS melekat **hanya** pada role Pegawai Umum sebagai fitur personal; role lain
   tidak punya menu Safety Check sendiri, hanya lihat rekap lewat dashboard.
2. Form Broadcast Safety Check seragam: hanya "Saya Aman" / "Butuh Bantuan" — tidak ada field
   tambahan meski pegawai skip/telat merespons.
3. Verifikasi Alert Bencana oleh Tim Satgas **wajib** approve/reject, bukan notifikasi read-only.
4. Gabungkan "Rekap Safety Check" ke alur Asesmen Kondisi Bencana, hapus menu terpisah. Hapus
   menu "Status Aset Unit" (sudah tercakup di aspek Aset).
5. Form Asesmen Aspek SDM butuh field "Catatan Kondisi Pegawai" dan "Catatan Tambahan Aspek SDM",
   selain rekap Safety Check otomatis.
6. Form Asesmen Aspek Aset butuh field: Konstruksi Bangunan Kantor, Akses ke Lokasi Kantor,
   Kondisi Peralatan, Jumlah Peralatan Tersedia, Kondisi Perlengkapan, Jumlah Perlengkapan
   Tersedia, Kendaraan Laik Operasi, Jumlah Kendaraan Tersedia, Catatan Aset.
7. Aspek Layanan Terdampak: input manual status per layanan kritis (Normal/Terganggu/Berhenti
   Total) oleh Tim Satgas — Fase 1 manual, bukan ditarik dari dokumen ADB.
8. Saat Pimpinan Satker approve: **jangan** tampilkan ulang form jenis bencana/lokasi. Hanya
   meninjau 5 aspek lalu approve/pending.
9. Tombol submit pertama "Kirim"; kalau Satgas update setelah pengiriman pertama, label jadi
   "Update Asesmen".
10. Kepala Perwakilan **harus** punya tombol trigger broadcast Safety Check tingkat wilayah
    (filter provinsi/kab-kota/jenis unit), bukan cuma monitoring — fitur ini sempat hilang di
    prototipe dan wajib dikembalikan.
11. Widget rekap Safety Check tampil sebagai **tab per kondisi** (Aman / Butuh Bantuan / Belum
    Merespons), bukan list yang harus di-scroll/next.
12. Prinsip trigger "siapa pun lebih dulu tahu, lebih dulu memicu": kalau satu role sudah memicu
    untuk suatu lingkup, role lain tidak perlu memicu ulang tapi tetap **diizinkan** memicu
    manual. Trigger otomatis BMKG tidak menghalangi trigger manual (relevan untuk logika
    deduplikasi notifikasi — lihat P5.3 PLAYBOOK).
13. Form trigger memuat: kategori ancaman, jenis ancaman, pesan ke pegawai, plus target lingkup
    sesuai role (provinsi/kab-kota/jenis unit untuk Perwakilan & Subkoordinator; unit untuk
    Satgas; filter bebas untuk Koordinator).

---

## 3. Standar platform ICS Keuangan (wajib dipatuhi, jangan diimprovisasi)

Sumber: `docs/Standar-Arsitektur-ICS.pdf` (dokumen berupa slide deck, sebagian besar diagram —
detail tekstual di bawah ini sudah divalidasi lewat PLAYBOOK Lampiran A).

- **Posisi SIGAP:** satu remote module di antara 22 modul tampilan, satu microservice di antara
  20 layanan data, di atas fondasi ICS Keuangan bersama Core APBN dan Non Core APBN. Komponen
  bersama yang **tidak boleh dibangun ulang**: Shell & SSO, IAM 3 lapis, Design System, 20
  layanan data/API, gudang data.
- **Scaffolding:** modul baru wajib clone `starter.mfe`, bukan `ng new`.
- **Golden Rule penamaan:** seluruh identitas (element name, function, selector, route path,
  display name) diturunkan dari satu `remoteName`. Port dialokasikan berurutan dari shell
  (4200-an) — nama remote & port resmi SIGAP masih harus dikonfirmasi ke BaTII (Lampiran E,
  poin ★ 1–2).
- **Routing:** path relatif, flat routes, **dilarang nested routing**. Remote sinkron dua arah
  dengan URL browser (Silent Location Strategy). Shell sangat ringan, **tanpa logika bisnis**.
- **Styling:** token via `@use 'index' as *`; pakai katalog komponen SCSS platform
  (`.page-header`, `.stats-row`, `.table-card`, dll). **Dilarang** hardcode warna atau membangun
  komponen visual sendiri yang menduplikasi katalog. Token resmi: Navy `#003d7a`, Blue
  `#275EA8`, Gold `#FCB332`.
- **Keamanan tiga lapis (Zero Trust)**, didelegasikan ke `iam.plugin` (.NET 10) — jangan tulis
  ulang logika keamanan sendiri:
  1. **Izin masuk:** `[KemenkeuAuthorize("app:resource:action")]` di endpoint C#;
     `*hasPermission` di elemen Angular.
  2. **Scope (cakupan data):** filter baris di klausa **WHERE** database, bukan disaring di
     memori.
  3. **Sieve (penyamaran kolom):** field sensitif di-null-kan di level response.
  Kebijakan IAM disimpan sebagai data (JSON), bukan hardcode.
- **Database:** tiap domain punya database sendiri; struktur 32 tabel PostgreSQL existing
  **tidak berubah**, dibaca apa adanya lewat EF Core (schema-first).
- **Audit trail:** jejak persisten untuk setiap pembuatan, pengubahan, dan **akses** data.
- **AGENTS.md:** wajib ada di setiap remote MFE, dibaca AI coding tool sebelum menulis kode.
- **AI Assistant:** read-only lewat MCP server, tanpa akses tulis/hapus — opsional untuk SIGAP.

**Aturan mutlak proyek (dari PLAYBOOK Bagian I):**
1. Prototipe Next.js tetap hidup sampai sistem baru lolos UAT.
2. Kode aplikasi ditulis **seolah-olah platform asli sudah ada** — tidak ada workaround yang
   menyesuaikan diri dengan keterbatasan dummy.
3. Jangan membangun ulang apa yang sudah disediakan platform.
4. Jangan pernah tempel secret asli ke prompt — lewat environment variable/vault.
5. Struktur 32 tabel database tidak berubah. **Satu pengecualian disetujui pemilik proyek
   (18 Sep 2026):** tabel ke-33 `"BroadcastSasaranUnit"` untuk kepemilikan unit per trigger
   safety check. Spesifikasinya di [API_CONTRACT.md](API_CONTRACT.md) §5. Perubahan tabel
   lain tetap harus dibawa ke diskusi lebih dulu.

**Lima aturan dummy** (Fase 3, PLAYBOOK bagian III): karantina di `libs/*-dummy/` /
`apps/shell-dummy/`; kontrak identik dengan dokumentasi platform; isi sesederhana mungkin;
tidak boleh naik ke production (build gagal kalau dummy masih ter-resolve); tercatat di
`DUMMY_REGISTRY.md` (belum dibuat — dibuat di Fase 3 sesuai format Lampiran D PLAYBOOK).

**Catatan lintas platform (Windows dev / Linux production):** kapitalisasi nama file & import
harus persis sama, akhir baris LF, path forward-slash (`Path.Combine()` di C#), script npm
lintas platform (`rimraf`, `cross-env`), verifikasi build di container Linux sejak awal — detail
lengkap di PLAYBOOK Bagian I.

---

## 4. Dokumen belum ada / masih perlu dibuat

- `AGENTS.md` — belum ada di repo. Dibuat di P4.3 (Fase 4), isi mengikuti spesifikasi di
  PLAYBOOK P4.3.
- `DUMMY_REGISTRY.md` — belum ada di repo. Dibuat di Fase 3 begitu dummy pertama dibuat, format
  di Lampiran D PLAYBOOK.
- Pertanyaan ke BaTII (Lampiran E PLAYBOOK) belum dikonfirmasi terkirim — cek F0.3 di checklist
  PLAYBOOK Bagian II sebelum lanjut ke Fase 3 (banyak dummy butuh jawaban BaTII untuk tahu apa
  yang harus ditiru).

---

## 5. Status pengerjaan & serah terima (per 18 Sep 2026)

Bagian ini untuk sesi baru: apa yang sudah dikerjakan, di mana hasilnya, dan apa yang belum
selesai. Checkbox di PLAYBOOK Bagian II **sengaja belum dicentang** — dicentang pemilik proyek
setelah memverifikasi sendiri (PLAYBOOK "Alur satu sesi kerja" langkah 5).

### 5.1 Lokasi yang TIDAK ada di monorepo ini
- **Dua repo, sengaja terpisah.** Monorepo ini = sistem baru (`github.com/atepagung/sigap`).
  Prototipe berasal dari `github.com/donny-apps/mkb` (akun lain, UAT Railway tim SOPB). Keduanya
  berbeda siklus hidup (prototipe dipensiunkan setelah UAT) dan jalur deploy, dan struktur repo
  resmi masih menunggu BaTII (Lampiran E #11). Jangan digabung atau dijadikan submodule.
- **Remote prototipe dipindah ke repo sendiri (keputusan 18 Sep 2026).** Akun `atepagung` tidak
  punya akses ke `donny-apps/mkb` (push: "Repository not found"). Tujuan baru:
  `github.com/atepagung/sigap-prototipe` (privat), dan `donny-apps/mkb` disimpan sebagai remote
  `upstream` untuk jejak asal. **UAT Railway tetap terhubung ke `donny-apps/mkb`**, jadi push ke
  repo baru tidak memperbarui UAT. Perubahan baru sampai ke UAT bila Railway diarahkan ulang
  atau Donny menarik perubahannya.
- **Prototipe Next.js** (rujukan utama migrasi), salinan lokal: `C:\dev\MKB APPS\App`. Prompt
  PLAYBOOK yang menyebut "prototipe" merujuk ke sini.
- **Baseline porting: prototipe commit `1b1487a` (branch `main`, di `atepagung/sigap-prototipe`
  setelah di-push).** API_CONTRACT,
  PERMISSION_MAP, dan `BUSINESS_RULES_INDEX.md` diturunkan dari keadaan prototipe pada commit
  itu. Perubahan prototipe sesudahnya **tidak** otomatis masuk kontrak. Sebelum porting (P4.4),
  periksa `git log 1b1487a..origin/main` di repo prototipe dan putuskan per perubahan. Commit
  ini baru ada di remote setelah butir 5.3 no. 2 selesai.
- Ada **dua** prototipe: `docs/mockup-sikuad-v17.html` (fokus alur proses bisnis) dan aplikasi
  Next.js di atas (fokus tampilan & fitur, yang dikoreksi dan di-refactor).
- Nilai untuk placeholder `[ ]` di PLAYBOOK:
  - P4.2 `[path file migration/schema]` → `C:\dev\MKB APPS\App\prisma\schema.prisma`
  - P5.2 `[path kode upload existing]` → `C:\dev\MKB APPS\App\src\lib\storage.ts`
  - P3.5 `[path file salinan SIMAN]` → prototipe memakai
    `C:\dev\MKB APPS\App\data\kantor-bmn.json` (bukan xlsx; jumlah barisnya belum dicocokkan
    dengan angka 1.431 di PLAYBOOK). Struktur organisasi: `data\otk_bundle.json`.
- `C:\dev\MKB APPS\vapid-baru.txt` berisi kunci VAPID (Web Push). **Rahasia** — jangan dibaca
  ke chat maupun disalin ke repo; masuk lewat environment variable/vault (P3.6).

### 5.2 Selesai
| Langkah | Hasil | Lokasi |
|---|---|---|
| P0 | Ringkasan scope, koreksi, standar | dokumen ini |
| P1.1 | 12 dari 13 koreksi Lampiran C ternyata sudah diterapkan sebelumnya; koreksi 7 (layanan kritis manual, bukan dari ADB) diperbaiki. Didokumentasikan di `CATATAN-UAT.md` (butir L1–L13) dan `RINGKASAN-PERBAIKAN-UAT.md` (Kelompok K) | repo prototipe, commit `cf9d727`, `425558d` |
| P1.2 | Sudah terpenuhi tanpa perubahan: flag `MODE_MATRIKS` + `RUTE_MATRIKS`/`PERAN_MATRIKS`/`PERAN_DISEMBUNYIKAN` di `src/logic/rilis.ts`; urutan sidebar pra→saat→pasca di `src/components/Shell.tsx` | repo prototipe |
| P1.3 | Logika bisnis dipindah ke `src/logic/` (33 berkas, tanpa impor React/Next.js); peta porting di `BUSINESS_RULES_INDEX.md` | repo prototipe, commit `61a48f4`…`ad8147a`, perbaikan tipe `1b1487a` |
| P2.1 | Kontrak API (47 endpoint) dan peta izin (23 permission) | `API_CONTRACT.md`, `PERMISSION_MAP.md` di repo ini |

### 5.3 Belum selesai / perlu tindakan pemilik proyek
1. **Build prototipe belum terverifikasi lulus.** `npm run build` sempat gagal karena galat tipe
   hasil P1.3; sudah diperbaiki (`1b1487a`) tetapi belum dijalankan ulang. Node.js tidak
   tersedia di shell agen, jadi build hanya bisa dijalankan pemilik proyek.
2. **Pindahkan remote prototipe lalu push**, dijalankan pemilik proyek. Agen diblokir mengarahkan
   kode ke tujuan eksternal baru:
   1. Buat repo **privat dan kosong** `atepagung/sigap-prototipe` di github.com/new.
   2. `git remote rename origin upstream` lalu
      `git remote add origin https://github.com/atepagung/sigap-prototipe.git`
   3. Setelah butir 1 lulus: `git push -u origin main`. Hanya `main`, jangan `--all`, supaya
      branch cadangan tidak ikut.
   Penulis 8 commit sesi ini (`cf9d727`…`1b1487a`) sudah diganti dari Donny ke `atepagung`; isi
   kode identik. Cadangan lamanya di branch lokal `cadangan/sebelum-ganti-penulis`, boleh
   dihapus setelah push berhasil. Identitas git lokal repo prototipe (`.git/config`) masih Donny:
   jalankan `git config --unset user.name` dan `git config --unset user.email` di repo itu.
   Untuk UAT, lihat butir "Remote prototipe dipindah" di §5.1.
3. **Commit dokumen di repo ini:** `MIGRATION_NOTES.md`, `API_CONTRACT.md`, `PERMISSION_MAP.md`.
4. **Pertanyaan ke BaTII:** Lampiran E PLAYBOOK ditambah API_CONTRACT §9 butir 1–7 (termasuk
   siapa menjalankan migrasi tabel ke-33).
5. **Pertanyaan ke pemilik proses bisnis:** API_CONTRACT §9 butir 8–10 (wewenang mengakhiri
   broadcast & tanggap darurat, jenis/batas lampiran, pemilik butir 1.1).
6. **Langkah PLAYBOOK berikutnya: P2.2** (inventaris komponen UI). Catatan: prototipe punya 76
   berkas `.tsx` di `src/components`, bukan 69 seperti di PLAYBOOK.

### 5.4 Keputusan yang mengikat sistem baru
- **Prototipe sengaja TIDAK disamakan** dengan API_CONTRACT (keputusan 18 Sep 2026). Seluruh
  selisih di API_CONTRACT §6 (12 butir) **wajib diterapkan di sigap-api/sigap-web**. Saat
  porting P4.4 ("logika identik"), butir §6 mengalahkan perilaku prototipe.
- Tabel ke-33 `"BroadcastSasaranUnit"` disetujui (lihat aturan mutlak no. 5 di atas).
- Aturan trigger: satu unit dipegang satu broadcast aktif per jenis bencana; trigger berikutnya
  melewati unit itu; sasaran dikunci saat dipicu; unit berdata kosong tidak ikut disasar
  (API_CONTRACT §3.3.1).
