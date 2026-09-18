# PERMISSION_MAP — sigap-api, Fase 1 (Tanggap Darurat)

Status: **disetujui untuk dibangun**, 18 September 2026. Pasangan dokumen ini:
[API_CONTRACT.md](API_CONTRACT.md). Nomor endpoint `#n` merujuk API_CONTRACT §2.

Dokumen ini adalah bahan pendaftaran aturan IAM ke BaTII: peran ↔ grup SSO, 23 permission,
profil Scope beserta predikat SQL terhadap kolom nyata, aturan Sieve, dan draf kebijakan
sebagai data (§7). Sesuai standar ICS, kebijakan disimpan sebagai data, bukan hardcode. Sumber
kebenarannya matriks `docs/Catatan-Masukan-Probis.xlsx` sheet "Fitur & Data per Role", butir
2.1–2.6, dengan lingkup data per peran dari PLAYBOOK Lampiran B.

Dua aturan dari matriks yang ditegakkan dan dibuktikan di §5:
- Peran bertanda **"-"** pada sebuah fitur tidak memegang satu pun permission untuk fitur itu.
- Peran bertanda **"Read Only"** tidak memegang permission tulis untuk fitur itu.

---

## 1. Peran dan grup SSO

| Peran (`RoleKey`) | Sebutan di matriks | Grup SSO | Fase 1 | Lingkup baca bawaan |
|---|---|---|---|---|
| `PEGAWAI` | Pegawai Umum | `sigap-pegawai` | aktif | dirinya sendiri & unitnya |
| `SATGAS` | Tim Satgas | `sigap-satgas` | aktif | `UNIT` |
| `PIMPINAN` | Pimpinan Unit (= Pimpinan Satker) | `sigap-pimpinan` | aktif | `UNIT` |
| `PERWAKILAN` | Perwakilan (Kepala Perwakilan) | `sigap-perwakilan` | aktif | `WILAYAH` (provinsi) |
| `SUBKOORDINATOR` | Subkoordinator | `sigap-subkoordinator` | aktif | `ESELON_I` |
| `KOORDINATOR` | Koordinator MKB | `sigap-koordinator` | aktif | `NASIONAL` |
| `SEKJEN` | Sekretaris Jenderal | `sigap-sekjen` | aktif | `NASIONAL` |
| `ADMIN` | — (tidak ada di matriks) | `sigap-admin` | aktif | **tanpa permission bisnis** |
| `PENGEMBANG` | — | `sigap-pengembang` | didaftarkan saja | tanpa permission |
| `IMPL_RKB` | — | `sigap-impl-rkb` | didaftarkan saja | tanpa permission |

- Peran dibaca dari klaim `groups` token SSO, bukan dari tabel `"UserRole"`.
- Tabel Pengguna di Dokumen UR menulis `sigap-Sekjen` dengan huruf kapital. Yang dipakai
  `sigap-sekjen`, seragam dengan grup lain (Lampiran B).
- Lingkup Subkoordinator = **Eselon I** dan Koordinator MKB = **nasional**, mengikuti UR dan
  Lampiran B. Lampiran 1 Kebutuhan Teknis keliru di dua peran ini, dan koreksinya perlu
  disampaikan saat pendaftaran grup.
- Tim Pengembang dan Tim Implementasi RKB **didaftarkan sekarang** supaya tidak perlu diajukan
  ulang di Fase 2, tetapi tidak diberi apa pun di Fase 1.
- Administrator tidak muncul di matriks, jadi tidak memegang permission bisnis. Fungsi sistemnya
  (kelola pengguna/unit) di luar kontrak ini. Prototipe mengizinkan ADMIN melakukan segalanya,
  dan itu **tidak** dibawa ke sistem baru.

---

## 2. Profil Scope

### 2.1 Parameter dari identitas
| Parameter | Sumber |
|---|---|
| `@penggunaId` | klaim `nip` → `"User"."id"` |
| `@unitId` | `"User"."unitId"` |
| `@provinsi` | `"Unit"."provinsi"` unit pengguna (bisa kosong) |
| `@eselonIKey` | `"Unit"."eselonIKey"` unit pengguna (bisa kosong) |

### 2.2 Profil
Nama tabel dan kolom persis seperti di PostgreSQL. Skema Prisma prototipe tidak memakai
`@map`, jadi keduanya bertanda kutip dan camelCase. `{unit}` = kolom unit tabel yang disaring
(daftar di bawah).

| Profil | Predikat | Bila parameter kosong |
|---|---|---|
| `SELF` | `{pemilik} = @penggunaId` | — |
| `UNIT` | `{unit} = @unitId` | — |
| `WILAYAH` | `{unit} IN (SELECT "id" FROM "Unit" WHERE "provinsi" = @provinsi)` | **menyempit ke `UNIT`** |
| `ESELON_I` | `{unit} IN (SELECT "id" FROM "Unit" WHERE "eselonIKey" = @eselonIKey)` | **menyempit ke `UNIT`** |
| `NASIONAL` | tanpa predikat | — |
| `SASARAN_SAYA` | `b."selesaiPada" IS NULL AND EXISTS (SELECT 1 FROM "BroadcastSasaranUnit" s WHERE s."broadcastId" = b."id" AND s."unitId" = @unitId AND s."status" = 'DISASAR')` | — |
| `TERSENTUH(p)` | `EXISTS (SELECT 1 FROM "BroadcastSasaranUnit" s WHERE s."broadcastId" = b."id" AND <profil p atas s."unitId">) OR b."dikirimOlehId" = @penggunaId` | ikut `p` |
| `IKUT_INDUK` | lampiran terlihat hanya bila baris induknya (`disasterAlertId` / `damageAssessmentId` / `checklistId`) terlihat menurut permission dan Scope induk | — |

**Fail-closed.** Kepala Perwakilan tanpa provinsi atau Subkoordinator tanpa Eselon I jatuh ke
unit sendiri, tidak pernah melebar. Respons `GET /me/konteks` memuat `catatan` penjelasnya.
Ini sama dengan `lingkup.ts` prototipe.

**Kolom per tabel**
| Tabel | `{unit}` | `{pemilik}` |
|---|---|---|
| `"SafetyCheckResponse"` | `"unitId"` | `"userId"` |
| `"DisasterAlert"` | `"unitId"` | `"pelaporId"` |
| `"DamageAssessment"` | `"unitId"` | — |
| `"ChecklistKondisiLapangan"` | `"unitId"` | — |
| `"DisasterDeclaration"` | `"unitId"` | — |
| `"LayananKritis"` | `"unitId"` | — |
| `"GangguanLayanan"` | lewat `"LayananKritis"."unitId"` (join `"layananId"`) | — |
| `"BroadcastSasaranUnit"` | `"unitId"` | — |
| `"User"` (daftar pegawai, penyebut rekap) | `"unitId"` | `"id"` |
| `"Unit"` (referensi) | `"id"` | — |
| `"ActiveBroadcast"` | lewat `SASARAN_SAYA` / `TERSENTUH` | `"dikirimOlehId"` |

### 2.3 Pengguna berperan ganda
- Scope sebuah permintaan = **gabungan (OR)** profil dari peran yang (a) dipegang pengguna
  **dan** (b) memberi permission endpoint itu. Peran yang tidak memberi permission tersebut
  **diabaikan**.
- Contoh: pengguna Satgas + Kepala Perwakilan. Verifikasi laporan (`sigap:laporan:verify`,
  hanya diberi SATGAS) → `UNIT`. Dashboard (`sigap:monitor:read`, hanya diberi PERWAKILAN) →
  `WILAYAH`. Prototipe memakai peran terluas untuk semuanya (API_CONTRACT §6 butir 5).
- Sieve mengikuti logika yang sama: field terlihat bila **salah satu** peran pemberi
  permission ada di daftar "terlihat untuk".
- Pengecualian: **lingkup trigger** memakai urutan prioritas, karena satu broadcast hanya punya
  satu lingkup (API_CONTRACT §3.3.2).

### 2.4 Scope tulis
Selain filter baris, Scope juga membatasi apa yang boleh ditulis:
- Laporan, asesmen, layanan kritis, dan jawaban safety check **selalu atas nama unit/pengguna
  pemanggil**. `unitId` dan `userId` diambil dari identitas, **tidak pernah dari body**.
- Trigger: kandidat sasaran dibatasi per lingkup, dan provinsi/Eselon I diturunkan dari unit
  pemicu (API_CONTRACT §3.3.2).
- Pencatatan keadaan pegawai (#6): pegawai sasaran harus di `UNIT` Satgas dan unitnya
  `DISASAR` pada broadcast.
- Persetujuan asesmen (#28) dan penutupan tanggap darurat (#29): `UNIT`.
- Mengakhiri broadcast (#16): pemicunya, atau lingkup pengakhir mencakup seluruh unit
  `DISASAR`-nya.

---

## 3. Daftar permission (23)

Tulis = mengubah data bisnis. `notifikasi:subscribe` hanya mendaftarkan perangkat milik sendiri
dan tidak dihitung sebagai tulis bisnis (lihat §5.3).

| # | Permission | Jenis | Endpoint | Pemegang → Scope |
|---|---|---|---|---|
| 1 | `sigap:safety-check:read` | baca | #1, #3 | PEGAWAI → `SASARAN_SAYA` (#1), `SELF` (#3) |
| 2 | `sigap:safety-check:respond` | tulis | #2 | PEGAWAI → `SASARAN_SAYA` |
| 3 | `sigap:safety-check:record` | tulis | #6 | SATGAS → `UNIT` |
| 4 | `sigap:safety-check-rekap:read` | baca | #4, #5 | PEGAWAI, PIMPINAN, SATGAS → `UNIT` |
| 5 | `sigap:broadcast:trigger` | tulis | #12, #13 | SATGAS, PERWAKILAN, SUBKOORDINATOR, KOORDINATOR → batas sasaran §2.4 |
| 6 | `sigap:broadcast:read` | baca | #14, #15 | SATGAS `TERSENTUH(UNIT)`, PERWAKILAN `TERSENTUH(WILAYAH)`, SUBKOORDINATOR `TERSENTUH(ESELON_I)`, KOORDINATOR `NASIONAL` |
| 7 | `sigap:broadcast:close` | tulis | #16 | keempat pemicu → §2.4 · *di luar matriks* |
| 8 | `sigap:laporan:create` | tulis | #7 | PEGAWAI → atas nama unit sendiri |
| 9 | `sigap:laporan:read` | baca | #9, #10, #17 | PEGAWAI → `SELF`; SATGAS → `UNIT` |
| 10 | `sigap:laporan:verify` | tulis | #18 | SATGAS → `UNIT` |
| 11 | `sigap:asesmen:create` | tulis | #21 | SATGAS → atas nama unit sendiri |
| 12 | `sigap:asesmen:update` | tulis | #22 | SATGAS → `UNIT` |
| 13 | `sigap:asesmen:read` | baca | #24–#27 | SATGAS, PIMPINAN `UNIT`; PERWAKILAN `WILAYAH`; SUBKOORDINATOR `ESELON_I`; KOORDINATOR, SEKJEN `NASIONAL` |
| 14 | `sigap:asesmen:approve` | tulis | #28 | PIMPINAN → `UNIT` |
| 15 | `sigap:tanggap-darurat:close` | tulis | #29 | PIMPINAN → `UNIT` · *di luar matriks* |
| 16 | `sigap:monitor:read` | baca | #30–#35 | PERWAKILAN `WILAYAH`; SUBKOORDINATOR `ESELON_I`; KOORDINATOR, SEKJEN `NASIONAL` |
| 17 | `sigap:layanan-kritis:read` | baca | #19 | SATGAS, PIMPINAN → `UNIT` |
| 18 | `sigap:layanan-kritis:create` | tulis | #20 | SATGAS → atas nama unit sendiri |
| 19 | `sigap:referensi:read` | baca | #37–#42 | tujuh peran matriks. #37–#38 tanpa Scope. #39–#42: PEGAWAI, SATGAS, PIMPINAN `UNIT`; PERWAKILAN `WILAYAH`; SUBKOORDINATOR `ESELON_I`; KOORDINATOR, SEKJEN `NASIONAL` |
| 20 | `sigap:lampiran:read` | baca | #11 | PEGAWAI, SATGAS, PIMPINAN, PERWAKILAN, SUBKOORDINATOR, KOORDINATOR, SEKJEN → `IKUT_INDUK` |
| 21 | `sigap:lampiran:upload` | tulis | #8, #23 | PEGAWAI → #8 `SELF` (laporan `MENUNGGU`); SATGAS → #23 `UNIT` |
| 22 | `sigap:notifikasi:read` | baca | #43 | tujuh peran matriks; isi dihitung menurut peran & Scope masing-masing |
| 23 | `sigap:notifikasi:subscribe` | perangkat | #44, #45 | tujuh peran matriks; hanya langganan milik sendiri |

`GET /me/konteks` (#36) cukup terautentikasi. `/health/*` (#46–#47) publik.

---

## 4. Matriks permission × peran

`—` = tidak dipegang. Isi sel = profil Scope.

| Permission | PEGAWAI | SATGAS | PIMPINAN | PERWAKILAN | SUBKOOR | KOORDINATOR | SEKJEN | ADMIN |
|---|---|---|---|---|---|---|---|---|
| `safety-check:read` | SASARAN_SAYA / SELF | — | — | — | — | — | — | — |
| `safety-check:respond` | SASARAN_SAYA | — | — | — | — | — | — | — |
| `safety-check:record` | — | UNIT | — | — | — | — | — | — |
| `safety-check-rekap:read` | UNIT | UNIT | UNIT | — | — | — | — | — |
| `broadcast:trigger` | — | UNIT | — | WILAYAH | ESELON_I | NASIONAL | — | — |
| `broadcast:read` | — | TERSENTUH | — | TERSENTUH | TERSENTUH | NASIONAL | — | — |
| `broadcast:close` | — | §2.4 | — | §2.4 | §2.4 | §2.4 | — | — |
| `laporan:create` | unit sendiri | — | — | — | — | — | — | — |
| `laporan:read` | SELF | UNIT | — | — | — | — | — | — |
| `laporan:verify` | — | UNIT | — | — | — | — | — | — |
| `asesmen:create` | — | unit sendiri | — | — | — | — | — | — |
| `asesmen:update` | — | UNIT | — | — | — | — | — | — |
| `asesmen:read` | — | UNIT | UNIT | WILAYAH | ESELON_I | NASIONAL | NASIONAL | — |
| `asesmen:approve` | — | — | UNIT | — | — | — | — | — |
| `tanggap-darurat:close` | — | — | UNIT | — | — | — | — | — |
| `monitor:read` | — | — | — | WILAYAH | ESELON_I | NASIONAL | NASIONAL | — |
| `layanan-kritis:read` | — | UNIT | UNIT | — | — | — | — | — |
| `layanan-kritis:create` | — | unit sendiri | — | — | — | — | — | — |
| `referensi:read` | UNIT | UNIT | UNIT | WILAYAH | ESELON_I | NASIONAL | NASIONAL | — |
| `lampiran:read` | IKUT_INDUK | IKUT_INDUK | IKUT_INDUK | IKUT_INDUK | IKUT_INDUK | IKUT_INDUK | IKUT_INDUK | — |
| `lampiran:upload` | SELF | UNIT | — | — | — | — | — | — |
| `notifikasi:read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| `notifikasi:subscribe` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |

Semua permission berawalan `sigap:`. Awalan dihilangkan di tabel ini supaya muat.

---

## 5. Keterlacakan ke matriks "Fitur & Data per Role"

### 5.1 Per butir matriks
Isi sel peran disalin dari xlsx. Kolom terakhir: permission yang memenuhinya.

| Kode | Fitur | Pegawai | Pimpinan | Satgas | Perwakilan | Subkoor | Koordinator | Sekjen | Permission |
|---|---|---|---|---|---|---|---|---|---|
| 2.1.1 | Response broadcast SC aktif | Tambah, Simpan | - | - | - | - | - | - | `safety-check:read`, `safety-check:respond` → PEGAWAI |
| 2.1.2 | Riwayat SC saya | Read Only | - | - | - | - | - | - | `safety-check:read` (SELF) → PEGAWAI |
| 2.1.3 | Rekap Safety Check | Read Only | Read Only (unit) | - | RO, Detail → dashboard | RO, Detail → dashboard | RO, Detail → dashboard | RO, Detail → dashboard | `safety-check-rekap:read` → PEGAWAI, PIMPINAN; pemantau lewat `monitor:read` (2.6) |
| 2.2.1 | Form laporan mandiri | Tambah, Simpan | - | - | - | - | - | - | `laporan:create`, `lampiran:upload` → PEGAWAI |
| 2.2.2 | Riwayat laporan saya | Read Only | - | - | - | - | - | - | `laporan:read` (SELF), `lampiran:read` → PEGAWAI |
| 2.3.1 | Trigger Safety Check | - | - | Aktifkan | Aktifkan | Aktifkan | Aktifkan | - | `broadcast:trigger` → 4 peran |
| 2.3.2 | Riwayat trigger | - | - | Read Only | Read Only | Read Only | Read Only | - | `broadcast:read` → 4 peran |
| 2.4.1 | Laporan masuk | - | - | Lihat, Approve/Reject | - | - | - | - | `laporan:read` (UNIT), `laporan:verify`, `lampiran:read` → SATGAS |
| 2.5.1 | Jenis bencana & waktu kejadian | - | RO, Approve | Tambah, Simpan | - | - | - | - | `asesmen:create`/`read` (SATGAS); `asesmen:read`/`approve` (PIMPINAN) |
| 2.5.2 | Aspek SDM | - | RO, Approve | Lihat, Modify/Update | - | - | - | - | `asesmen:update`/`read` (SATGAS); `asesmen:read`/`approve` (PIMPINAN) |
| 2.5.2.1 | Rekap SC (dalam SDM) | - | RO, Approve | Lihat, Modify/Update | - | - | - | - | `safety-check-rekap:read` (SATGAS, PIMPINAN), `safety-check:record` (SATGAS) |
| 2.5.2.2 | Form SDM | - | RO, Approve | Tambah, Simpan, Ubah | - | - | - | - | `asesmen:create`/`update` (SATGAS) |
| 2.5.3 | Form Aset | - | RO, Approve | Tambah, Simpan, Ubah | - | - | - | - | `asesmen:create`/`update`, `lampiran:upload` (SATGAS) |
| 2.5.4 | Form TIK | - | RO, Approve | Tambah, Simpan, Ubah | - | - | - | - | `asesmen:create`/`update` (SATGAS) |
| 2.5.5 | Form Arsip | - | RO, Approve | Tambah, Simpan, Ubah | - | - | - | - | `asesmen:create`/`update` (SATGAS) |
| 2.5.6 | Form Layanan | - | RO, Approve | Tambah, Simpan, Ubah | - | - | - | - | `asesmen:create`/`update`, `layanan-kritis:read`/`create` (SATGAS); `layanan-kritis:read` (PIMPINAN) |
| 2.6.1 | Notifikasi asesmen masuk | - | - | - | RO, Detail | RO, Detail | RO, Detail | RO, Detail | `monitor:read` → 4 pemantau |
| 2.6.2 | Dashboard 5 aspek | - | - | - | RO, Detail | RO, Detail | RO, Detail | RO, Detail | `monitor:read`, `asesmen:read`, `lampiran:read` → 4 pemantau |
| luar | Akhiri broadcast | | | | | | | | `broadcast:close` → 4 pemicu |
| luar | Tanggap darurat selesai | | | | | | | | `tanggap-darurat:close` → PIMPINAN |

Tiga penafsiran yang disetujui 18 Sep 2026:
1. **2.1.3 bagi Satgas "-"** dibaca sebagai *tidak ada menu rekap tersendiri*. Satgas tetap
   membaca rekap yang sama lewat 2.5.2.1, sesuai koreksi 4 yang menyatukan keduanya.
2. **2.5 bagi pemantau "-"**, tetapi 2.6.2 memberi mereka "Lihat Detail" atas hasil asesmen.
   Karena itu `asesmen:read` dipegang pemantau, dengan Sieve pada catatan SDM.
3. **2.1.3 bagi pemantau** "menjadi dashboard Monitor SC". Mereka membaca angka lewat
   `monitor:read`, bukan daftar per pegawai lewat `safety-check-rekap:read`.

### 5.2 Bukti aturan "-" → tanpa akses
| Peran | Fitur bertanda "-" | Permission fitur itu yang dipegang |
|---|---|---|
| PEGAWAI | 2.3, 2.4, 2.5, 2.6 | tidak ada ✓ |
| PIMPINAN | 2.1.1, 2.1.2, 2.2, 2.3, 2.4, 2.6 | tidak ada ✓ (rekap 2.1.3 diberi "Read Only") |
| SATGAS | 2.1.1, 2.1.2, 2.1.3, 2.2, 2.6 | tidak ada ✓, kecuali rekap 2.1.3 yang diperoleh lewat 2.5.2.1 (penafsiran 1) |
| PERWAKILAN | 2.1.1, 2.1.2, 2.2, 2.4, 2.5 | tidak ada ✓ (2.5: penafsiran 2) |
| SUBKOORDINATOR | 2.1.1, 2.1.2, 2.2, 2.4, 2.5 | tidak ada ✓ (2.5: penafsiran 2) |
| KOORDINATOR | 2.1.1, 2.1.2, 2.2, 2.4, 2.5 | tidak ada ✓ (2.5: penafsiran 2) |
| SEKJEN | 2.1.1, 2.1.2, 2.2, 2.3, 2.4, 2.5 | tidak ada ✓ (2.5: penafsiran 2) |

### 5.3 Bukti aturan "Read Only" → tanpa tulis
| Peran | Permission tulis yang dipegang | Dasar di matriks |
|---|---|---|
| SEKJEN | **tidak ada** | seluruh selnya RO atau "-" ✓ |
| PERWAKILAN, SUBKOOR, KOORDINATOR | `broadcast:trigger`, `broadcast:close` | 2.3.1 "Aktifkan"; close di luar matriks ✓ |
| PIMPINAN | `asesmen:approve`, `tanggap-darurat:close` | 2.5 "Approve"; close di luar matriks ✓ |
| PEGAWAI | `safety-check:respond`, `laporan:create`, `lampiran:upload` | 2.1.1 & 2.2.1 "Tambah, Simpan" ✓ |
| SATGAS | `broadcast:trigger`, `broadcast:close`, `laporan:verify`, `asesmen:create`, `asesmen:update`, `safety-check:record`, `layanan-kritis:create`, `lampiran:upload` | 2.3.1 "Aktifkan", 2.4.1 "Approve/Reject", 2.5 "Tambah, Simpan, Ubah", 2.5.2.1 "Modify/Update"; close di luar matriks ✓ |

`notifikasi:subscribe` dipegang seluruh peran, termasuk Sekretaris Jenderal. Ia hanya
mendaftarkan perangkat milik sendiri untuk menerima pemberitahuan dan tidak mengubah data
bisnis, jadi tidak melanggar "Read Only".

---

## 6. Sieve

Field yang di-Sieve **tetap ada di respons dengan nilai `null`**, tidak dihapus, supaya bentuk
respons stabil bagi Angular. Data yang di-Sieve juga tidak boleh ikut ke log maupun cache
Redis (PLAYBOOK P5.1, P6.3).

| Endpoint | Field | Terlihat untuk | Di-`null`-kan untuk | Alasan |
|---|---|---|---|---|
| #4 `GET /safety-check/rekap` | `data[].lokasiTerakhir` | SATGAS | PEGAWAI, PIMPINAN | Koordinat pegawai adalah data paling sensitif di aplikasi ini, dan hanya diperlukan untuk penjemputan |
| #4 | `data[].keterangan`, `data[].dicatatOleh` | SATGAS, PIMPINAN | PEGAWAI | Catatan Satgas dapat memuat keadaan medis rekan kerja |
| #24, #25, #26, #35 | `aspek.sdm.catatanKondisiPegawai`, `aspek.sdm.catatanTambahan` | SATGAS, PIMPINAN | PERWAKILAN, SUBKOORDINATOR, KOORDINATOR, SEKJEN | Memuat nama dan kondisi pegawai; pemantau cukup angka |
| #30–#35 | — | — | — | **Dirancang tanpa** field nama/NIP/koordinat pegawai sama sekali; pemantau hanya menerima agregat |

Tidak perlu Sieve: #2, #3, #9 (data milik pemanggil sendiri) dan #10/#17 (pembacanya hanya
pelapor sendiri dan Tim Satgas yang memang harus menghubunginya).

---

## 7. Draf kebijakan sebagai data

**[asumsi format]** Disesuaikan setelah BaTII menjawab cara pendaftaran Scope dan Sieve ke IAM
(Lampiran E #6). Isinya sama persis dengan §1–§6. Di sistem baru berkas ini disimpan sebagai
`iam-policy.sigap.json` dan tidak di-hardcode.

```json
{
  "aplikasi": "sigap",
  "versiKebijakan": "2026-09-18",
  "peran": {
    "PEGAWAI":        { "grupSso": "sigap-pegawai" },
    "SATGAS":         { "grupSso": "sigap-satgas" },
    "PIMPINAN":       { "grupSso": "sigap-pimpinan" },
    "PERWAKILAN":     { "grupSso": "sigap-perwakilan" },
    "SUBKOORDINATOR": { "grupSso": "sigap-subkoordinator" },
    "KOORDINATOR":    { "grupSso": "sigap-koordinator" },
    "SEKJEN":         { "grupSso": "sigap-sekjen" },
    "ADMIN":          { "grupSso": "sigap-admin" },
    "PENGEMBANG":     { "grupSso": "sigap-pengembang", "fase": 2 },
    "IMPL_RKB":       { "grupSso": "sigap-impl-rkb", "fase": 2 }
  },
  "profilLingkup": {
    "SELF":     { "predikat": "{pemilik} = @penggunaId" },
    "UNIT":     { "predikat": "{unit} = @unitId" },
    "WILAYAH":  { "predikat": "{unit} IN (SELECT \"id\" FROM \"Unit\" WHERE \"provinsi\" = @provinsi)",     "bilaParameterKosong": "UNIT" },
    "ESELON_I": { "predikat": "{unit} IN (SELECT \"id\" FROM \"Unit\" WHERE \"eselonIKey\" = @eselonIKey)", "bilaParameterKosong": "UNIT" },
    "NASIONAL": { "predikat": null },
    "SASARAN_SAYA": { "predikat": "b.\"selesaiPada\" IS NULL AND EXISTS (SELECT 1 FROM \"BroadcastSasaranUnit\" s WHERE s.\"broadcastId\" = b.\"id\" AND s.\"unitId\" = @unitId AND s.\"status\" = 'DISASAR')" },
    "TERSENTUH":    { "predikat": "EXISTS (SELECT 1 FROM \"BroadcastSasaranUnit\" s WHERE s.\"broadcastId\" = b.\"id\" AND {profil atas s.\"unitId\"}) OR b.\"dikirimOlehId\" = @penggunaId" },
    "IKUT_INDUK":   { "predikat": "baris induk lampiran terlihat menurut permission dan Scope induknya" }
  },
  "gabungMultiPeran": "OR atas profil peran yang memberi permission endpoint; peran lain diabaikan",
  "permission": {
    "sigap:safety-check:read":       { "PEGAWAI": { "GET /safety-check/aktif": "SASARAN_SAYA", "GET /safety-check/respons-saya": "SELF" } },
    "sigap:safety-check:respond":    { "PEGAWAI": "SASARAN_SAYA" },
    "sigap:safety-check:record":     { "SATGAS": "UNIT" },
    "sigap:safety-check-rekap:read": { "PEGAWAI": "UNIT", "PIMPINAN": "UNIT", "SATGAS": "UNIT" },
    "sigap:broadcast:trigger":       { "SATGAS": "UNIT", "PERWAKILAN": "WILAYAH", "SUBKOORDINATOR": "ESELON_I", "KOORDINATOR": "NASIONAL" },
    "sigap:broadcast:read":          { "SATGAS": "TERSENTUH(UNIT)", "PERWAKILAN": "TERSENTUH(WILAYAH)", "SUBKOORDINATOR": "TERSENTUH(ESELON_I)", "KOORDINATOR": "NASIONAL" },
    "sigap:broadcast:close":         { "SATGAS": "PEMICU_ATAU_MENCAKUP", "PERWAKILAN": "PEMICU_ATAU_MENCAKUP", "SUBKOORDINATOR": "PEMICU_ATAU_MENCAKUP", "KOORDINATOR": "NASIONAL" },
    "sigap:laporan:create":          { "PEGAWAI": "UNIT_SENDIRI" },
    "sigap:laporan:read":            { "PEGAWAI": "SELF", "SATGAS": "UNIT" },
    "sigap:laporan:verify":          { "SATGAS": "UNIT" },
    "sigap:asesmen:create":          { "SATGAS": "UNIT_SENDIRI" },
    "sigap:asesmen:update":          { "SATGAS": "UNIT" },
    "sigap:asesmen:read":            { "SATGAS": "UNIT", "PIMPINAN": "UNIT", "PERWAKILAN": "WILAYAH", "SUBKOORDINATOR": "ESELON_I", "KOORDINATOR": "NASIONAL", "SEKJEN": "NASIONAL" },
    "sigap:asesmen:approve":         { "PIMPINAN": "UNIT" },
    "sigap:tanggap-darurat:close":   { "PIMPINAN": "UNIT" },
    "sigap:monitor:read":            { "PERWAKILAN": "WILAYAH", "SUBKOORDINATOR": "ESELON_I", "KOORDINATOR": "NASIONAL", "SEKJEN": "NASIONAL" },
    "sigap:layanan-kritis:read":     { "SATGAS": "UNIT", "PIMPINAN": "UNIT" },
    "sigap:layanan-kritis:create":   { "SATGAS": "UNIT_SENDIRI" },
    "sigap:referensi:read":          { "PEGAWAI": "UNIT", "SATGAS": "UNIT", "PIMPINAN": "UNIT", "PERWAKILAN": "WILAYAH", "SUBKOORDINATOR": "ESELON_I", "KOORDINATOR": "NASIONAL", "SEKJEN": "NASIONAL" },
    "sigap:lampiran:read":           { "PEGAWAI": "IKUT_INDUK", "SATGAS": "IKUT_INDUK", "PIMPINAN": "IKUT_INDUK", "PERWAKILAN": "IKUT_INDUK", "SUBKOORDINATOR": "IKUT_INDUK", "KOORDINATOR": "IKUT_INDUK", "SEKJEN": "IKUT_INDUK" },
    "sigap:lampiran:upload":         { "PEGAWAI": "SELF", "SATGAS": "UNIT" },
    "sigap:notifikasi:read":         { "PEGAWAI": "SELF", "SATGAS": "SELF", "PIMPINAN": "SELF", "PERWAKILAN": "SELF", "SUBKOORDINATOR": "SELF", "KOORDINATOR": "SELF", "SEKJEN": "SELF" },
    "sigap:notifikasi:subscribe":    { "PEGAWAI": "SELF", "SATGAS": "SELF", "PIMPINAN": "SELF", "PERWAKILAN": "SELF", "SUBKOORDINATOR": "SELF", "KOORDINATOR": "SELF", "SEKJEN": "SELF" }
  },
  "sieve": [
    { "endpoint": "GET /safety-check/rekap", "field": "data[].lokasiTerakhir",                  "terlihatUntuk": ["SATGAS"] },
    { "endpoint": "GET /safety-check/rekap", "field": "data[].keterangan",                      "terlihatUntuk": ["SATGAS", "PIMPINAN"] },
    { "endpoint": "GET /safety-check/rekap", "field": "data[].dicatatOleh",                     "terlihatUntuk": ["SATGAS", "PIMPINAN"] },
    { "endpoint": "GET /asesmen*",           "field": "aspek.sdm.catatanKondisiPegawai",        "terlihatUntuk": ["SATGAS", "PIMPINAN"] },
    { "endpoint": "GET /asesmen*",           "field": "aspek.sdm.catatanTambahan",              "terlihatUntuk": ["SATGAS", "PIMPINAN"] },
    { "endpoint": "GET /monitor/unit/{unitId}", "field": "asesmenTerkini.aspek.sdm.catatanKondisiPegawai", "terlihatUntuk": [] },
    { "endpoint": "GET /monitor/unit/{unitId}", "field": "asesmenTerkini.aspek.sdm.catatanTambahan",       "terlihatUntuk": [] }
  ]
}
```

`UNIT_SENDIRI` = tulis selalu atas nama unit pemanggil (§2.4). `PEMICU_ATAU_MENCAKUP` =
pemicunya, atau lingkup pengakhir mencakup seluruh unit `DISASAR` broadcast.

---

## 8. Pertanyaan untuk BaTII terkait IAM

Daftar lengkap ada di API_CONTRACT §9. Yang menyangkut dokumen ini:
1. Apakah format `sigap:resource:action` sesuai konvensi platform? (Lampiran E #5)
2. Cara mendaftarkan Scope dan Sieve: UI admin, berkas konfigurasi seperti §7, atau API?
   (Lampiran E #6)
3. Bagaimana `iam.plugin` menggabungkan Scope pengguna berperan ganda: OR seperti §2.3, atau
   prioritas?
4. Dapatkah Scope dinyatakan sebagai subquery, seperti profil `WILAYAH` dan `SASARAN_SAYA`,
   atau hanya kesamaan kolom?
5. Apakah `iam.plugin` sudah menyediakan konteks pengguna (daftar permission & lingkup) untuk
   `*hasPermission`, sehingga #36 tidak perlu dibangun?
