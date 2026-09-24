# API_CONTRACT — sigap-api, Fase 1 (Tanggap Darurat)

Status: **disetujui untuk dibangun**, 18 September 2026. Pasangan dokumen ini:
[PERMISSION_MAP.md](PERMISSION_MAP.md) (peta izin, Scope, dan Sieve untuk pendaftaran IAM).

Sumber, urut prioritas (PLAYBOOK Lampiran F): Standar Arsitektur ICS → koreksi stakeholder
(Lampiran C) → matriks `docs/Catatan-Masukan-Probis.xlsx` sheet "Fitur & Data per Role"
butir 2.1–2.6 → Dokumen UR → `docs/desain-probis-v15.html` (rujukan tampilan saja).
Skema data: 32 tabel prototipe `MKB APPS/App/prisma/schema.prisma` + satu tabel baru yang
disetujui (bagian 5).

Kontrak ini ditulis seolah platform asli sudah ada: autentikasi SSO Kemenkeu, otorisasi
`iam.plugin` tiga lapis, gateway ICS. Bagian yang masih asumsi ditandai **[asumsi]** dan
terkumpul di bagian 9.

---

## 1. Konvensi umum

### 1.1 Alamat dan versi
- Awalan seluruh endpoint bisnis: `/api/v1` **[asumsi — menunggu standar gateway ICS]**.
- Health check di luar awalan: `/health/live`, `/health/ready` (tanpa autentikasi).
- Perubahan yang merusak kompatibilitas menaikkan versi (`/api/v2`), tidak mengubah `v1`.

### 1.2 Autentikasi dan identitas
- Header `Authorization: Bearer <access_token>` dari SSO Kemenkeu (OIDC, realm `kemenkeu`,
  audience `sigap-api`).
- Klaim yang dipakai: `nip` (atau `preferred_username`), `kode_satker`, `kode_eselon1`,
  `groups`.
- **Peran** diambil dari klaim `groups` (pemetaan grup→peran di PERMISSION_MAP bagian 1), **bukan**
  dari tabel `"UserRole"`.
- **Pengguna dan unit** dicari lewat `nip` → `"User"."nip"` → `"User"."id"`, `"User"."unitId"`.
  Tabel `"User"` diperlakukan sebagai profil rujukan untuk kolom FK (mis. `submittedById`),
  bukan sistem login. **[asumsi — cara tabel `"User"` diisi dari HRIS/SSO perlu dipastikan]**
- Parameter Scope turunan identitas: `@penggunaId`, `@unitId`, `@provinsi` (=
  `"Unit"."provinsi"` unit pengguna), `@eselonIKey` (= `"Unit"."eselonIKey"`).

### 1.3 Format data
- JSON, nama field camelCase berbahasa Indonesia mengikuti istilah domain.
- Waktu: ISO-8601 UTC (`2026-09-18T03:05:00Z`). Konversi ke WIB/WITA/WIT urusan tampilan.
- ID: string (cuid, sesuai skema).
- **Nilai berskala memakai kode**: opsi asesmen, level keparahan, kondisi fisik. Contohnya
  `RUSAK_RINGAN`. Pemetaan kode ↔ nilai tersimpan di bagian 3.5.3.
- **Nama jenis bencana dipakai apa adanya**, misalnya `"Gempa Bumi"`, karena nama baku UU
  24/2007 itu sendiri pengenalnya dan tersimpan persis begitu. Daftar sahnya ada di
  `GET /referensi/jenis-bencana`.
- Nilai tersimpan yang tidak dikenal pemetaan (data lama) dikembalikan sebagai
  `"TIDAK_DIKENAL"` dan dicatat ke log, tidak menggagalkan respons.

### 1.4 Koleksi dan paginasi
```json
{ "data": [ … ], "halaman": 1, "ukuran": 20, "total": 134 }
```
Parameter `halaman` (mulai 1), `ukuran` (bawaan 20, maks 100). **[asumsi — bentuk amplop]**

### 1.5 Galat
`application/problem+json` (RFC 7807, `ProblemDetails` ASP.NET Core) ditambah `kode` yang
stabil untuk dibaca mesin. **[asumsi — menunggu standar ICS]**
```json
{
  "type": "https://sigap.kemenkeu.go.id/galat/LAPORAN_SUDAH_DIVERIFIKASI",
  "title": "Laporan sudah diverifikasi",
  "status": 409,
  "detail": "Laporan ini sudah diverifikasi Budi Santoso pada 18 Sep 2026 10.12 WIB.",
  "kode": "LAPORAN_SUDAH_DIVERIFIKASI",
  "errors": { "lokasi": ["Lokasi wajib diisi."] }
}
```
`errors` hanya ada pada 400.

| Status | Kapan |
|---|---|
| 400 `VALIDASI_GAGAL` | Masukan tidak sah (format, wajib, panjang, nilai di luar daftar) |
| 401 | Token tidak ada/kedaluwarsa |
| 403 `TIDAK_BERWENANG` | Peran tidak memegang permission endpoint |
| 404 `TIDAK_DITEMUKAN` | Tidak ada, **atau di luar Scope pemanggil** — sengaja tidak dibedakan agar keberadaan data di luar lingkup tidak bocor |
| 409 | Benturan keadaan (kode spesifik per endpoint) |
| 413 / 415 | Lampiran terlalu besar / tipe ditolak |
| 422 | Aturan bisnis menolak permintaan yang formatnya sah (kode spesifik) |

### 1.6 Keamanan tiga lapis
Setiap endpoint mencantumkan **Permission** (lapis 1, `[KemenkeuAuthorize]`), **Scope**
(lapis 2, klausa WHERE), dan **Sieve** (lapis 3, field di-null-kan). Definisi lengkap profil
Scope (`SELF`, `UNIT`, `WILAYAH`, `ESELON_I`, `NASIONAL`, `SASARAN_SAYA`) dan aturan
gabung multi-peran ada di PERMISSION_MAP bagian 2. Ringkasnya:
- Scope ditentukan oleh **peran yang memberi permission endpoint itu**, bukan peran terluas
  pengguna.
- Scope selalu diterapkan di query database, tidak pernah menyaring hasil di memori.
- Kode aplikasi tidak menulis `if (role == …)` sendiri.

### 1.7 Jejak audit
Setiap tindakan tulis dicatat terpusat (interceptor, bukan per endpoint) ke `"JejakPerubahan"`
(`entitas`, `entitasId`, `aksi`, `olehId`, `alasan`, `ringkasan`). `ringkasan` memuat JSON
nilai sebelum/sesudah, karena tabelnya tidak punya kolom khusus untuk itu. Akses baca ke data
paling sensitif (daftar keadaan per pegawai, koordinat) juga dicatat. **[asumsi — diganti
audit trail bawaan `iam.plugin` bila tersedia, Lampiran E #10]**

### 1.8 Kiriman ulang dan jaringan buruk
Pegawai di lokasi bencana sering mengirim ulang. Tidak ada header idempotensi. Kiriman ulang
ditangani aturan bisnis yang sama dengan prototipe:
- Jawaban safety check memakai `PUT` (upsert per pegawai per broadcast), jadi aman diulang.
- Laporan bencana: pelapor + jenis + lokasi yang sama dalam 2 menit → 409 `LAPORAN_KEMBAR`.
- Asesmen: pengirim yang sama dalam 2 menit → 409 `ASESMEN_KEMBAR`.

---

## 2. Daftar endpoint (47)

| # | Method & path | Kode matriks | Permission |
|---|---|---|---|
| **2.1 Safety Check / SOS** ||||
| 1 | `GET /safety-check/aktif` | 2.1.1 | `sigap:safety-check:read` |
| 2 | `PUT /safety-check/broadcast/{broadcastId}/respons-saya` | 2.1.1 | `sigap:safety-check:respond` |
| 3 | `GET /safety-check/respons-saya` | 2.1.2 | `sigap:safety-check:read` |
| 4 | `GET /safety-check/rekap` | 2.1.3, 2.5.2.1 | `sigap:safety-check-rekap:read` |
| 5 | `GET /safety-check/rekap/ringkasan` | 2.1.3, 2.5.2.1 | `sigap:safety-check-rekap:read` |
| 6 | `PUT /safety-check/broadcast/{broadcastId}/respons/{pegawaiId}` | 2.5.2.1 | `sigap:safety-check:record` |
| **2.2 Laporkan Potensi Bencana** ||||
| 7 | `POST /laporan-bencana` | 2.2.1 | `sigap:laporan:create` |
| 8 | `POST /laporan-bencana/{id}/lampiran` | 2.2.1 | `sigap:lampiran:upload` |
| 9 | `GET /laporan-bencana/saya` | 2.2.2 | `sigap:laporan:read` |
| 10 | `GET /laporan-bencana/{id}` | 2.2.2, 2.4.1 | `sigap:laporan:read` |
| 11 | `GET /lampiran/{id}` | 2.2, 2.4, 2.5 | `sigap:lampiran:read` |
| **2.3 Trigger Safety Check** ||||
| 12 | `GET /safety-check/broadcast/pratinjau` | 2.3.1 | `sigap:broadcast:trigger` |
| 13 | `POST /safety-check/broadcast` | 2.3.1 | `sigap:broadcast:trigger` |
| 14 | `GET /safety-check/broadcast` | 2.3.2 | `sigap:broadcast:read` |
| 15 | `GET /safety-check/broadcast/{id}` | 2.3.2 | `sigap:broadcast:read` |
| 16 | `POST /safety-check/broadcast/{id}/selesai` | *di luar matriks* | `sigap:broadcast:close` |
| **2.4 Verifikasi Alert Bencana** ||||
| 17 | `GET /laporan-bencana` | 2.4.1 | `sigap:laporan:read` |
| 18 | `POST /laporan-bencana/{id}/verifikasi` | 2.4.1 | `sigap:laporan:verify` |
| **2.5 Asesmen Kondisi Bencana** ||||
| 19 | `GET /layanan-kritis` | 2.5.6 | `sigap:layanan-kritis:read` |
| 20 | `POST /layanan-kritis` | 2.5.6 (koreksi 7) | `sigap:layanan-kritis:create` |
| 21 | `POST /asesmen` | 2.5.1–2.5.6 | `sigap:asesmen:create` |
| 22 | `POST /asesmen/{id}/revisi` | 2.5.2–2.5.6 "Ubah" | `sigap:asesmen:update` |
| 23 | `POST /asesmen/{id}/lampiran` | 2.5.3 | `sigap:lampiran:upload` |
| 24 | `GET /asesmen` | 2.5, 2.6.2 | `sigap:asesmen:read` |
| 25 | `GET /asesmen/terkini` | 2.5 | `sigap:asesmen:read` |
| 26 | `GET /asesmen/{id}` | 2.5, 2.6.2 | `sigap:asesmen:read` |
| 27 | `GET /asesmen/{id}/versi` | 2.5 | `sigap:asesmen:read` |
| 28 | `POST /asesmen/{id}/persetujuan` | 2.5 "Approve" | `sigap:asesmen:approve` |
| 29 | `POST /tanggap-darurat/{id}/selesai` | *di luar matriks* | `sigap:tanggap-darurat:close` |
| **2.6 Dashboard Monitor SC & Sumber Daya** ||||
| 30 | `GET /monitor/ringkasan` | 2.6 | `sigap:monitor:read` |
| 31 | `GET /monitor/safety-check` | 2.1.3→2.6 | `sigap:monitor:read` |
| 32 | `GET /monitor/asesmen-masuk` | 2.6.1 | `sigap:monitor:read` |
| 33 | `GET /monitor/aspek` | 2.6.2 | `sigap:monitor:read` |
| 34 | `GET /monitor/layanan` | 2.6.2 (Lihat Detail) | `sigap:monitor:read` |
| 35 | `GET /monitor/unit/{unitId}` | 2.6.2 (Lihat Detail) | `sigap:monitor:read` |
| **Pendukung** ||||
| 36 | `GET /me/konteks` | — | (terautentikasi) |
| 37 | `GET /referensi/jenis-bencana` | 2.2.1, 2.3.1, 2.5.1 | `sigap:referensi:read` |
| 38 | `GET /referensi/opsi-asesmen` | 2.5 | `sigap:referensi:read` |
| 39 | `GET /referensi/provinsi` | 2.3, 2.6 | `sigap:referensi:read` |
| 40 | `GET /referensi/kabupaten-kota` | 2.3, 2.6 | `sigap:referensi:read` |
| 41 | `GET /referensi/eselon-1` | 2.3, 2.6 | `sigap:referensi:read` |
| 42 | `GET /referensi/unit` | 2.3, 2.6 | `sigap:referensi:read` |
| 43 | `GET /notifikasi` | 2.6.1 dll. | `sigap:notifikasi:read` |
| 44 | `POST /notifikasi/langganan` | — | `sigap:notifikasi:subscribe` |
| 45 | `DELETE /notifikasi/langganan` | — | `sigap:notifikasi:subscribe` |
| 46 | `GET /health/live` | — | publik |
| 47 | `GET /health/ready` | — | publik |

Kebutuhan Teknis memperkirakan 60–80 endpoint. Angka itu mencakup seluruh sistem termasuk
Fase 2 (enam modul dokumen MKB, eksekusi RKB, LPKB). Butir 2.1–2.6 menghasilkan 47, dan
tidak digelembungkan untuk mengejar angka.

---

## 3. Detail endpoint

Setiap entri: permission · Scope per peran · Sieve · request · response · galat khusus.
Path ditulis tanpa awalan `/api/v1`.

### Potongan objek yang dipakai berulang
```jsonc
// RingkasUnit
{ "id": "…", "nama": "KPP Madya Pekanbaru", "provinsi": "Riau", "kabupatenKota": "Kota Pekanbaru", "eselonI": "djp" }
// RingkasPengguna
{ "id": "…", "nama": "Afrizal Rizky Barkah", "nip": "199101052013101001", "jabatan": "Penelaah Teknis Kebijakan" }
// Lampiran — url SELALU path API ber-autentikasi, tidak pernah URL object storage/presigned
{ "id": "…", "tipe": "FOTO", "mimeType": "image/jpeg", "ukuranBytes": 482113, "url": "/api/v1/lampiran/…", "diunggahPada": "…" }
// RingkasBroadcast
{ "id": "…", "kategoriBencana": "ALAM", "jenisBencana": "Gempa Bumi", "lokasi": "Provinsi Riau",
  "sumber": "MANUAL", "lingkup": "WILAYAH", "dipicuPada": "…", "status": "AKTIF",
  "pemicu": { "pengguna": RingkasPengguna, "peran": "PERWAKILAN", "unit": RingkasUnit } }
```

---

### 3.1 Safety Check / SOS

**Aturan pokok.** Hanya peran Pegawai Umum yang menjawab (koreksi 1). Jawabannya cuma dua,
`AMAN` atau `BUTUH_BANTUAN`, tanpa isian lain (koreksi 2). Tiap jawaban **terikat pada
satu broadcast tertentu** (bagian 6 butir 2). Seorang pegawai bisa sedang ditanya oleh
lebih dari satu broadcast sekaligus bila jenis bencananya berbeda.

#### 1. `GET /safety-check/aktif`
Broadcast yang sedang berjalan dan menyasar unit pemanggil. Dipakai popup dan beranda.
- **Permission** `sigap:safety-check:read` · **Scope** PEGAWAI: `SASARAN_SAYA` · **Sieve** —
- Urut dari yang **paling lama** dipicu. Tampilan wajib menampilkan seluruhnya, supaya
  broadcast yang lebih lama tidak tenggelam oleh yang lebih baru.
```json
{ "data": [
  { "broadcast": RingkasBroadcast,
    "pesan": "Terjadi gempa di sekitar kantor. Mohon segera konfirmasi kondisi Anda.",
    "responsSaya": { "status": "AMAN", "dijawabPada": "2026-09-18T03:07:12Z" } }
] }
```
`responsSaya` bernilai `null` bila belum menjawab.

#### 2. `PUT /safety-check/broadcast/{broadcastId}/respons-saya`
Menjawab, atau mengubah jawaban. Jawaban bisa berubah dari aman menjadi butuh bantuan, dan
justru kabar terbaru itu yang menentukan siapa dijemput lebih dulu.
- **Permission** `sigap:safety-check:respond` · **Scope** PEGAWAI: `SASARAN_SAYA` (unit
  pegawai harus berstatus `DISASAR` pada broadcast ini) · **Sieve** —
```json
{ "status": "BUTUH_BANTUAN", "lat": -0.5071, "lng": 101.4478 }
```
- `lat`/`lng` opsional, diambil peramban tanpa isian pengguna. Nilai di luar rentang bumi
  diperlakukan `null`, **tidak ditolak**: kegagalan lokasi tidak boleh menghalangi kabar
  keselamatan.
- Efek: upsert `"SafetyCheckResponse"` per (`userId`, `broadcastId`), `unitId` = unit
  pegawai, waktu jawab diperbarui meski statusnya sama. `dicatatOlehId` dan `keterangan`
  dikosongkan, sebab jawaban kini dari pegawainya sendiri. Nilai sebelumnya tersimpan di
  jejak audit. `BUTUH_BANTUAN` memunculkan peringatan bagi Tim Satgas dan Pimpinan unit.
```json
{ "broadcastId": "…", "status": "BUTUH_BANTUAN", "dijawabPada": "…", "perubahan": "DIUBAH" }
```
`perubahan`: `BARU` | `DIUBAH` | `DITEGASKAN_ULANG` (status sama, waktu diperbarui).
- Galat: 404 (broadcast tidak ada / unit tidak disasar), 409 `BROADCAST_SUDAH_SELESAI`.

#### 3. `GET /safety-check/respons-saya`
Riwayat safety check pemanggil (2.1.2), terbaru lebih dulu, berhalaman.
- **Permission** `sigap:safety-check:read` · **Scope** PEGAWAI: `SELF` · **Sieve** —
```json
{ "data": [ { "broadcast": RingkasBroadcast, "status": "AMAN", "dijawabPada": "…", "dicatatkanSatgas": false } ], … }
```

#### 4. `GET /safety-check/rekap`
Daftar keadaan per pegawai **untuk satu broadcast di unit pemanggil**. Melayani 2.1.3
(Pegawai, Pimpinan) dan 2.5.2.1 (Tim Satgas, di dalam aspek SDM asesmen). Empat peran
pemantau tidak memakai endpoint ini, sebab matriks 2.1.3 menyatakan rekap mereka "menjadi
dashboard Monitor SC" (bagian 3.6).
- **Permission** `sigap:safety-check-rekap:read` · **Scope** PEGAWAI, PIMPINAN, SATGAS:
  `UNIT`, dan unit itu harus `DISASAR` pada broadcast yang diminta.
- **Sieve**: `lokasiTerakhir` hanya SATGAS. `keterangan`, `dicatatOleh` hanya SATGAS dan
  PIMPINAN.
- Query: `broadcastId` (opsional), `status` = `BUTUH_BANTUAN`|`BELUM`|`AMAN` (tab per
  kondisi, koreksi 11), `cari`, `halaman`, `ukuran`.
- Tanpa `broadcastId`, dipakai broadcast aktif yang memegang unit pemanggil dan paling baru
  dipicu. Broadcast aktif lain yang juga memegang unit itu (jenis bencana berbeda) disebut
  di `broadcastLainAktif`.
- Penyebut: pegawai aktif berperan Pegawai Umum di unit itu. Tanpa baris jawaban berarti
  `BELUM`.
- Urutan: `BUTUH_BANTUAN`, `BELUM`, `AMAN`, lalu nama.
```json
{
  "broadcast": RingkasBroadcast,
  "broadcastLainAktif": [ { "id": "…", "jenisBencana": "Banjir" } ],
  "unit": RingkasUnit,
  "data": [ {
    "pegawai": RingkasPengguna,
    "status": "BUTUH_BANTUAN",
    "dijawabPada": "…",
    "dicatatkan": true,
    "dicatatOleh": { "id": "…", "nama": "…" },
    "keterangan": "Dihubungi lewat telepon oleh Satgas pukul 10.12",
    "lokasiTerakhir": { "lat": -0.5071, "lng": 101.4478, "pada": "…" }
  } ],
  "halaman": 1, "ukuran": 20, "total": 58
}
```

#### 5. `GET /safety-check/rekap/ringkasan`
Angka untuk penanda tab. Permission, Scope, dan pemilihan broadcast sama dengan #4.
```json
{ "broadcast": RingkasBroadcast, "unit": RingkasUnit,
  "totalPegawai": 58, "aman": 49, "butuhBantuan": 3, "belumMerespons": 6, "tingkatRespons": 0.897 }
```
`tingkatRespons` = (aman + butuhBantuan) / totalPegawai.

#### 6. `PUT /safety-check/broadcast/{broadcastId}/respons/{pegawaiId}`
Tim Satgas mencatatkan keadaan pegawai yang tidak dapat menjawab sendiri, misalnya terluka,
ponselnya mati, atau sedang dievakuasi ("Modify/Update" pada 2.5.2.1).
- **Permission** `sigap:safety-check:record` · **Scope** SATGAS: `UNIT` (unit pegawai =
  unit Satgas, dan unit itu `DISASAR` pada broadcast) · **Sieve** —
```json
{ "status": "AMAN", "alasan": "Dihubungi lewat telepon oleh Satgas pukul 10.12" }
```
- `alasan` wajib, 5–300 karakter. Pernyataan keselamatan orang lain harus punya dasar yang
  dapat dipertanggungjawabkan.
- Efek: upsert jawaban dengan `dicatatOlehId` = Satgas dan `keterangan` = alasan. Audit
  `DICATATKAN` / `DICATATKAN_ULANG`.
```json
{ "pegawaiId": "…", "broadcastId": "…", "status": "AMAN", "dicatatOleh": RingkasPengguna, "dicatatPada": "…", "perubahan": "BARU" }
```

---

### 3.2 Laporkan Potensi Bencana

**Objek `Laporan`**
```json
{
  "id": "…", "unit": RingkasUnit, "pelapor": RingkasPengguna,
  "kategoriBencana": "ALAM", "jenisBencana": "Banjir", "level": "SEDANG",
  "lokasi": "Lantai 1 Gedung A", "deskripsi": "Air masuk setinggi 20 cm",
  "status": "MENUNGGU",
  "verifikasi": null,
  "lampiran": [ Lampiran ],
  "dilaporkanPada": "…"
}
```
`verifikasi` setelah diputus: `{ "keputusan": "TOLAK", "alasan": "…", "oleh": RingkasPengguna, "pada": "…" }`.
`level`: `SANGAT_RINGAN` | `RINGAN` | `SEDANG` | `BERAT` | `SANGAT_BERAT`.

#### 7. `POST /laporan-bencana`
- **Permission** `sigap:laporan:create` · **Scope** PEGAWAI: laporan selalu atas nama unit
  pelapor sendiri (`unitId` dari identitas, tidak dari body).
```json
{ "jenisBencana": "Banjir", "level": "SEDANG", "lokasi": "Lantai 1 Gedung A", "deskripsi": "Air masuk setinggi 20 cm" }
```
- Validasi: `jenisBencana` wajib dan terdaftar di taksonomi (`kategoriBencana` diisi
  sistem). `lokasi` wajib, ≤200. `deskripsi` opsional, ≤2000.
- 201 + `Location`, isi `Laporan`. Efek: peringatan ke Tim Satgas unit.
- Galat: 409 `LAPORAN_KEMBAR`.

#### 8. `POST /laporan-bencana/{id}/lampiran`
`multipart/form-data`, satu berkas per panggilan di field `berkas`.
- **Permission** `sigap:lampiran:upload` · **Scope** PEGAWAI: `SELF` (hanya pelapornya),
  selama status `MENUNGGU`.
- Tipe: `image/jpeg`, `image/png` (FOTO); `video/mp4` (VIDEO); `audio/mpeg`, `audio/mp4`,
  `audio/ogg`, `audio/webm` (AUDIO, pesan suara). Maks 10 MB per berkas, maks 5 berkas per
  laporan. **[asumsi — lihat bagian 9]**
- 201, isi `Lampiran`. Galat: 413 `LAMPIRAN_TERLALU_BESAR`, 415 `LAMPIRAN_TIPE_DITOLAK`,
  409 `BATAS_LAMPIRAN`, 409 `LAPORAN_SUDAH_DIVERIFIKASI`, 503 `LAMPIRAN_GAGAL_DISIMPAN`
  (laporannya tetap ada, boleh diulang).

#### 9. `GET /laporan-bencana/saya`
Riwayat laporan pemanggil (2.2.2), terbaru lebih dulu. Termasuk alasan penolakan, supaya
pelapor mengerti dasar keputusan (UAT D2).
- **Permission** `sigap:laporan:read` · **Scope** `SELF` (`"DisasterAlert"."pelaporId" = @penggunaId`)

#### 10. `GET /laporan-bencana/{id}`
- **Permission** `sigap:laporan:read` · **Scope** PEGAWAI: `SELF`, SATGAS: `UNIT`
- **Sieve** — (peran lain tidak punya akses, dan pelapor adalah dirinya sendiri)

#### 11. `GET /lampiran/{id}`
Mengalirkan isi berkas. Header `Content-Type`, `Content-Disposition: inline`,
`Cache-Control: private, no-store`.
- **Permission** `sigap:lampiran:read` · **Scope**: **mengikuti induk**. Lampiran laporan
  hanya terbaca oleh yang boleh membaca laporannya, dan lampiran asesmen oleh yang boleh
  membaca asesmennya. Di luar itu 404.

---

### 3.3 Trigger Safety Check

#### 3.3.1 Aturan kepemilikan unit (keputusan 18 Sep 2026)

1. **Satu unit dipegang paling banyak satu broadcast aktif per jenis bencana.**
2. Trigger baru **tidak ditolak**. Unit sasaran yang sudah dipegang broadcast aktif lain
   untuk jenis bencana yang sama **dilewati**, seluruh pegawainya. Unit sisanya dipegang
   trigger baru. Pegawai unit yang dilewati tetap sedang ditanya lewat broadcast pemegangnya,
   jadi tidak ada yang luput.
3. Jenis bencana berbeda tidak saling melewati. Banjir yang aktif tidak menghalangi Gempa.
4. **Sasaran dikunci saat tombol ditekan.** Bila broadcast pemegang selesai belakangan, unit
   itu **tidak** otomatis masuk ke broadcast yang tadi melewatinya.
5. **Sasaran = unit yang cocok kriterianya secara positif**, sama persis dengan lingkup baca
   peran pemicu. Unit yang kolom provinsi/kabupaten-kota/Eselon I-nya kosong **tidak**
   cocok dengan penyempit tersebut. Contohnya, kanwil Riau tidak menyasar unit kantor pusat.
6. Trigger otomatis BMKG tunduk pada aturan yang sama. Trigger manual tetap diterima; hanya
   unit yang sudah dipegang trigger otomatis untuk jenis yang sama yang dilewati (koreksi 12:
   trigger otomatis tidak menghalangi trigger manual).
7. Setiap trigger menyimpan identitas lengkap: ID, pemicu, peran dan unit pemicu, lingkup,
   kriteria, dan waktu. Daftar unit disasar dan dilewati (beserta pemegangnya) tersimpan di
   `"BroadcastSasaranUnit"` (bagian 5). Peran dan unit pemicu pada saat memicu direkam di jejak
   audit, sebab `"ActiveBroadcast"` hanya menyimpan `dikirimOlehId`.

**Contoh.** Unit A (unit Afrizal) sudah dipegang trigger Gempa dari Satgas A. Kanwil Riau
memicu Gempa se-provinsi. Hasilnya unit B dan C dipegang trigger kanwil, dan unit A tercatat
`DILEWATI` karena dipegang trigger Satgas A pukul 10.05. Pegawai unit A tidak menerima
popup kedua.

#### 3.3.2 Lingkup dan penyempit per peran

| Peran | Lingkup | Kandidat unit | Penyempit yang boleh dikirim |
|---|---|---|---|
| Tim Satgas | `UNIT` | unit pemicu | — |
| Kepala Perwakilan | `WILAYAH` | `provinsi` = provinsi unit pemicu | `unitId` (satu unit di provinsinya) **atau** `kabupatenKota` dan/atau `eselonI` |
| Subkoordinator | `ESELON_I` | `eselonIKey` = Eselon I unit pemicu | `provinsi`, `kabupatenKota` |
| Koordinator MKB | `NASIONAL` | seluruh unit | `eselonI`, `provinsi`, `kabupatenKota` |

Sekretaris Jenderal tidak memicu (matriks 2.3: "-"). Bila pengguna memegang beberapa peran
pemicu, dipakai urutan prototipe: Koordinator → Kepala Perwakilan → Subkoordinator → Satgas.
Provinsi/Eselon I **selalu** diturunkan dari unit pemicu, tidak pernah dari body. Penyempit di
luar kolom tabel → 400 `PENYEMPIT_TIDAK_BERLAKU`. Kepala Perwakilan atau Subkoordinator yang
provinsi/Eselon I unitnya kosong → 422 `DATA_UNIT_PEMICU_TIDAK_LENGKAP` (fail-closed,
sama dengan `lingkup.ts`).

#### 12. `GET /safety-check/broadcast/pratinjau`
Menghitung sasaran tanpa memicu, supaya pemicu tahu dampaknya lebih dulu. Pemicuan yang
sunyi lebih berbahaya daripada penolakan yang terbaca.
- **Permission** `sigap:broadcast:trigger` · **Scope**: kandidat menurut bagian 3.3.2
- Query: `jenisBencana` (wajib), `unitId`, `provinsi`, `kabupatenKota`, `eselonI`
```json
{
  "lingkupPemicu": "WILAYAH",
  "lokasi": "Provinsi Riau",
  "disasar": { "jumlahUnit": 2, "jumlahPegawai": 131, "unit": [ RingkasUnit ] },
  "dilewati": [ { "unit": RingkasUnit,
                  "dipegangOleh": { "broadcastId": "…", "jenisBencana": "Gempa Bumi", "pemicu": { "nama": "…", "peran": "SATGAS" }, "dipicuPada": "…" } } ]
}
```

#### 13. `POST /safety-check/broadcast`
- **Permission** `sigap:broadcast:trigger` · **Scope tulis**: bagian 3.3.2
```json
{
  "kategoriBencana": "ALAM",
  "jenisBencana": "Gempa Bumi",
  "pesan": "Terjadi gempa di wilayah Riau. Segera konfirmasi kondisi Anda.",
  "penyempit": { "unitId": null, "provinsi": null, "kabupatenKota": "Kota Pekanbaru", "eselonI": null }
}
```
- Isi formulir sesuai koreksi 13: kategori ancaman, jenis ancaman, pesan, target sesuai peran.
  `kategoriBencana` harus sesuai `jenisBencana` di taksonomi. `pesan` opsional (ada teks
  bawaan), ≤500.
- Dalam **satu transaksi**:
  1. Buat `"ActiveBroadcast"`.
  2. Untuk tiap unit kandidat: bila ada baris `DISASAR` aktif untuk (unit, jenis bencana),
     sisipkan `DILEWATI` dengan `dilewatiKarenaBroadcastId` = pemegangnya. Selain itu
     sisipkan `DISASAR`. Indeks unik parsial (bagian 5) menahan dua trigger bersamaan. Bila
     penyisipan `DISASAR` benturan, baca ulang pemegangnya dan catat `DILEWATI`.
  3. Rekam identitas pemicu ke jejak audit (aksi `DIPICU`).
- Sesudah transaksi: notifikasi hanya ke pegawai aktif berperan Pegawai Umum di unit
  `DISASAR`. Unit yang dilewati tidak menerima notifikasi kedua.
- 201, isi `DetailBroadcast` (#15).
- Galat: 422 `SASARAN_KOSONG` (tidak ada unit cocok), 409 `SELURUH_SASARAN_SUDAH_DIPEGANG`
  (semua kandidat dilewati; tidak dibuat broadcast kosong, rincian pemegang ada di `detail`),
  422 `DATA_UNIT_PEMICU_TIDAK_LENGKAP`, 400 `PENYEMPIT_TIDAK_BERLAKU`.

#### 14. `GET /safety-check/broadcast`
Riwayat trigger (2.3.2). Memuat broadcast peran mana pun yang menyentuh lingkup pemanggil,
sebab yang perlu diketahui justru apakah orang lain sudah memicu lebih dulu.
- **Permission** `sigap:broadcast:read` · **Scope**: broadcast yang punya baris
  `"BroadcastSasaranUnit"` (status apa pun) dengan unit di lingkup pemanggil (SATGAS
  `UNIT`, PERWAKILAN `WILAYAH`, SUBKOORDINATOR `ESELON_I`, KOORDINATOR `NASIONAL`),
  **atau** pemanggil adalah pemicunya.
- Query: `status` (`AKTIF`|`SELESAI`), `jenisBencana`, `sumber`, `sejak`, `halaman`
- Isi `data`: `RingkasBroadcast` ditambah `jumlahUnitDisasar`, `jumlahPegawaiDisasar`,
  `jumlahMenjawab`.

#### 15. `GET /safety-check/broadcast/{id}` — `DetailBroadcast`
- **Permission** `sigap:broadcast:read` · **Scope** sama dengan #14
```json
{
  "id": "…", "kategoriBencana": "ALAM", "jenisBencana": "Gempa Bumi", "pesan": "…",
  "lokasi": "Kota Pekanbaru, Provinsi Riau",
  "sumber": "MANUAL", "mmiTertinggi": null,
  "lingkup": "WILAYAH",
  "kriteria": { "unitId": null, "provinsi": "Riau", "kabupatenKota": "Kota Pekanbaru", "eselonI": null },
  "pemicu": { "pengguna": RingkasPengguna, "peran": "PERWAKILAN", "unit": RingkasUnit },
  "dipicuPada": "…",
  "status": "AKTIF",
  "diakhiri": null,
  "sasaran": {
    "jumlahUnitDisasar": 2, "jumlahPegawaiDisasar": 131, "jumlahMenjawab": 97,
    "unitDisasar": [ RingkasUnit ],
    "unitDilewati": [ { "unit": RingkasUnit, "dipegangOleh": { … } } ]
  }
}
```
Angka `jumlah*` selalu penuh. Daftar `unitDisasar`/`unitDilewati` disaring ke lingkup
pembaca. `sumber`: `MANUAL` | `OTOMATIS_BMKG`. `diakhiri`: `{ "oleh": RingkasPengguna, "pada": "…", "alasan": "…" }`.

#### 16. `POST /safety-check/broadcast/{id}/selesai` — di luar matriks
Tanpa ini broadcast tidak pernah padam. **Perlu konfirmasi pemilik proses bisnis.**
- **Permission** `sigap:broadcast:close` · **Otorisasi**: pemicunya sendiri, **atau**
  lingkup pengakhir mencakup **seluruh** unit `DISASAR` broadcast itu. Koordinator dapat
  mengakhiri apa pun. Kepala Perwakilan hanya broadcast yang seluruh sasarannya di
  provinsinya. Satgas hanya miliknya sendiri. Terlihat tetapi tidak berhak → 403
  `TIDAK_BERWENANG_MENGAKHIRI`.
- Body opsional: `{ "alasan": "Kondisi sudah aman" }` (≤300).
- Efek: `selesaiPada`, `diakhiriOlehId`, lalu seluruh baris `"BroadcastSasaranUnit"`-nya
  `aktif = false`. Unitnya bebas dipegang trigger **berikutnya**, tetapi tidak dialihkan ke
  broadcast yang sudah berjalan.
- Galat: 409 `BROADCAST_SUDAH_SELESAI`.

**Pemicu otomatis BMKG** bukan endpoint. Worker terjadwal (P5.1) membuat broadcast `sumber
= OTOMATIS_BMKG` untuk unit yang `kabkota`-nya tercakup wilayah ber-MMI ≥ V, lewat algoritme
#13 yang sama. Satu kejadian BMKG hanya memicu sekali (`sumberKejadian`).

---

### 3.4 Verifikasi Alert Bencana

#### 17. `GET /laporan-bencana`
Laporan masuk (2.4.1).
- **Permission** `sigap:laporan:read` · **Scope** SATGAS: `UNIT`. PEGAWAI: `SELF` (hasilnya
  sama dengan #9).
- Query: `status`, `sejak`, `halaman`. Urutan: `MENUNGGU` lebih dulu, lalu terbaru.

#### 18. `POST /laporan-bencana/{id}/verifikasi`
- **Permission** `sigap:laporan:verify` · **Scope** SATGAS: `UNIT`
```json
{ "keputusan": "TOLAK", "alasan": "Getaran berasal dari pekerjaan konstruksi di sebelah kantor" }
```
- `keputusan`: `VALID` | `TOLAK` (bukan notifikasi baca saja, koreksi 3). `alasan` wajib
  bila `TOLAK`, ≤400.
- `VALID` → status `TERVERIFIKASI` + peringatan eskalasi ke Pimpinan Satker unit. `TOLAK` →
  `DITOLAK`, dan alasannya terbaca pelapor. Verifikasi tidak otomatis memicu broadcast: ia
  menjadi dasar bagi Satgas/Pimpinan untuk bertindak (UR).
- 200, isi `Laporan`. Galat: 409 `LAPORAN_SUDAH_DIVERIFIKASI`, 409 `LAPORAN_DIBATALKAN`.

---

### 3.5 Asesmen Kondisi Bencana

#### 3.5.1 Konsep seri dan versi
- Satu **seri** = satu unit + satu jenis bencana untuk satu kejadian. Seri dimulai sejak
  broadcast aktif yang memegang unit itu untuk jenis tersebut dipicu. Tanpa broadcast
  pemegang (misalnya kebakaran satu ruangan yang tidak memicu safety check), seri dimulai 24
  jam ke belakang.
- Kiriman pertama dalam seri adalah **versi 1** (tombol "Kirim"). Kiriman berikutnya
  menambah versi, **tidak menimpa** (tombol "Update Asesmen", koreksi 9). Keadaan lapangan
  berubah dari jam ke jam, dan urutan perubahan itu yang dibaca saat pertanggungjawaban.
- **Persetujuan berlaku per seri.** Setelah Pimpinan menyetujui, versi berikutnya adalah
  pembaruan kondisi dan tidak perlu disetujui ulang.

#### 3.5.2 Objek `Asesmen`
```json
{
  "id": "…",
  "unit": RingkasUnit,
  "dikirimOleh": RingkasPengguna,
  "dikirimPada": "…",
  "urutan": 2,
  "kondisiBencana": {
    "kategoriBencana": "ALAM", "jenisBencana": "Gempa Bumi",
    "waktuKejadian": "2026-09-18T02:40:00Z",
    "kondisiFisik": "MINOR",
    "uraian": "Retak pada dinding lantai 2, satu tangga darurat tertutup puing"
  },
  "aspek": {
    "sdm":   { "kelengkapanHadir": "SEBAGIAN_75", "korbanJiwa": "TIDAK_ADA", "kondisiFisik": "LUKA_RINGAN",
               "kondisiPsikis": "TRAUMA_RINGAN", "catatanKondisiPegawai": "…", "catatanTambahan": "…" },
    "aset":  { "konstruksiBangunan": "RUSAK_RINGAN", "aksesLokasi": "DAPAT_DIAKSES",
               "kondisiPeralatan": "NORMAL", "jumlahPeralatan": "LENGKAP",
               "kondisiPerlengkapan": "RUSAK_RINGAN", "jumlahPerlengkapan": "SEBAGIAN",
               "kendaraanLaikOperasi": "NORMAL", "jumlahKendaraan": "LENGKAP", "catatan": "…" },
    "tik":   { "kondisiPerangkat": "NORMAL", "jumlahPerangkat": "LENGKAP", "aksesJaringan": "LAMBAT",
               "kelistrikan": "UPS_GENSET", "aplikasiUtama": "BERFUNGSI_SEBAGIAN", "catatan": "…" },
    "arsip": { "arsipVital": "AMAN", "arsipPenting": "AMAN", "evakuasiFisik": "DAPAT_DILAKUKAN", "catatan": "…" },
    "layanan": [ { "layananId": "…", "nama": "Layanan SP2D", "rtoJam": 24, "status": "TERGANGGU" } ]
  },
  "persetujuan": {
    "status": "DISETUJUI",
    "disetujuiOleh": RingkasPengguna, "disetujuiPada": "…",
    "tanggapDarurat": { "id": "…", "status": "DARURAT" }
  },
  "lampiran": [ Lampiran ]
}
```
- `persetujuan.status`: `MENUNGGU_PIMPINAN` | `DISETUJUI`. Status ini tampil di dashboard
  pemantau **segera setelah dikirim**, tanpa menunggu Pimpinan (UR).
- `lampiran` memuat lampiran seluruh versi dalam seri.
- Aspek SDM juga menampilkan rekap safety check langsung dari #4/#5 (koreksi 5). Rekap tidak
  disalin ke asesmen.

#### 3.5.3 Pemetaan field ↔ kolom ↔ nilai tersimpan
Satu asesmen tersimpan di **dua tabel**: `"DamageAssessment"` (kondisi bencana, catatan
pegawai, layanan) dan `"ChecklistKondisiLapangan"` (empat aspek). Keduanya ditulis dalam
satu transaksi. `Asesmen.id` = `"DamageAssessment"."id"`.

| Field API | Kolom | Kode ↔ nilai tersimpan |
|---|---|---|
| `kondisiBencana.jenisBencana` | `DamageAssessment.jenisBencana` | nama taksonomi apa adanya |
| `kondisiBencana.kategoriBencana` | `DamageAssessment.kategoriBencana` | `ALAM`/`NONALAM`/`SOSIAL` (diisi sistem) |
| `kondisiBencana.waktuKejadian` | `DamageAssessment.waktuKejadian` | — |
| `kondisiBencana.kondisiFisik` | `DamageAssessment.kondisiFisik` | `AMAN` Aman · `MINOR` Minor · `BERAT` Berat · `KRITIS` Kritis |
| `kondisiBencana.uraian` | `DamageAssessment.deskripsi` | — |
| `sdm.catatanKondisiPegawai` | `DamageAssessment.catatanPegawai` | — (koreksi 5) |
| `sdm.kelengkapanHadir` | `Checklist.sdmJumlah` | `PENUH_100` "100% Lengkap" · `SEBAGIAN_75` "75%" · `SEBAGIAN_50` "50%" · `SEBAGIAN_25` "25%" |
| `sdm.korbanJiwa` | `Checklist.sdmKorban` | `TIDAK_ADA` "Tidak Ada" · `ADA` "Ada" |
| `sdm.kondisiFisik` | `Checklist.sdmFisik` | `AMAN` "Aman" · `LUKA_RINGAN` "Ada Luka Ringan" · `LUKA_BERAT` "Ada Luka Berat" |
| `sdm.kondisiPsikis` | `Checklist.sdmPsikis` | `AMAN` · `TRAUMA_RINGAN` · `TRAUMA_SEDANG` · `TRAUMA_BERAT` ("Trauma …") |
| `sdm.catatanTambahan` | `Checklist.sdmCatatan` | — (koreksi 5) |
| `aset.konstruksiBangunan` | `Checklist.asetGedungKonstruksi` | `KOKOH` · `RUSAK_RINGAN` · `RUSAK_SEDANG` · `RUSAK_BERAT` |
| `aset.aksesLokasi` | `Checklist.asetGedungAkses` | `DAPAT_DIAKSES` · `TIDAK_DAPAT_DIAKSES` |
| `aset.kondisiPeralatan` | `Checklist.asetPeralatanKondisi` | `NORMAL` · `RUSAK_RINGAN` · `RUSAK_SEDANG` · `RUSAK_BERAT` |
| `aset.jumlahPeralatan` | `Checklist.asetPeralatanJumlah` | `LENGKAP` "Lengkap" · `SEBAGIAN` "Ada Sebagian" · `TIDAK_ADA` "Tidak Ada Sama Sekali" |
| `aset.kondisiPerlengkapan` | `Checklist.asetPerlengkapanKondisi` | seperti `kondisiPeralatan` |
| `aset.jumlahPerlengkapan` | `Checklist.asetPerlengkapanJumlah` | seperti `jumlahPeralatan` |
| `aset.kendaraanLaikOperasi` | `Checklist.asetKendaraanLaik` | `NORMAL` · `TIDAK_LAIK_RINGAN` · `TIDAK_LAIK_SEDANG` · `TIDAK_LAIK_BERAT` ("Tidak Laik - …") |
| `aset.jumlahKendaraan` | `Checklist.asetKendaraanJumlah` | seperti `jumlahPeralatan` |
| `aset.catatan` | `Checklist.asetCatatan` | — (sembilan field aset: koreksi 6) |
| `tik.kondisiPerangkat` | `Checklist.tikKomputerKondisi` | seperti `kondisiPeralatan` |
| `tik.jumlahPerangkat` | `Checklist.tikKomputerJumlah` | seperti `jumlahPeralatan` |
| `tik.aksesJaringan` | `Checklist.tikJaringanAkses` | `NORMAL` · `LAMBAT` · `TERPUTUS_TOTAL` |
| `tik.kelistrikan` | `Checklist.tikJaringanPower` | `PLN_NORMAL` "Tersedia (PLN Normal)" · `UPS_GENSET` "Tersedia via UPS/Genset" · `TIDAK_TERSEDIA` |
| `tik.aplikasiUtama` | `Checklist.tikAplikasiUtama` | `BERFUNGSI_NORMAL` · `BERFUNGSI_SEBAGIAN` · `TIDAK_BERFUNGSI` |
| `tik.catatan` | `Checklist.tikCatatan` | — |
| `arsip.arsipVital`, `arsip.arsipPenting` | `Checklist.arsipVital`, `.arsipPenting` | `AMAN` · `RUSAK_RINGAN` · `RUSAK_SEDANG` · `RUSAK_BERAT` |
| `arsip.evakuasiFisik` | `Checklist.arsipEvakuasi` | `DAPAT_DILAKUKAN` · `TIDAK_DAPAT_DILAKUKAN` |
| `arsip.catatan` | `Checklist.arsipCatatan` | — |
| `layanan[]` | `DamageAssessment.layananTerdampak` (JSON) | `NORMAL` · `TERGANGGU` · `BERHENTI_TOTAL` (koreksi 7) |

Daftar opsi yang sama disajikan `GET /referensi/opsi-asesmen`, satu sumber untuk formulir
Satgas dan layar Pimpinan, supaya label tidak melenceng seperti yang pernah dikeluhkan tim
probis.

#### 19. `GET /layanan-kritis`
Daftar layanan kritis unit untuk aspek Layanan.
- **Permission** `sigap:layanan-kritis:read` · **Scope** SATGAS, PIMPINAN: `UNIT`
```json
{ "data": [ { "id": "…", "nama": "Layanan SP2D", "rtoJam": 24, "rtoLabel": "1 Hari", "sumber": "MANUAL" } ] }
```
Hanya baris `kritis = true`. `sumber`: `ADB` bila `adbPada` terisi, selain itu `MANUAL`.

#### 20. `POST /layanan-kritis` — koreksi 7
Fase 1 tidak membangun kuesioner ADB, jadi Tim Satgas mendaftarkan sendiri layanan kritis
unitnya.
- **Permission** `sigap:layanan-kritis:create` · **Scope** SATGAS: `UNIT` (`unitId` dari identitas)
```json
{ "nama": "Layanan SP2D", "rtoJam": 24 }
```
`rtoJam` ∈ {1, 24, 48, 96, 168, 192} (enam periode baku ADB). `nama` ≤120, unik per unit.
Tersimpan dengan `kritis = true`. 201. Galat: 409 `LAYANAN_SUDAH_ADA`.

#### 21. `POST /asesmen`
Kiriman pertama dalam seri, atau kiriman lengkap baru.
- **Permission** `sigap:asesmen:create` · **Scope** SATGAS: selalu atas nama unit sendiri
- Body: bentuk `Asesmen` (bagian 3.5.2) tanpa `id`, `unit`, `dikirim*`, `urutan`, `persetujuan`,
  `lampiran`. Untuk `layanan` cukup `layananId` + `status`.
- **Seluruh field berskala wajib diisi.** Field catatan opsional. `waktuKejadian` opsional
  dan tidak boleh lebih dari 1 menit di masa depan.
- **`layanan` wajib memuat setiap layanan kritis unit**, termasuk yang `NORMAL`, supaya
  "sudah diperiksa dan normal" tidak rancu dengan "belum diperiksa". Kekurangan → 400
  `LAYANAN_BELUM_DINILAI` beserta daftarnya. Unit tanpa layanan kritis mengirim `[]`.
- Efek: dua tabel dalam satu transaksi. Tiap layanan `TERGANGGU`/`BERHENTI_TOTAL` yang belum
  punya gangguan berjalan membuat `"GangguanLayanan"`, sehingga hitung mundur RTO dimulai.
  Peringatan dikirim ke Pimpinan unit, dan asesmen muncul di `/monitor/asesmen-masuk`.
- 201, isi `Asesmen`. Galat: 409 `ASESMEN_KEMBAR`.

#### 22. `POST /asesmen/{id}/revisi` — "Update Asesmen" / ubah per aspek
- **Permission** `sigap:asesmen:update` · **Scope** SATGAS: `UNIT`
- Body seperti #21, tetapi **setiap bagian opsional**: kirim hanya aspek yang berubah. Bagian
  yang tidak dikirim disalin dari versi `{id}`. Ini melayani "Ubah" per aspek pada matriks
  2.5.2.2–2.5.6.
- `{id}` harus versi terkini seri → selain itu 409 `BUKAN_VERSI_TERKINI` (mencegah pembaruan
  yang saling menimpa). `jenisBencana` tidak dapat diubah → 400
  `JENIS_BENCANA_TIDAK_DAPAT_DIUBAH`. Bencana lain berarti seri baru lewat #21.
- 201, isi `Asesmen` versi baru (`urutan` + 1).

#### 23. `POST /asesmen/{id}/lampiran`
Foto kerusakan. `multipart/form-data`, field `berkas`.
- **Permission** `sigap:lampiran:upload` · **Scope** SATGAS: `UNIT`
- `image/jpeg`, `image/png`, maks 10 MB. **[asumsi]** Galat: 413, 415, 503 `LAMPIRAN_GAGAL_DISIMPAN`.

#### 24. `GET /asesmen`
- **Permission** `sigap:asesmen:read` · **Scope** SATGAS, PIMPINAN: `UNIT` · PERWAKILAN:
  `WILAYAH` · SUBKOORDINATOR: `ESELON_I` · KOORDINATOR, SEKJEN: `NASIONAL`
- **Sieve**: lihat #26
- Query: `unitId`, `jenisBencana`, `statusPersetujuan`, `sejak`, `hanyaTerkini` (bawaan
  `true`, satu baris per seri), `halaman`
- Isi `data`: `{ id, unit, jenisBencana, urutan, dikirimPada, dikirimOleh, statusPersetujuan }`

#### 25. `GET /asesmen/terkini`
Asesmen terkini unit untuk seri berjalan. Formulir Satgas memakainya sebagai isian awal dan
penentu label tombol, dan layar persetujuan Pimpinan membacanya.
- **Permission** `sigap:asesmen:read` · **Scope** seperti #24
- Query: `unitId` (bawaan unit pemanggil), `jenisBencana` (bawaan: jenis broadcast aktif
  yang memegang unit)
```json
{ "asesmen": Asesmen, "urutanBerikutnya": 3 }
```
`asesmen: null` dan `urutanBerikutnya: 1` bila belum ada. Tampilan memakainya untuk memilih
label "Kirim" atau "Update Asesmen".

#### 26. `GET /asesmen/{id}`
- **Permission** `sigap:asesmen:read` · **Scope** seperti #24. Pemantau memperoleh "Lihat
  Detail" lewat 2.6.2 (PERMISSION_MAP bagian 5.1, penafsiran 2).
- **Sieve**: `aspek.sdm.catatanKondisiPegawai` dan `aspek.sdm.catatanTambahan` hanya
  SATGAS dan PIMPINAN. Keduanya dapat memuat nama dan keadaan medis pegawai. Pemantau
  memperoleh angka, bukan nama.

#### 27. `GET /asesmen/{id}/versi`
Seluruh versi dalam seri: `{ id, urutan, dikirimPada, dikirimOleh }`. Isinya diambil per
versi lewat #26.
- **Permission** `sigap:asesmen:read` · **Scope** seperti #24

#### 28. `POST /asesmen/{id}/persetujuan` — koreksi 8
Pimpinan Satker menyetujui asesmen, dan tanggap darurat unit aktif.
- **Permission** `sigap:asesmen:approve` · **Scope** PIMPINAN: `UNIT`
- **Body kosong.** Tidak ada isian jenis bencana maupun lokasi. Keduanya diambil dari asesmen,
  sebab mengetik ulang membuka peluang deklarasi menyebut bencana lain dari yang diasesmen.
  Status "pending" adalah keadaan bawaan sebelum disetujui, bukan tindakan tersendiri.
- Syarat: `{id}` versi terkini seri (409 `BUKAN_VERSI_TERKINI`), seri belum disetujui (409
  `SERI_SUDAH_DISETUJUI`), unit belum berstatus darurat (409 `UNIT_SUDAH_DARURAT`), asesmen
  tidak dibatalkan (409 `ASESMEN_DIBATALKAN`).
- Efek: `"DisasterDeclaration"` baru (`status = DARURAT`, `jenisBencana`/`kategori` dari
  asesmen, `lokasi` = nama unit). Notifikasi ke Kepala Perwakilan (provinsi unit),
  Subkoordinator (Eselon I unit), Koordinator MKB, dan Sekretaris Jenderal.
- 200, isi `Asesmen` dengan `persetujuan` terisi.

#### 29. `POST /tanggap-darurat/{id}/selesai` — di luar matriks
Tanpa ini unit berstatus darurat selamanya, dan persetujuan kejadian berikutnya tertolak
`UNIT_SUDAH_DARURAT`. **Perlu konfirmasi pemilik proses bisnis.**
- **Permission** `sigap:tanggap-darurat:close` · **Scope** PIMPINAN: `UNIT`
- Efek: `DARURAT` → `PULIH`, `resolvedAt`. Galat: 409 `TANGGAP_DARURAT_SUDAH_SELESAI`.

---

### 3.6 Dashboard Monitor SC & Sumber Daya

Peran: Kepala Perwakilan, Subkoordinator, Koordinator MKB, Sekretaris Jenderal. **Read only
mutlak**, tidak satu pun endpoint tulis. Pimpinan Satker tidak termasuk (matriks 2.6: "-").
Keadaan unitnya sudah termuat di layar persetujuannya.

- **Permission** `sigap:monitor:read` untuk seluruh endpoint di bagian ini.
- **Scope** PERWAKILAN: `WILAYAH` · SUBKOORDINATOR: `ESELON_I` · KOORDINATOR, SEKJEN:
  `NASIONAL`.
- **Penyaring bersama**: `provinsi`, `kabupatenKota`, `eselonI`, `unitId`. Penyaring hanya
  **mempersempit** di dalam lingkup (AND dengan Scope). Penyaring di luar lingkup menghasilkan
  kosong, bukan galat.
- `jenisBencana`: bawaan = jenis broadcast aktif terbaru di lingkup. Respons memuat
  `jenisAktif` untuk pilihan tampilan. Angka safety check tidak dijumlah lintas jenis, sebab
  pegawai yang sama bisa ditanya oleh dua jenis bencana.
- `sejak`: bawaan = waktu dipicunya broadcast aktif tertua di lingkup, atau 24 jam ke
  belakang bila tidak ada.
- **Sieve**: tidak ada nama maupun koordinat pegawai di bagian ini. Pemantau memperoleh angka
  agregat (PERMISSION_MAP bagian 6; PLAYBOOK P6.3).

#### 30. `GET /monitor/ringkasan`
```json
{
  "lingkup": { "jenis": "WILAYAH", "label": "Wilayah Riau", "penyaringAktif": { "eselonI": "djp" } },
  "jenisBencana": "Gempa Bumi", "jenisAktif": ["Gempa Bumi", "Banjir"],
  "safetyCheck": { "totalPegawai": 412, "aman": 350, "butuhBantuan": 7, "belumMerespons": 55,
                   "tingkatRespons": 0.867, "jumlahUnitDisasar": 9, "jumlahUnitBelumDisasar": 3 },
  "asesmen": { "unitMelapor": 6, "menungguPimpinan": 2, "disetujui": 4 },
  "tanggapDarurat": { "unitDarurat": 4 },
  "layanan": { "normal": 31, "terganggu": 5, "berhentiTotal": 1 }
}
```
`jumlahUnitBelumDisasar` = unit di lingkup yang belum dipegang broadcast mana pun untuk jenis
itu. Angka ini membantu memutuskan perlu tidaknya memicu.

#### 31. `GET /monitor/safety-check`
Tabel agregat (2.1.3 untuk pemantau).
- Query tambahan: `kelompok` = `unit` | `provinsi` | `eselon-1` | `provinsi-eselon-1`
```json
{ "data": [ { "kelompok": { "kode": "…", "label": "KPP Madya Pekanbaru" },
              "totalPegawai": 58, "aman": 49, "butuhBantuan": 3, "belumMerespons": 6, "tingkatRespons": 0.897 } ], … }
```

#### 32. `GET /monitor/asesmen-masuk` — 2.6.1
Terbaru lebih dulu. Status deklarasi tampil meski Pimpinan belum menyetujui.
```json
{ "data": [ { "asesmenId": "…", "unit": RingkasUnit, "jenisBencana": "Gempa Bumi", "urutan": 2,
              "dikirimPada": "…", "statusPersetujuan": "MENUNGGU_PIMPINAN", "tanggapDarurat": null } ], … }
```

#### 33. `GET /monitor/aspek` — 2.6.2
Agregat atas **versi terkini tiap seri** di lingkup, bukan seluruh versi. Satu unit yang
memperbarui lima kali tidak terhitung lima kali.
```json
{
  "jumlahUnitMelapor": 12,
  "sdm":   { "kelengkapanHadir": { "PENUH_100": 7, "SEBAGIAN_75": 3, "SEBAGIAN_50": 2, "SEBAGIAN_25": 0 },
             "unitAdaKorbanJiwa": 1, "unitAdaLukaBerat": 2, "unitAdaTraumaBerat": 0 },
  "aset":  { "konstruksiBangunan": { "KOKOH": 8, "RUSAK_RINGAN": 3, "RUSAK_SEDANG": 1, "RUSAK_BERAT": 0 },
             "aksesLokasi": { … }, "kendaraanLaikOperasi": { … } },
  "tik":   { "aksesJaringan": { … }, "kelistrikan": { … }, "aplikasiUtama": { … } },
  "arsip": { "arsipVital": { … }, "evakuasiFisik": { … } },
  "layanan": { "normal": 31, "terganggu": 5, "berhentiTotal": 1 }
}
```

#### 34. `GET /monitor/layanan`
Rincian "Lihat Detail" aspek Layanan: gangguan yang masih berjalan dari `"GangguanLayanan"`
(`pulihPada` kosong, tidak dibatalkan).
- Query tambahan: `status` = `TERGANGGU` | `BERHENTI_TOTAL`
```json
{ "data": [ { "layanan": { "id": "…", "nama": "Layanan SP2D", "rtoJam": 24 }, "unit": RingkasUnit,
              "status": "TERGANGGU", "sejak": "…", "sisaRtoJam": 6.5 } ], … }
```

#### 35. `GET /monitor/unit/{unitId}`
"Lihat Detail" satu unit. Unit di luar lingkup → 404.
```json
{
  "unit": RingkasUnit,
  "tanggapDarurat": { "id": "…", "status": "DARURAT", "jenisBencana": "Gempa Bumi", "sejak": "…" },
  "safetyCheck": [ { "broadcast": RingkasBroadcast, "totalPegawai": 58, "aman": 49, "butuhBantuan": 3, "belumMerespons": 6 } ],
  "asesmenTerkini": Asesmen,
  "layananTerganggu": [ … ]
}
```
`asesmenTerkini` tunduk pada Sieve #26.

---

### 3.7 Pendukung

#### 36. `GET /me/konteks`
Identitas, lingkup, dan daftar permission untuk menyusun menu dan `*hasPermission` di
tampilan. **[asumsi — mungkin sudah disediakan `iam.plugin`]**
```json
{ "pengguna": RingkasPengguna, "unit": RingkasUnit, "peran": ["PEGAWAI", "SATGAS"],
  "permission": ["sigap:safety-check:read", "…"],
  "lingkup": { "jenis": "UNIT", "label": "KPP Madya Pekanbaru", "catatan": null } }
```
`catatan` terisi bila lingkup menyempit karena data organisasi belum lengkap (fail-closed).

#### 37. `GET /referensi/jenis-bencana`
Taksonomi UU 24/2007. Tanpa Scope.
```json
{ "data": [ { "kategori": "ALAM", "label": "Bencana Alam", "jenis": ["Gempa Bumi", "Tsunami", "…"] },
            { "kategori": "NONALAM", "label": "Bencana Non-alam", "jenis": ["Kebakaran Gedung", "…"] },
            { "kategori": "SOSIAL", "label": "Bencana Sosial", "jenis": ["Kerusuhan Massa", "…"] } ] }
```

#### 38. `GET /referensi/opsi-asesmen`
Opsi setiap field berskala (bagian 3.5.3) plus level keparahan laporan. Tanpa Scope.
```json
{ "sdm.kelengkapanHadir": [ { "kode": "PENUH_100", "label": "100% Lengkap" }, … ], "…": [ … ] }
```

#### 39–42. `GET /referensi/provinsi` · `/kabupaten-kota?provinsi=` · `/eselon-1` · `/unit`
Pilihan untuk penyempit trigger dan penyaring dashboard. **Scope**: hanya nilai di dalam
lingkup baca pemanggil (profil per peran di PERMISSION_MAP bagian 3). `/unit` menerima `provinsi`,
`kabupatenKota`, `eselonI`, `cari`, `halaman`.

#### 43. `GET /notifikasi`
Peringatan yang **dihitung saat diminta** untuk pemanggil. Contohnya: safety check belum
dijawab, laporan masuk (Satgas), asesmen menunggu persetujuan (Pimpinan), asesmen masuk
(pemantau), gempa terkini. Tidak ada status "sudah dibaca", karena skema tidak punya
tabelnya. Sekaligus menjadi saluran *in-app polling* cadangan (P3.6).
`GEMPA_KUAT_TANPA_KANTOR` (`PERINGATAN`, **[asumsi]**, P5.1): hanya bagi pemegang `sigap:monitor:read` berlingkup nasional
(Koordinator MKB, Sekretaris Jenderal). BMKG mencatat guncangan ≥ ambang MMI dalam jendela waktu di wilayah yang tidak punya unit
Kemenkeu, jadi tidak ada broadcast otomatis; peringatan menyarankan memicu safety check manual. Kosong bila pemicu otomatis mati.
```json
{ "data": [ { "kode": "SC_BELUM_DIJAWAB", "tingkat": "GENTING", "judul": "…", "pesan": "…",
              "terkait": { "jenis": "BROADCAST", "id": "…" } } ] }
```
`terkait` menunjuk sumber daya, bukan rute tampilan. Pemetaan ke halaman urusan Angular.

#### 44–45. `POST` / `DELETE /notifikasi/langganan`
Langganan Web Push perangkat (`"LanggananPush"`). POST `{ "endpoint", "keys": { "p256dh", "auth" }, "peramban" }` → 201.
DELETE `{ "endpoint" }` → 204. Dapat dinonaktifkan lewat konfigurasi selama izin Web Push di
domain platform belum dijawab BaTII (Lampiran E #13).

#### 46–47. `GET /health/live`, `GET /health/ready`
`live`: proses hidup. `ready`: database dapat dijangkau. 200/503, tanpa autentikasi.

---

## 4. Siklus status

```
Broadcast          AKTIF ──(selesai)──▶ SELESAI
  sasaran/unit     DISASAR(aktif) ──(broadcast selesai)──▶ DISASAR(tidak aktif)
                   DILEWATI (tetap, menunjuk pemegangnya)

Jawaban SC         BELUM ──▶ AMAN ⇄ BUTUH_BANTUAN   (per pegawai per broadcast; terakhir berlaku)

Laporan            MENUNGGU ──VALID──▶ TERVERIFIKASI  (+ eskalasi ke Pimpinan)
                            └─TOLAK──▶ DITOLAK        (alasan terbaca pelapor)

Asesmen (seri)     v1 "Kirim" ──▶ v2 "Update Asesmen" ──▶ …
  persetujuan      MENUNGGU_PIMPINAN ──(Pimpinan)──▶ DISETUJUI
Tanggap darurat    (dibuat saat DISETUJUI) DARURAT ──(selesai)──▶ PULIH
```

---

## 5. Perubahan skema yang disetujui: tabel ke-33

Disetujui 18 September 2026 sebagai **pengecualian atas aturan "struktur 32 tabel tidak
berubah"**. Aturan kepemilikan unit (bagian 3.3.1) butuh daftar unit per trigger yang dikunci,
sedangkan `"ActiveBroadcast"` hanya menyimpan kriteria. Tanpa tabel ini, sasaran harus
dihitung ulang dari kriteria, dan hasilnya bergeser setiap kali data provinsi/kabupaten unit
dilengkapi. Itu akan mengubah jejak audit "siapa ditanya oleh trigger mana" setelah
kejadian. **Migrasinya perlu dikoordinasikan dengan BaTII** (Lampiran E #14: siapa
menjalankan migrasi skema).

```sql
CREATE TYPE "StatusSasaran" AS ENUM ('DISASAR', 'DILEWATI');

CREATE TABLE "BroadcastSasaranUnit" (
  "id"                        TEXT         PRIMARY KEY,
  "broadcastId"               TEXT         NOT NULL REFERENCES "ActiveBroadcast"("id"),
  "unitId"                    TEXT         NOT NULL REFERENCES "Unit"("id"),
  "jenisBencana"              TEXT         NOT NULL,  -- salinan dari broadcast, untuk indeks unik
  "status"                    "StatusSasaran" NOT NULL,
  "dilewatiKarenaBroadcastId" TEXT         REFERENCES "ActiveBroadcast"("id"),
  "aktif"                     BOOLEAN      NOT NULL DEFAULT TRUE,  -- false saat broadcast selesai
  "isDemo"                    BOOLEAN      NOT NULL DEFAULT FALSE, -- konsisten dengan 32 tabel lain
  "createdAt"                 TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "sasaran_unik_per_broadcast" UNIQUE ("broadcastId", "unitId"),
  CONSTRAINT "sasaran_dilewati_wajib_pemegang"
    CHECK (("status" = 'DILEWATI') = ("dilewatiKarenaBroadcastId" IS NOT NULL))
);

-- Menegakkan "satu pemegang per (unit, jenis bencana)" di tingkat database,
-- sehingga dua trigger yang ditekan bersamaan tidak dapat memegang unit yang sama.
CREATE UNIQUE INDEX "sasaran_satu_pemegang_aktif"
  ON "BroadcastSasaranUnit" ("unitId", "jenisBencana")
  WHERE "status" = 'DISASAR' AND "aktif";

CREATE INDEX "sasaran_unit_aktif" ON "BroadcastSasaranUnit" ("unitId") WHERE "aktif";
```

`jenisBencana` dan `aktif` sengaja disalin dari `"ActiveBroadcast"`. Indeks unik parsial
PostgreSQL tidak dapat merujuk kolom tabel lain.

---

## 6. Selisih yang disengaja terhadap prototipe

PLAYBOOK P4.4 meminta porting "identik termasuk edge case". Butir di bawah ini **sengaja
tidak identik** dan harus diperlakukan sebagai spesifikasi baru saat porting. Keputusan 18 Sep
2026: prototipe **tidak** disamakan sekarang; kedua belas butir ini **wajib diterapkan di
sigap-api dan sigap-web**, dan mengalahkan perilaku prototipe bila keduanya berbeda.

1. **Trigger kembar.** Prototipe (`trigger-actions.ts`) menolak trigger kedua **secara global**
   bila jenis bencananya sama, sehingga Satgas Jakarta memblokir kanwil Riau. Kontrak:
   kepemilikan per unit (bagian 3.3.1).
2. **Pengikatan jawaban.** Prototipe (`submitSafetyCheck`) mengikat jawaban ke broadcast aktif
   **terbaru di mana pun**, bukan yang menyasar unit pegawai. Kontrak: `broadcastId` eksplisit
   di path, divalidasi terhadap sasaran.
3. **Catatan Satgas yang basi.** Prototipe tidak mengosongkan `dicatatOlehId` saat pegawai
   kemudian menjawab sendiri, sehingga jawaban pegawai tercatat "dicatat Satgas". Kontrak:
   dikosongkan.
4. **Kebocoran keberadaan data.** Prototipe menjawab "berada di luar lingkup unit Anda".
   Kontrak: 404.
5. **Scope multi-peran.** Prototipe (`hitungLingkup`) memakai peran terluas pengguna untuk
   semua tindakan. Contohnya, pengguna Satgas + Kepala Perwakilan memverifikasi laporan
   se-provinsi. Kontrak: Scope dari peran yang memberi permission itu (PERMISSION_MAP bagian 2.3).
6. **Administrator.** Prototipe mengizinkan ADMIN melakukan semua tindakan bisnis
   (`SELALU = ['ADMIN']`). ADMIN tidak ada di matriks, jadi kontrak tidak memberinya satu
   pun permission bisnis.
7. **Nilai bawaan diam-diam.** Prototipe mengisi field asesmen kosong dengan nilai "baik"
   (mis. `'100% Lengkap'`). Kontrak: wajib diisi (400), termasuk penilaian setiap layanan
   kritis.
8. **Batas seri asesmen.** Prototipe memakai broadcast aktif terbaru global. Kontrak: broadcast
   yang memegang unit itu untuk jenis bencana tersebut.
9. **Unit berdata kosong.** Prototipe (`sasaran.ts`) sengaja menyasar unit tanpa provinsi.
   Kontrak: hanya yang cocok (keputusan 18 Sep 2026).
10. **Pesan suara.** UR dan desain V15 menyebut lampiran pesan suara, dan skema sudah punya
    `AttachmentType.AUDIO`, tetapi prototipe hanya menerima `image/*` dan `video/*`.
    Kontrak: AUDIO diterima.
11. **Nama pegawai bagi pemantau.** Halaman Monitor prototipe menampilkan rekap per pegawai
    (bernama) kepada pemantau, dan desain V15 punya toggle "Lihat" per kategori. Kontrak:
    angka agregat saja.
12. **Mengakhiri broadcast.** Prototipe mengizinkan peran berlingkup provinsi mengakhiri
    broadcast mana pun, termasuk yang nasional. Kontrak: pemicu, atau lingkup yang mencakup
    seluruh sasarannya.

---

## 7. Keterbatasan skema yang diketahui

Tidak diubah (bukan bagian keputusan tabel ke-33), tetapi harus diketahui implementor:

- **Asesmen = dua baris tanpa FK.** `"DamageAssessment"` dan `"ChecklistKondisiLapangan"`
  hanya dipasangkan oleh konvensi (unit + pengirim + ditulis dalam satu transaksi). Implementasi
  wajib menulis keduanya dalam satu transaksi dan memasangkannya dengan aturan yang sama
  saat membaca. Data prototipe lama yang tidak berpasangan tampil dengan aspek kosong.
- **Persetujuan tanpa FK.** `"DisasterDeclaration"` tidak menyimpan `asesmenId`. Status
  persetujuan seri diturunkan dari deklarasi unit yang sama, jenis bencana yang sama, dan
  `declaredAt` ≥ awal seri, yang tidak dibatalkan.
- **Versi asesmen tanpa penanda.** Tidak ada kolom `urutan`/`seri`. Urutan dihitung dari
  `createdAt` di dalam seri (bagian 3.5.1).
- **Jejak audit tanpa kolom nilai sebelum/sesudah.** Disimpan sebagai JSON di
  `"JejakPerubahan"."ringkasan"`.
- **Notifikasi in-app tanpa status baca.** Lihat #43.

---

## 8. Di luar kontrak ini

- **Butir 1.1 Data Bencana Nasional.** Matriks memberi Koordinator MKB "Tambah, Modify",
  sedangkan prototipe memindahkannya ke Sekretaris Jenderal. Menunggu keputusan (bagian 9).
- **Info Bencana Terkini BMKG/BNPB/MAGMA.** Dikontrak bersama service integrasi (P5.1).
- **Alur request → approve broadcast** pada desain V15 (`"BroadcastRequest"`). Sudah
  digantikan trigger berjenjang langsung (koreksi 12, UAT T12). Tabelnya tetap ada tetapi
  tidak dipakai.
- **Pembatalan/koreksi catatan** (`koreksi-actions.ts`). Tidak ada di matriks 2.1–2.6.
- Seluruh modul **Fase 2**: dokumen MKB, eksekusi RKB, LPKB, simulasi. Peran Tim Pengembang
  dan Tim Implementasi RKB tidak mendapat endpoint.
- **Fungsi Administrator** (kelola pengguna, unit, pembersihan data uji coba).

---

## 9. Pertanyaan terbuka

**Untuk BaTII**
1. Format permission `sigap:resource:action` sesuai konvensi platform? (Lampiran E #5)
2. Cara mendaftarkan Scope dan Sieve ke IAM: UI admin, berkas konfigurasi, atau API?
   (Lampiran E #6). Draf datanya ada di PERMISSION_MAP bagian 7.
3. Awalan path (`/api/v1`), amplop koleksi, dan bentuk galat standar ICS?
4. Bagaimana `iam.plugin` menggabungkan Scope pengguna berperan ganda: gabungan (usulan
   kontrak) atau prioritas?
5. Bagaimana tabel `"User"` diisi dari HRIS/SSO, dan apakah `kode_satker` memetakan ke
   `"Unit"."kode"`?
6. Siapa menjalankan migrasi skema untuk tabel ke-33? (Lampiran E #14)
7. Apakah `iam.plugin` menyediakan audit trail bawaan dan endpoint konteks pengguna
   (pengganti #36)? (Lampiran E #10)

**Untuk pemilik proses bisnis**
8. Mengakhiri broadcast (#16) dan menyatakan pulih tanggap darurat (#29) tidak ada di matriks
   tetapi diperlukan siklus hidupnya. Siapa berwenang?
9. Jenis dan batas lampiran. PLAYBOOK P5.2 menyebut "foto/video/rekaman suara" lalu "jpg, png,
   pdf, docx, xlsx". Usulan kontrak: foto+video+audio untuk laporan, foto untuk asesmen, 10 MB
   per berkas, 5 berkas per laporan.
10. Butir 1.1 Data Bencana Nasional: Koordinator MKB (matriks) atau Sekretaris Jenderal
    (prototipe)?
