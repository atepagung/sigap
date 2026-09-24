# ACCESS_RULES — kontrol akses di prototipe yang TIDAK diporting sebagai kode bisnis

Disusun 21 September 2026 saat porting aturan bisnis (PLAYBOOK P4.4) dari prototipe
`atepagung/sigap-prototipe` commit `1b1487a`, memakai `BUSINESS_RULES_INDEX.md` sebagai peta.

Setiap butir di sini adalah logika prototipe yang memutuskan **siapa boleh melihat atau mengubah
apa**. Sesuai PLAYBOOK Lampiran A dan [AGENTS.md](AGENTS.md) bagian 5, logika seperti ini tidak
ditulis ulang di C# — ia diterjemahkan menjadi **permission** (`[KemenkeuAuthorize]`), **Scope**
(klausa `WHERE`), atau **Sieve** (field di-`null`-kan) yang ditegakkan `iam.plugin`, dengan data
kebijakan di [apps/sigap-api/iam-policy.sigap.json](apps/sigap-api/iam-policy.sigap.json).

Kolom **Status**: ✓ = sudah tertampung di kebijakan/kontrak, tinggal dipakai saat endpoint dibangun
(P4.5) · ⚖ = perlu keputusan atau jawaban pihak lain sebelum endpointnya dibangun.

---

## Ringkasan

| # | Sumber prototipe | Diterjemahkan menjadi | Status |
|---|---|---|---|
| A1 | `trigger.ts` `kewenanganPicu` | `sigap:broadcast:trigger` + `DataScope.Terluas()` | ✓ (22 Sep 2026, lihat Rincian) |
| A2 | `trigger-sasaran.ts` `bolehAkhiriPicu` | `sigap:broadcast:close` + `PEMICU_ATAU_MENCAKUP` | ✓ (semantik berubah, butir 12) |
| A3 | `trigger-actions.ts` pemilihan sasaran per peran | Scope tulis PERMISSION_MAP 2.4 | ✓ |
| A4 | `tanggap-darurat.ts` `bolehKelolaUnit` | `sigap:asesmen:approve`, `sigap:tanggap-darurat:close` + `UNIT` | ✓ (ADMIN dibuang) |
| A5 | `penjawab-safety.ts` `wajibMenjawab`, `SARING_PENJAWAB` | `sigap:safety-check:respond` · penyebut rekap | ✓ (diterapkan sebagai `[ASUMSI]`, lihat Rincian) |
| A6 | `sasaran.ts` `menyasar`, `saringSasaran`, `broadcastUntukSaya`, `perluMelaporDulu` | Scope `SASARAN_SAYA` + `"BroadcastSasaranUnit"` | ✓ (butir 2, 9) |
| A7 | `safety.ts` `rekapRespons(cakupan)`, `kejadianTerkini` | Scope `UNIT` per permission, broadcast pemegang unit | ✓ |
| A8 | `peringatan.ts` `hitungPeringatan` | #43 dihitung per permission & Scope | ⚖ satu kebocoran agregat |
| A9 | `rilis.ts` `peranDibuka`, `PERAN_MATRIKS`, `ruteDibuka` | menu shell + `*hasPermission` dari `/me/konteks` | ✓ (dua rute ⚖) |
| A10 | `wewenang.ts`, `lingkup.ts` | sudah menjadi `iam-policy.sigap.json` (P3.3) | ✓ |
| A11 | `picu-otomatis.ts` `akunSistem` | identitas layanan untuk worker BMKG | ⚖ BaTII |

---

## Rincian

### A1 — Kewenangan memicu per peran (`src/logic/trigger.ts`)
**Prototipe.** `kewenanganPicu(roles)` memetakan peran → lingkup pemicuan (SATGAS `UNIT`,
PERWAKILAN `PROVINSI`, SUBKOORDINATOR `ESELON_I`, KOORDINATOR `NASIONAL`), dan untuk pengguna
berperan ganda memilih yang **tertinggi** menurut urutan Koordinator → Kepala Perwakilan →
Subkoordinator → Satgas. Hasil `null` = tidak berwenang (`trigger-actions.ts` baris 43–46).

**Terjemahan.** `[KemenkeuAuthorize(Izin.BroadcastTrigger)]` di #12/#13; lingkupnya sudah ada di
kebijakan (`"sigap:broadcast:trigger": { SATGAS: UNIT, PERWAKILAN: WILAYAH, SUBKOORDINATOR: ESELON_I,
KOORDINATOR: NASIONAL }`). Judul dan keterangan per peran (`judul`, `keterangan`, `LABEL_LINGKUP`)
adalah teks tampilan dan pindah ke Angular.

**Diputuskan 22 Sep 2026 (P4.5 putaran 4), opsi (a) tanpa perlu data baru.** PERMISSION_MAP bagian
2.3 menetapkan pengecualian: lingkup trigger memakai **prioritas**, bukan gabungan OR, karena satu
broadcast hanya punya satu lingkup. Ternyata prioritas prototipe (Koordinator → Kepala Perwakilan →
Subkoordinator → Satgas) persis sama dengan urutan keluasan profil generik pada permission ini
(NASIONAL → WILAYAH → ESELON_I → UNIT) — kontainmen organisasi yang sudah melekat pada kosakata
`profilLingkup`, bukan pengetahuan tentang peran tertentu. `DataScope.Terluas()` (ekstensi baru di
`Kemenkeu.Iam.Dummy`, `[ASUMSI]`, DUMMY_REGISTRY butir 13) memilih grant generik terluas dari sebuah
`DataScope`; `PembangunSasaran` (Application/Broadcast) memanggilnya dan bercabang atas
`grant.Profile`/`grant.Area`, **tidak pernah** atas nama peran. Kasus berperan ganda kini terlayani
benar, dibuktikan `Terluas_memilih_NASIONAL_atas_lingkup_lain_walau_berperan_ganda` dan
`Terluas_memilih_WILAYAH_atas_UNIT_tanpa_Koordinator` di `Kemenkeu.Iam.Dummy.Tests`.

### A2 — Siapa boleh mengakhiri broadcast (`src/logic/trigger-sasaran.ts`)
**Prototipe.** `bolehAkhiriPicu`: pemicunya sendiri, **atau** peran berlingkup `PROVINSI`/`NASIONAL`
— sehingga Kepala Perwakilan dapat mengakhiri broadcast nasional.

**Terjemahan.** `sigap:broadcast:close` dengan profil `PEMICU_ATAU_MENCAKUP(<lingkup>)` (kebijakan
baris `sigap:broadcast:close`): pemicunya, atau lingkup pengakhir mencakup **seluruh** unit
`DISASAR`. Terlihat tetapi tidak berhak → 403 `TIDAK_BERWENANG_MENGAKHIRI` (#16). Ini selisih
disengaja API_CONTRACT bagian 6 butir 12. Profil ini bertanda `domain` (disusun kode aplikasi),
jadi saat #16 dibangun predikatnya ditulis di kueri Infrastructure, bukan sebagai `if` di use case.

### A3 — Pemilihan sasaran menurut peran (`src/app/trigger-actions.ts`, memakai `trigger-sasaran.ts`)
**Prototipe.** Memilih `sasaranUnit`/cabang provinsi/`sasaranEselonI`/`sasaranNasional` menurut
lingkup peran; memastikan satu unit pilihan Kepala Perwakilan berada di provinsinya (baris 74);
mengambil provinsi dan Eselon I dari unit pemicu, tidak dari formulir.

**Yang diporting ke Domain** hanya perakitan kriteria dan kalimat lokasi
(`Sigap.Domain.Broadcast.SasaranPemicu`) — isinya data, bukan keputusan akses. **Yang tidak:**
pemilihan fungsi menurut peran (A1) dan pemeriksaan provinsi unit pilihan.

**Terjemahan.** Scope tulis PERMISSION_MAP bagian 2.4: kandidat sasaran = unit dalam
`GetScope(Izin.BroadcastTrigger)`, diterapkan di `WHERE`. Unit pilihan di luar lingkup → 404
(bukan pesan "hanya dapat memicu unit di dalam provinsi Anda sendiri" — butir 4). Pemeriksaan
"tidak ada unit di kota X" (baris 97–111) bukan kontrol akses dan menjadi 422 `SASARAN_KOSONG`.

### A4 — Kepemilikan unit untuk tanggap darurat (`src/logic/tanggap-darurat.ts`)
**Prototipe.** `bolehKelolaUnit(target, unitSaya, roles)`: unit sendiri, **atau** `ADMIN` untuk
unit mana pun. Dipakai saat mengaktifkan dan menutup tanggap darurat.

**Terjemahan.** `sigap:asesmen:approve` (#28) dan `sigap:tanggap-darurat:close` (#29), keduanya
PIMPINAN → `UNIT`. Pengecualian ADMIN **tidak** dibawa (butir 6: ADMIN tanpa permission bisnis).

### A5 — Siapa wajib menjawab safety check (`src/logic/penjawab-safety.ts`)
**Prototipe.** `PERAN_PENJAWAB = ['PEGAWAI']`. Dipakai dua arah:
1. `wajibMenjawab(roles)` untuk **pengguna yang sedang masuk** (popup, peringatan "Anda belum
   mengonfirmasi").
2. `SARING_PENJAWAB` untuk **orang lain**: penyebut dan pembilang rekap hanya menghitung pengguna
   aktif pemegang peran Pegawai Umum.

**Terjemahan.** (1) = memegang `sigap:safety-check:respond`, yang hanya diberikan kepada PEGAWAI.
Angular memakai `*hasPermission`, backend `[KemenkeuAuthorize]`; tidak ada pemeriksaan peran.

**Diterapkan 23 Sep 2026 (P4.5 putaran 5), sebagai `[ASUMSI]` — (2).** Ini bukan izin pemanggil,
melainkan definisi kelompok yang dihitung, dan membutuhkan **keanggotaan peran pegawai lain**.
API_CONTRACT 1.2 menetapkan peran dibaca dari klaim `groups` token, **bukan** tabel `"UserRole"`.
Token hanya menjelaskan pemanggil, jadi untuk menghitung "berapa Pegawai Umum aktif di unit ini"
dibutuhkan sumber lain. Mengikuti keputusan pemilik proyek yang sama dengan A1 (usulan diterapkan
sebagai asumsi, dicatat DUMMY_REGISTRY), penyebut #4/#5 dihitung dari tabel `"UserRole"` yang sudah
ada — `SafetyCheckStore` membaca `u.Roles.Any(r => r.Role == RoleKey.Pegawai)`, sama seperti
`PenerimaPemberitahuanDariUserRole`. Penyebut #30/#31/#35 (Monitor) memakai pola yang sama
(`MonitorStore.Pegawai()`), diterapkan 23 Sep 2026. Direktori grup `iam.plugin`/Keycloak masih
pertanyaan terbuka untuk BaTII bila tabel `"UserRole"` dianggap sementara (API_CONTRACT bagian 9
butir 5).

### A6 — Siapa sasaran sebuah broadcast (`src/logic/sasaran.ts`)
**Prototipe.** `menyasar(b, unit)` dan `saringSasaran(b)` menghitung ulang sasaran dari kriteria
`"ActiveBroadcast"` setiap kali ditanya. Unit tanpa provinsi/kabupaten/Eselon I **selalu** dianggap
sasaran. `broadcastUntukSaya` mengambil broadcast terbaru yang menyasar; `perluMelaporDulu`
memutuskan pengalihan ke halaman safety check.

**Terjemahan.** Scope `SASARAN_SAYA` (PERMISSION_MAP 2.2) atas `"BroadcastSasaranUnit"` yang dikunci
saat dipicu. Dua selisih disengaja: unit berdata kosong **tidak** disasar (butir 9), dan jawaban
diikat ke `broadcastId` eksplisit (butir 2). `perluMelaporDulu` menjadi keputusan Angular atas
`GET /safety-check/aktif` (#1, urut dari yang paling lama). Tidak ada kode C# yang meniru `menyasar`
di memori.

### A7 — Cakupan rekap safety check (`src/logic/safety.ts`)
**Prototipe.** `rekapRespons(cakupan, …)` menerima `cakupan = saringUnit(hitungLingkup(...))` —
lingkup peran **terluas** pengguna — dan `kejadianTerkini()` mengambil broadcast aktif terbaru di
seluruh Kemenkeu.

**Terjemahan.** Scope dari permission endpoint: `sigap:safety-check-rekap:read` → `UNIT` (#4, #5),
`sigap:monitor:read` → `WILAYAH`/`ESELON_I`/`NASIONAL` (#30–#35). Broadcast yang direkap = yang
memegang unit itu (#4), bukan terbaru global (butir 5 dan 8). Aturan hitungnya sendiri (jawaban
terakhir per pegawai, pembilang = kelompok penyebut) **bukan** kontrol akses dan dicatat di
`Sigap.Domain.SafetyCheck.RekapSafetyCheck` sebagai syarat kueri.

### A8 — Mesin peringatan (`src/logic/peringatan.ts`)
**Prototipe.** `hitungPeringatan` menyalakan tiap jenis peringatan menurut peran
(`punya('SATGAS') || punya('PERWAKILAN')` …), menyaring dengan lingkup peran terluas, dan memilih
penerima push pelanggaran RTO menurut peran (SATGAS/IMPL_RKB/PIMPINAN di unit; KOORDINATOR/SEKJEN
di mana pun).

**Terjemahan.** #43 menghitung tiap peringatan **hanya bila pemanggil memegang permission sumber
datanya**, dengan Scope permission itu. Contoh: "laporan menunggu verifikasi" ↔
`sigap:laporan:verify` (SATGAS saja — prototipe juga menampilkannya ke Kepala Perwakilan, yang tidak
memegang `laporan:read`); "Anda belum mengonfirmasi" ↔ `sigap:safety-check:respond`. Penerima push
RTO ditentukan dari pemegang permission pada lingkup unit, bukan daftar peran; IMPL_RKB dibuang
(Fase 2), begitu pula peringatan dokumen MKB dan LPKB.

**⚖ Temuan, diselesaikan 23 Sep 2026.** Peringatan `picu-belum` (`PICU_BELUM` di kode, #43)
prototipenya menghitung laporan `TERVERIFIKASI` **seluruh Kemenkeu tanpa Scope**, lalu
menampilkannya kepada Kepala Perwakilan dan Subkoordinator — membocorkan jumlah laporan di luar
lingkup mereka. Diterjemahkan memakai Scope `sigap:broadcast:trigger` pemanggil (bukan
`sigap:monitor:read` seperti usul semula — permission itulah yang menentukan siapa "berwenang
memicu", termasuk SATGAS yang tidak memegang `monitor:read`). Masih menunggu konfirmasi pemilik
proses bisnis bahwa angka ber-Scope memang yang dimaksud, bukan angka nasional.

### A9 — Gerbang rilis per rute dan peran (`src/logic/rilis.ts`)
**Prototipe.** `MODE_MATRIKS`, `RUTE_MATRIKS`, `PERAN_MATRIKS`, `peranDibuka`,
`PERAN_DISEMBUNYIKAN`: menu dan halaman dibuka per peran; ADMIN membuka semua rute.

**Terjemahan.** Tidak ada padanan di C#. Menu shell dan `*hasPermission` di Angular disusun dari
daftar permission `GET /me/konteks` (#36); dua peran Fase 2 tidak memegang permission apa pun, jadi
menunya kosong dengan sendirinya — saklar `MODE_MATRIKS` tidak diperlukan. ADMIN tidak membuka rute
bisnis (butir 6).

**⚖ Dua rute yang berbeda dari kontrak:** `/data-bencana` hanya SEKJEN di prototipe, sedangkan
matriks memberi Koordinator MKB "Tambah, Modify" (API_CONTRACT bagian 9 butir 10); dan
`/katalog-kantor` (SEKJEN) yang tidak ada di kontrak maupun matriks.

### A10 — Peta wewenang dan lingkup (`src/logic/wewenang.ts`, `src/logic/lingkup.ts`)
Sudah menjadi data di `iam-policy.sigap.json` sejak P3.3 dan diuji `IzinTests`. Tidak diporting —
memportingnya berarti menulis ulang logika keamanan ([AGENTS.md](AGENTS.md) bagian 4).

### A11 — Akun sistem pengirim broadcast otomatis (`src/logic/picu-otomatis.ts`)
**Prototipe.** `akunSistem()` membuat baris `"User"` palsu (`sistem@sigap.internal`, NIP
`0000000000000000`, `aktif = false`, tanpa peran) supaya `"ActiveBroadcast"."dikirimOlehId"` (wajib)
terisi saat BMKG memicu.

**⚖ Terjemahan belum dapat ditentukan.** Pemicu otomatis adalah aktor mesin (worker P5.1), jadi
identitasnya urusan platform: akun layanan / *client credentials* SSO. Menulis baris `"User"` palsu
bertentangan dengan API_CONTRACT 1.2 (`"User"` = profil rujukan dari HRIS/SSO). Pertanyaan untuk
BaTII: apakah platform menyediakan identitas layanan, dan bagaimana ia dipetakan ke kolom
`dikirimOlehId` (FK `NOT NULL` ke `"User"`) tanpa mengubah skema.

**Diputuskan pemilik proyek 24 Sep 2026 (P5.1), `[ASUMSI]`: akun layanan sebagai baris `"User"`.** Pilihan yang
sama dengan prototipe, dengan pengaman yang tidak ada di prototipe: akun ber-NIP `SISTEM-BMKG` (dari
`Bmkg:NipLayanan`), **aktif** (resolver identitas hanya menemukan pengguna aktif), **tanpa satu pun
`"UserRole"`**, `isDemo = false`, di unit akar `kemenkeu`. Karena tanpa peran, ia tidak pernah muncul sebagai
pegawai: penyebut rekap dan penerima pemberitahuan membaca `"UserRole"`, jadi pengecualiannya tidak bergantung
pada ingatan siapa pun (tes `Akun_layanan_tidak_dihitung_sebagai_pegawai_penyebut_rekap`, mutasi tertangkap).
Ia masuk ke `ICurrentUserContext` lewat `IServiceIdentity.AssumeAsync(nip)` (dummy `Kemenkeu.Iam`, DUMMY_REGISTRY
butir 102), sehingga jejak audit dan `dikirimOlehId` terisi dari jalur yang sama dengan permintaan HTTP. Lingkup
datanya selalu kosong. Peran dan profil yang dititipkan di jejak audit pemicu: `SISTEM|BMKG|<unit akar>`.
Pertanyaan ke BaTII di atas **tetap terbuka**; bila platform menyediakan identitas layanan, cukup
`IServiceIdentity` yang diganti dan baris `"User"`-nya dihapus.

---

## Status penerapan di endpoint (diperbarui 23 Sep 2026, P4.5 putaran 5)

Keputusan pemilik proyek 21 Sep 2026: butir ⚖ diterapkan mengikuti usulan, bertanda `[ASUMSI]`, dan dicatat di
[DUMMY_REGISTRY.md](DUMMY_REGISTRY.md) bagian 9 supaya mudah dicabut.

| Butir | Status | Keterangan |
| --- | --- | --- |
| A5 | **Diterapkan (masih via `"UserRole"`, sama seperti sebelumnya).** | Penerima pemberitahuan: domain Laporan, Asesmen, Broadcast. Penyebut rekap (#4/#5, dan kini #30/#31/#35): Pegawai Umum aktif di unit, dibaca `"UserRole"` di `SafetyCheckStore`/`MonitorStore` |
| A1 | **Diterapkan, tanpa data prioritas baru** | `DataScope.Terluas()` (ekstensi generik `Kemenkeu.Iam.Dummy`, DUMMY_REGISTRY butir 13) memilih grant terluas dari `sigap:broadcast:trigger`; urutannya kebetulan sama dengan prioritas prototipe. `PembangunSasaran` membaca `grant.Profile`/`grant.Area`, tidak pernah nama peran. Terbukti tes "berperan ganda tetap dipilih terluas" di `Kemenkeu.Iam.Dummy.Tests` |
| A2 | **Diterapkan** | `sigap:broadcast:close` profil `PEMICU_ATAU_MENCAKUP`: otorisasi #16 dibaca langsung dari `DataScope.Grants` (`Area.IsNational` atau seluruh `unitDisasarAktif` ⊆ `Area.UnitIds`), bukan nama peran. Terbukti tes Perwakilan yang mencakup vs tidak mencakup seluruh sasaran |
| A3 | **Diterapkan** | Kandidat sasaran #12/#13 = `GetScope(Izin.BroadcastTrigger).Terluas().Area`, diterapkan di `WHERE` (`KandidatAsync`). Unit pilihan Kepala Perwakilan di luar provinsinya → 404, bukan pesan khusus |
| A4 | **Diterapkan** | `bolehKelolaUnit` menjadi `sigap:asesmen:approve` (#28) dan `sigap:tanggap-darurat:close` (#29), keduanya Scope `UNIT`; ADMIN tidak lagi ikut. Terbukti oleh tes peran dan tes "Pimpinan unit lain -> 404, tanpa UPDATE terkirim" |
| A6 | **Diterapkan** | Sasaran dikunci saat dipicu di `"BroadcastSasaranUnit"` (bukan dihitung ulang tiap dibaca seperti prototipe); unit berdata kosong tidak pernah cocok kriteria (fail-closed, berbeda dari `menyasar` prototipe) |
| A7 | **Diterapkan (#4/#5, #30/#31).** | Scope `sigap:safety-check-rekap:read` = UNIT (unit pemanggil sendiri, tanpa parameter unit lain); `sigap:monitor:read` = WILAYAH/ESELON_I/NASIONAL, penyebut sama (Pegawai Umum aktif) atas seluruh unit di lingkup itu, dijumlah per unit yang dipegang broadcast aktif dari jenis yang diminta — bukan `kejadianTerkini` global |
| S5 | **Diterapkan** | Pasangan dua separuh asesmen `(unitId, submittedById, createdAt)` di `AsesmenStore`; kedua separuh ditulis dalam satu `SaveChanges` dengan `createdAt` sama persis. Uji mutasi: pasangan tanpa waktu dan penyimpanan dua tahap sama-sama tertangkap |
| A8 | **Diterapkan (#43), dipersempit.** | Hanya lima jenis peringatan Fase 1 yang diporting (bukan seluruh `peringatan.ts` — Fase 2 dan kabar BMKG/MAGMA dibuang). Temuan ⚖ `picu-belum` **diselesaikan**: kini dihitung dalam Scope `sigap:broadcast:trigger` pemanggil, bukan seluruh Kemenkeu. Lihat DUMMY_REGISTRY butir 17 |
| A9, A10 | Tidak berlaku di sigap-api | A9 urusan Angular; A10 sudah kebijakan sejak P3.3 |
| A11 | **Diterapkan sebagai `[ASUMSI]` (keputusan pemilik 24 Sep 2026, P5.1); pertanyaan ke BaTII tetap terbuka.** | Akun layanan `SISTEM-BMKG` = baris `"User"` aktif tanpa `"UserRole"`, masuk lewat `IServiceIdentity` (DUMMY_REGISTRY butir 102). Membuka blokir penulisan oleh proses latar. Detail di bagian A11 di atas |

Aturan akses baru yang muncul saat menerjemahkan endpoint (bukan berasal dari prototipe):

- **`sigap:lampiran:upload` dipegang dua peran** (Pegawai `SELF` untuk #8, Satgas `UNIT` untuk #23). Karena
  Scope ditentukan permission, Satgas unit yang sama akan lolos Scope pada laporan pegawai lewat #8. Kontrak
  menetapkan #8 "hanya pelapornya", maka `pelaporId = pemanggil` dipasang **bersama** Scope (AND), bukan
  menggantikannya. Dibuktikan oleh `Hanya_pelapor_sendiri_yang_dapat_menambah_lampiran` (5 akun, termasuk Satgas
  yang merangkap pegawai). Pola yang sama berlaku untuk #9 (riwayat saya).
- **`sigap:lampiran:read` tidak cukup.** Scope `IKUT_INDUK` mensyaratkan induknya terlihat menurut permission
  induk (`laporan:read`, `asesmen:read`). Koordinator memegang `lampiran:read` tetapi tidak `laporan:read`, jadi
  lampiran laporan dijawab **404**, bukan 403.
