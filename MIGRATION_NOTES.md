# MIGRATION_NOTES

Ringkasan scope Fase 1, koreksi stakeholder, dan standar platform wajib. Disusun dari
`docs/PLAYBOOK.md` (Lampiran A–F), `docs/UR-SistemMKB.docx`, `docs/Catatan-Masukan-Probis.xlsx`
(sheet "Fitur & Data per Role", "Masukan SIGAP Tanggap Darurat", "Ucob SIGAP V4"), dan
`docs/Standar-Arsitektur-ICS.pdf`. Prioritas kalau dokumen bertentangan: lihat Lampiran F
PLAYBOOK (Standar Arsitektur ICS → masukan/revisi stakeholder → Dokumen UR → Kebutuhan Teknis →
mockup HTML, referensi visual saja).

---

## 0. Status berjalan dan arti "lanjutkan" (baca ini dulu)

Sesi baru cukup dibuka dengan prompt **P0 (Onboarding)** dari `docs/PLAYBOOK.md`, lalu pesan **"lanjutkan"**.
P4.5 dan P4.6 sudah selesai, jadi "lanjutkan" **bukan lagi** berarti putaran P4.5. Pada 25 Sep 2026 pekerjaan terakhir adalah
**P5.1 (pemicu Safety Check otomatis dari BMKG)**; "lanjutkan" berarti membaca daftar **keputusan yang menunggu pemilik** di
bawah, **menanyakan yang mana dulu** (jangan memilih sendiri), lalu mengerjakannya tanpa bertanya ulang hal yang sudah
diputuskan (lihat `AGENTS.md`, `ACCESS_RULES.md`, `DUMMY_REGISTRY.md` bagian 9).

**Keputusan yang menunggu pemilik (per 25 Sep 2026):**
1. **[SELESAI 25 Sep 2026, dengan dummy]** Tautan kantor ke unit. Keputusan pemilik: data asli platform akan memuat organisasi sampai lokasi kantor lewat API, jadi sementara dipakai dummy. `node infra/organisasi-seed/kantor-vertikal-dummy.mjs --yes-development` membuat 849 unit vertikal `isDemo` (satu per `kodeSatker`, induk = Eselon I OTK, `kabkota` dari gedung) dan mengisi `"KantorBmn"."unitId"` 1.424 dari 1.431 gedung; bawaannya dry-run, aman diulang, tanpa perubahan skema maupun kode aplikasi (DUMMY_REGISTRY bagian 9 butir 19). Sisa: 44 satker tersebar di beberapa kabupaten/kota (unit hanya memakai yang terbanyak) dan nama gempa berbentuk kecamatan/ibu kota ("Luwuk") tetap tidak cocok; pencocokan akhiran nama sengaja tidak dibangun.
2. **[SELESAI 25 Sep 2026]** Peragaan peringatan `GEMPA_KUAT_TANPA_KANTOR` di peramban dengan BMKG asli. Pemilik login sebagai Koordinator (`...0006`, lingkup NASIONAL); halaman Notifikasi menampilkan 8 peringatan gempa (MMI III-V; Pidie Jaya, Kab. Bima, Lembata, Sumur, Nagekeo, Padang Pariaman, Luwuk/Pohuwato, Pangalengan/Kertasari dan wilayah NTT) di samping 2 peringatan RTO skenario. Diperagakan setelah unit vertikal dummy terpasang (keputusan 1): 7 kejadian memicu broadcast otomatis ke unit berkabupaten/kota, sisanya wilayah tanpa unit yang muncul sebagai peringatan. Data peragaan (7 `"ActiveBroadcast"` otomatis, 26 sasaran, 33 jejak audit `user-layanan-bmkg`) sudah dihapus dan DB kembali ke baseline; API dev dinyalakan ulang tanpa `Bmkg__Aktif`. Catatan: `get_page_text` dibaca sebelum data selesai dimuat sehingga sempat tampak kosong; tampilan penuh terbukti lewat respons `GET /notifikasi` dan tangkapan layar. Halaman login ada di remote sigap-web (`npm run start:web`, port 4299), bukan di shell dummy, yang hanya memuatnya bila remote hidup.
3. **[DISELIDIKI 25 Sep 2026, TIDAK TERREPRODUKSI]** Dua tes lama yang flaky (`PersetujuanTests`: persetujuan serentak dan tanggap darurat pulih). Diulang tanpa satu pun kegagalan: 8x kelas itu sendiri, 5x seluruh `Sigap.Api.Tests` (903 tes), 2x solusi penuh sambil kelas itu diulang 6x, dan 15 proses `PersetujuanTests` bersamaan di tiga gelombang (total sekitar 35 run). Pembacaan kode: persetujuan berjalan di bawah `pg_advisory_xact_lock` per unit (`UnitKerjaPostgres`) dan `SelesaikanAsync` memakai kunci yang sama, jadi urutannya terjamin; balapan logika tidak ditemukan. Dugaan (belum terbukti): kehabisan waktu tunggu permintaan atau perintah DB saat mesin sangat terbebani, karena kedua tes menembakkan 5-6 permintaan paralel ke satu unit. Tidak ada kode yang diubah. Bila muncul lagi, simpan pesan galat lengkap (kode status yang diterima, bukan hanya hitungan) sebelum mengulang.
4. **Cache Redis selesai 25 Sep 2026** (cadangan hasil BMKG di Redis `sigap:`, lihat P5.1 putaran 3 di bagian 5.2 dan DUMMY_REGISTRY bagian 9 butir 20). Sisa P5: Web Push dinyalakan
   (menunggu Lampiran E #13 + kunci VAPID; pengirimnya sudah sungguhan sejak P5.3).
5. **[SELESAI 27 Sep 2026] Peringatan cuaca CAP dan rekap BNPB** sebagai endpoint baru **#48 `GET /info-bencana`** (keputusan pemilik: kontrak jadi 48 endpoint; `sigap:referensi:read` tanpa Scope karena prototipe menampilkan "Info Bencana Terkini" ke semua peran). P5.1 putaran 4 di bagian 5.2, asumsi di DUMMY_REGISTRY bagian 9 butir 21. **Belum:** halaman Angular yang memakainya (atribusi BMKG/BNPB wajib tampil di sana), dan MAGMA. Butir ⚖ ACCESS_RULES yang terbuka: V1, V4, A9.
6. **[SELESAI 27 Sep 2026] P5.2 object storage lampiran.** Driver `disk`/`s3` di `Lampiran:Driver`, MinIO lokal untuk pengembangan. Sekaligus menjawab pertanyaan terbuka #9 API_CONTRACT: dokumen (PDF/DOCX/XLSX) ditambahkan ke `AturanLampiran` untuk laporan dan asesmen, video/audio tetap khusus laporan — keputusan pemilik, lihat bagian 5.2 baris "P5.2" untuk rincian dan DUMMY_REGISTRY bagian 9 butir 3/4.
7. **[SELESAI 27 Sep 2026] P5.3 logika notifikasi broadcast.** Ketiga perilaku (penerima = lingkup, dedup lingkup beririsan, tahan luring) ternyata sudah terbangun sejak P4.5/P3.6; putaran ini menyatukannya ke `PemberitahuBroadcast`, menambah unit test, dan **menemukan bug Production**: port `libs/notifikasi` hanya diisi dummy pengembangan, sehingga di Production setiap use case yang memberi tahu gagal 500. Kini diisi implementasi Postgres + pengirim Web Push sungguhan (keputusan pemilik: dibangun sekarang). Rinciannya di bagian 5.2 baris "P5.3", asumsi di DUMMY_REGISTRY bagian 9 butir 22.
8. **[SELESAI 27 Sep 2026] P6.2 pemeriksaan otomatis dummy & lintas platform.** Sebagian besar yang diminta (stylelint tanpa hex di luar token, ESLint `no-restricted-imports` ke `libs/*-dummy`, `LarangDummyDiPublish` di `Sigap.Api.csproj`, deteksi kapitalisasi/CRLF/skrip npm di `periksa-repo.mjs`, build image Docker via `verifikasi:container`) **ternyata sudah ada** dari putaran sebelumnya — diverifikasi ulang, tidak dibangun dobel. Yang baru: `node scripts/verifikasi-siap-produksi.mjs` (gerbang gabungan web+api, sengaja gagal hari ini selama dummy aktif — dijalankan manual, bukan gerbang harian; keputusan pemilik dari pertanyaan "kapan gerbang ini dijalankan": skrip terpisah, bukan menambah ke `check:web`/`verifikasi-linux`), `node scripts/status-dummy.mjs` (mem-parse tabel Status DUMMY_REGISTRY bagian 1, bukan salinan terpisah), banner startup Development di `sigap-api` (`BannerDummy.Tulis()`), dan pemeriksa path backslash di berkas konfigurasi (`tsconfig*.json`, `angular.json`, `.csproj`, `.slnx`) di `periksa-repo.mjs`. Detail di DUMMY_REGISTRY bagian 8 butir 4–5.
9. **[AUDIT SELESAI 27 Sep 2026; SELURUH TEMUAN DIPERBAIKI: T1, T2, S1, S2 27 Sep 2026, S3 dan R6–R10 28 Sep 2026] P6.3 audit keamanan tiga lapis.** **Perbaikan T1:** `RingkasanJejak.RahasiaPerEntitas` menyamarkan `lat`/`lng`/`keterangan` `SafetyCheckResponse` (per entitas, sebab `"GangguanLayanan"."keterangan"` tetap harus tercatat); `Susun` kini menerima nama entitas. Baris jejak lama di DB dev (data skenario) tidak diubah. **Perbaikan T2:** `BroadcastStore.BacaAsync` menyaring `unitDisasar`/`unitDilewati` ke wilayah dasar pembaca di klausa WHERE (dibagi dengan `Dasar()` lewat `WilayahDasar`), angka `jumlah*` tetap penuh dihitung di SQL. Tes baru: Infrastructure +7 (unit `RingkasanJejak`), Api +4 (jejak #2 tanpa koordinat, jejak #6 tanpa keterangan, daftar sasaran per lingkup, unit dilewati di luar lingkup beserta nama pemicunya tidak disebut); tiga mutasi tertangkap (daftar rahasia dikosongkan, saringan sasaran dilepas, angka dihitung dari daftar tersaring). 1.946 tes solusi lulus, `dotnet format` bersih, `verifikasi-linux api` lulus. **Perbaikan S1 (temuan 3, penjaga Lapis 1):** `ProteksiEndpointTests` (95 tes) membaca endpoint dari `EndpointDataSource` host yang berjalan, bukan daftar tulisan tangan, dan `KontrakApi` kini ikut membaca kolom Permission API_CONTRACT: seluruh 46 endpoint bisnis terpasang dan cocok dengan kontrak; setiap aksi ber-`[KemenkeuAuthorize]` tunggal dengan permission kontrak (`/me/konteks` wajib `[Authorize]` tanpa permission); yang terbuka hanya yang `publik` menurut kontrak (health) atau alat Development (OpenAPI/Scalar); setiap endpoint tanpa token 401 dan dengan token sah tanpa permission 403 (kecuali #36: 200). Temuan sampingan: upload lampiran (#8, #23) menjawab 415 sebelum otorisasi bila tipe konten salah, karena `[Consumes]` ikut memilih endpoint di routing — bukan celah (tidak ada data diproses), tes mengirim multipart sesuai metadata endpoint. Tiga mutasi tertangkap (atribut dicabut, permission tertukar, `[AllowAnonymous]`). 2.041 tes solusi lulus, `verifikasi-linux api` lulus. **Perbaikan S2 (temuan 4, Scope Safety Check):** #1–#6 dan peringatan `SC_BELUM_DIJAWAB` (#43) kini membaca `GetScope(permission)` endpointnya — `SASARAN_SAYA` disusun `LingkupSafetyCheck.UnitSasaranSaya` dari wilayah dasar grant (profil domain, pola sama dengan `TERSENTUH`), `SELF` dan `UNIT` lewat `ApplyScope` di klausa WHERE (pegawai #6, unit dan penyebut #4/#5, riwayat #3). #35 Monitor meneruskan lingkup `monitor:read` ke ringkasan rekap. Satu perubahan semantik yang tidak tampak hari ini: `unitId` baris yang dicatatkan Satgas (#6) = unit pegawai yang lolos Scope (dari database, bukan dari permintaan), bukan unit Satgas — sama di bawah Scope UNIT, dan sesuai KANDIDAT_SCOPE_SIEVE S3. Pengait uji `AplikasiUjiDb.LingkupDikosongkan` (permission tetap ada, lingkup kosong); 6 tes `LingkupSafetyCheckTests` gagal pada kode lama dan lulus pada kode baru; 299 tes Safety Check/Monitor/Notifikasi/Broadcast lama tetap hijau (perilaku hari ini identik). 2.047 tes solusi lulus, `verifikasi-linux api` lulus. **Perbaikan S3 (temuan 5, header keamanan, 28 Sep 2026):** `HeaderKeamanan` (paling depan di pipeline) memasang `Cache-Control: no-store`, `nosniff`, CSP `default-src 'none'; frame-ancestors 'none'`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` pada `/api/*` dan `/health/*` lewat `OnStarting` + `TryAdd` — ikut pada 401/403/404 yang tidak dibuat controller, dan tidak menimpa nilai endpoint (lampiran #11 tetap `private, no-store`; health tetap `no-store, no-cache`); header `Server` Kestrel dimatikan. HSTS tidak dipasang (`[ASUMSI]` milik gateway, DUMMY_REGISTRY bagian 9 butir 23); OpenAPI/Scalar Development tidak tersentuh. 6 tes `HeaderKeamananTests`; dua mutasi tertangkap (middleware dicabut, `TryAdd` diganti penimpaan → tes lampiran gagal). Terbukti juga di Kestrel sungguhan (`curl`: tanpa `Server`, lima header ada pada 200 dan 401). 2.053 tes solusi lulus, `verifikasi-linux api` lulus. **Perbaikan temuan rendah (R6–R10, 28 Sep 2026):** (R6) agregat #31 per kelompok dihitung per unit di SQL (`GROUP BY`), bukan satu baris per pegawai di memori; (R7) #21 membaca ulang kiriman lewat `BacaKirimanSendiriAsync` (tanpa Scope baca, dibatasi `submittedById` pemanggil, pola `LaporanStore.TambahAsync`) — `asesmen:create` berprofil tulis `UNIT_SENDIRI`, sehingga baca ulang lewat `asesmen:read` akan 500 setelah data tersimpan; (R8, keputusan pemilik) #44 memindahkan pemilik langganan hanya bila `keys` sama, selain itu 409 `LANGGANAN_MILIK_PERANGKAT_LAIN` (kode galat baru, API_CONTRACT #44); (R9) `cari` #4 maks 100 karakter dan `%`/`_` di-escape, satu aturan dengan #42 (`Pencarian.Rapikan`, `PolaCari.Mengandung`); (R10) `appsettings.Development.json` tidak ikut artefak publish (dijaga `verifikasi-linux api` lewat daftar publish MSBuild), dan — keputusan pemilik — seam login dev sigap-web dibiarkan untuk UAT tetapi `verifikasi-siap-produksi.mjs` gagal selama masih ada. Tiap perbaikan punya tes yang gagal pada kode lama. Laporan awal audit: Tidak ada temuan kritis (tidak ada endpoint tanpa proteksi; koordinat pegawai tidak keluar lewat API mana pun di luar Satgas unitnya; Redis hanya berisi 4 kunci data publik BMKG/BNPB). **Dua temuan TINGGI, keduanya terbukti:** (T1) `"JejakPerubahan"."ringkasan"` menyimpan `lat`/`lng` dan `keterangan` jawaban Safety Check apa adanya — `RingkasanJejak.Rahasia` menyamarkan catatan SDM asesmen tetapi tidak ketiga kolom `"SafetyCheckResponse"` ini, padahal di API dua di antaranya di-Sieve (terbukti di DB dev: keterangan Satgas "Terjebak di lantai 3…" tersimpan utuh); (T2) `GET /safety-check/broadcast/{id}` (#15) mengirim **seluruh** `unitDisasar`/`unitDilewati` (plus nama pemicu broadcast pemegang) kepada pembaca berlingkup sempit, padahal API_CONTRACT #15 mewajibkan daftar disaring ke lingkup pembaca — terbukti lewat tes sementara (Satgas lingkup UNIT melihat 3 unit se-provinsi; tesnya sudah dihapus). **Sedang:** tidak ada penjaga otomatis bahwa setiap aksi controller ber-`[KemenkeuAuthorize]` (hanya `/me/konteks` yang diuji tanpa token); domain Safety Check (#1–#6) menurunkan lingkup dari identitas dengan tangan alih-alih `GetScope` (aman hari ini karena kebijakan = UNIT, tetapi perubahan kebijakan tidak akan berlaku); respons JSON tanpa `Cache-Control: no-store`/security header (lampiran #11 sudah benar). **Rendah:** `SafetyCheckKelompokAsync` memuat satu baris per pegawai se-lingkup ke memori; baca-ulang dengan permission berbeda (`KirimAsesmen` pakai `AsesmenRead`); langganan push #44 berpindah pemilik berdasarkan `endpoint` saja; `cari` rekap tanpa batas panjang; seam login dev (password grant ke Keycloak lokal) ikut terbawa bundel produksi sigap-web; `appsettings.Development.json` (kredensial dev) ikut masuk image verifikasi sementara — Dockerfile production kelak wajib mengecualikannya. Daftar lengkap dan urutan perbaikan disampaikan ke pemilik 27 Sep 2026.
10. **[SELESAI 28 Sep 2026] P6.4 audit trail.** Sebagian besar sudah ada sejak P4.5 (interseptor `SaveChanges` + `IJejakAudit`, satu transaksi dengan datanya): trigger `DIPICU` (peran/lingkup/unit pemicu), respons pegawai, pencatatan Satgas, verifikasi/penolakan, kirim asesmen — diverifikasi, tidak dibangun ulang. "Pending" bukan tindakan (API_CONTRACT #28). **Selisih yang ditutup:** (1) akses baca data sensitif yang diwajibkan API_CONTRACT 1.7 **tidak pernah dicatat** (`CatatAksesAsync` tidak dipanggil) — kini `CatatAksesFilter` global mencatat `DIAKSES` untuk setiap respons bertanda `IAksesTercatat` (hari ini #4 rekap), gagal-tertutup, tanpa isi data; `ArsitekturAksesTests` menolak DTO pembawa `RekapBarisDto` tanpa penanda; (2) revisi asesmen kini `DIREVISI` dengan `alasan` = id versi asal (dulu `DIBUAT` tanpa rujukan, nilai "sebelum" tak tertelusur); (3) persetujuan Pimpinan kini `DISETUJUI` dengan `alasan` = id asesmen (tabel deklarasi tidak punya kolom rujukan, skema tetap). Koordinat tetap disamarkan di jejak (lebih ketat dari syarat prompt "hanya bila berubah", keputusan P6.3 T1). 5 tes baru, empat mutasi tertangkap (filter tidak dipasang, DTO tanpa penanda, revisi/persetujuan dicatat `DIBUAT`). Penggantian oleh audit bawaan `iam.plugin`: DUMMY_REGISTRY bagian 9 butir 8.

**Gerbang verifikasi (CI GitHub tidak dipakai, akun terkunci billing, pemilik tidak akan memulihkannya):**
`node scripts/verifikasi-linux.mjs web|api|repo|semua` (satu target per pemanggilan) dan `npm run verifikasi:container` (image dijalankan, bukan hanya diuji), plus `dotnet format apps/sigap-api/sigap-api.slnx
--verify-no-changes` dan `npm run check:web`. Bangun ulang .NET gagal bila API dev (`dotnet run`, port 5299) masih hidup karena
mengunci DLL: hentikan dulu proses `Sigap.Api.exe` beserta induknya. Login peramban dilakukan pemilik (kata sandi akun uji di `.env`,
NIP di `infra/organisasi-seed/README.md`); token hanya di memori, jadi memuat ulang halaman = login ulang.

**Posisi P4.5 (23 Sep 2026):** **P4.5 selesai — seluruh 47 endpoint kontrak sudah dibangun**, lulus
verifikasi Linux, **sudah di-commit lokal (24 Sep 2026, belum di-push)** per lapisan — bukan per domain, karena domain saling merujuk (Asesmen, Referensi, Laporan, Notifikasi, SafetyCheck, Broadcast) dan satu commit per domain tidak dapat dikompilasi sendiri. Tiap lapisan terbukti mandiri di checkout bersih (Domain 449 tes, Application 24, Infrastructure terbangun tanpa peringatan), dan HEAD bersih menjalankan 1.563 tes solusi + 43 `iam-dummy`, 0 gagal. Tujuh putaran: Laporan/Lampiran/
Verifikasi (#7-#11, #17, #18), Referensi (#37-#42), Asesmen/Layanan Kritis/Tanggap Darurat (#19-#29),
Broadcast/Trigger Safety Check (#12-#16), Safety Check/SOS (#1-#6), Monitor SC & Sumber Daya (#30-#35),
Notifikasi (#43-#45), ditambah #36 (P4.2) dan #46-#47 (health, sudah sejak P4.1).
1.563 tes lulus (1.545 sebelumnya + 18 tes Notifikasi baru; termasuk 43 tes `Kemenkeu.Iam.Dummy.Tests`).

**P4.6 (Angular) sudah dimulai (23-24 Sep 2026).** Putaran pertama membangun **seluruh 20 halaman**
yang menggantikan prototipe untuk keempat feature folder — `safety-check/` (#1-#6, #12-#16),
`verifikasi-alert/` (#7-#11, #17-#18), `asesmen-bencana/` (#19-#29), `dashboard/` (#30-#35) — plus
`notifikasi/` (#43) dan infrastruktur inti (`core/auth`, `core/referensi`, halaman `masuk`). Dibangun
di atas fondasi kosong: sebelumnya hanya ada `beranda`/404, tanpa `HttpClient`, tanpa auth, tanpa satu
pun panggilan API. Diverifikasi hidup di browser (login sungguhan → dashboard menampilkan data nyata
dari sigap-api), bukan hanya build hijau. Rinciannya di bagian 5.2 baris "P4.6 (putaran 1)".

**`.spec.ts` sudah dilengkapi (24 Sep 2026):** seluruh 23 komponen baru (20 halaman + `masuk` + `beranda`
yang ditulis ulang) punya tes Vitest/TestBed — 61 tes, `npm run check:web` hijau. Catatan pola yang
dipakai ulang bila menambah tes baru: `fixture.whenStable()` **tidak** menunggu `Promise` dari layanan
tiruan (`vi.fn().mockResolvedValue`) di `ngOnInit` — pakai `flushAsync()` (`shared/testing/flush-async.ts`)
untuk rantai `await`/`Promise.all` sendiri, dan sediakan rute nyata di `provideRouter([...])` bila
komponen memanggil `router.navigate` (NG04002 kalau tidak ada yang cocok).

**P5.1 putaran 1 (pemicu Safety Check otomatis dari BMKG) sudah dibangun (24 Sep 2026), MATI bawaan.** Worker
`PemantauBmkg` -> `PicuBroadcastOtomatis` atas nama akun layanan `SISTEM-BMKG` (keputusan pemilik untuk A11: baris
`"User"` aktif tanpa `"UserRole"`, `[ASUMSI]`), memakai `store.PicuAsync` yang sama dengan trigger manual. Menyalakannya:
`Bmkg__Aktif=true` + akun layanan ada (SQL di README API). **Keterbatasan yang harus dibaca pemilik sebelum menyalakan:**
isian "Dirasakan" BMKG mencampur kabupaten/kota dengan kecamatan atau ibu kota ("Luwuk", "Sumur", "Agam"), dan OTK tidak
memuat `kabkota`, jadi di data nyata hari ini pemicu hampir pasti tidak menjangkau unit mana pun. Rinciannya di bagian 5.2
baris "P5.1 (putaran 1)" dan DUMMY_REGISTRY bagian 9 butir 18.

**P4.6 selesai (25 Sep 2026)** dengan dua dummy atas keputusan pemilik, sambil menunggu BaTII: paginasi dan diagram batang (`KeuPaginationComponent`, `KeuBarChartComponent`, DUMMY_REGISTRY 2.7 butir 104-105 dan 2.8). Sisa yang bukan pekerjaan kode: pertanyaan paginasi/grafik ke BaTII belum dikirim, dan UI Web Push belum bisa dicoba ujung ke ujung (butuh service worker shell asli, kunci VAPID, dan pengirim push P5.3). Seam login dev dan CORS pengembangan dicatat DUMMY_REGISTRY butir 51/58 — baca sebelum menyentuh `core/auth`.

**Data organisasi dev sudah terisi (24 Sep 2026):** `node infra/organisasi-seed/seed.mjs --yes-development`
memuat 444 unit OTK asli + 5 unit demo, 10 akun uji Keycloak, dan 12 pegawai pelengkap ("Unit" 449, "User" 22).
Dashboard yang dulu menampilkan nol kini punya lingkup nyata. Skenario UAT gempa Pasaman (KPP Madya
Pekanbaru) menyusul lewat `node infra/skenario-seed/seed.mjs --yes-development` dan **sudah diverifikasi
hidup di dashboard Perwakilan** (bagian 5.2, baris "Seeder skenario").

Pekerjaan sigap-api yang masih terbuka (bukan endpoint baru) ada di bagian 5.3: pengiriman push sungguhan (`IGudangLanggananPush`/`ICatatanKiriman`
masih dummy), dan butir ⚖ di ACCESS_RULES (V1, V4, A9; A11 sudah diterapkan sebagai `[ASUMSI]`, lihat P5.1 di bawah).

**Cara kerja tiap putaran (pola sudah baku, ikuti):** Domain (aturan murni, tes pembanding fikstur bila ada) ->
Application (use case + port) -> Infrastructure (store ber-Scope di WHERE) -> Api (controller tipis,
`[KemenkeuAuthorize]`, `[ProduksGalat]`, XML doc) -> tes DB per run (peran yang tidak berhak, Scope lewat SQL,
Sieve, validasi, balapan, jejak audit, OpenAPI) -> uji mutasi -> `dotnet format --verify-no-changes`,
`npm run periksa:repo`, seluruh tes, `node scripts/verifikasi-linux.mjs api` dan `repo` -> perbarui README api,
DUMMY_REGISTRY bagian 9, ACCESS_RULES, dan berkas ini. Docker Desktop dinyalakan pemilik; tanpa PostgreSQL
tes DB dilewati, jadi minta pemilik menyalakannya sebelum verifikasi.

**Sebelum menulis kode, tanyakan/umumkan ke pemilik:**
- P4.6 putaran 1 (23-24 Sep 2026) membangun seluruh halaman tapi belum ada `.spec.ts` per komponen dan
  belum diberitahu ke BaTII soal dua kebutuhan katalog baru (paginasi, grafik — DUMMY_REGISTRY 2.8).
  Baca DUMMY_REGISTRY butir 51/58 sebelum menyentuh `core/auth` (seam login dev + CORS pengembangan).
- Notifikasi (#43-#45) selesai 23 Sep 2026 dengan tujuh tebakan ber-`[ASUMSI]` — lihat DUMMY_REGISTRY
  butir 17 sebelum menyentuh domain ini lagi (daftar peringatan yang dipersempit dari prototipe, makna
  `PICU_BELUM` yang di-Scope, `IGudangLanggananPush`/`ICatatanKiriman` yang masih dummy).
- Monitor (#30-#35) selesai 23 Sep 2026 dengan enam tebakan ber-`[ASUMSI]` — lihat DUMMY_REGISTRY butir 16
  sebelum menyentuh domain ini lagi (definisi "unit" pada dashboard, makna `sejak`, sumber angka `layanan`,
  urutan fallback `asesmenTerkini` #35).
- Terbuka: **V1** (Sieve NIP), **V4** (Sieve uraian), **A9** (dua rute Angular ⚖); dan tafsiran
  revisi asesmen per **blok** (bukan per field) menunggu konfirmasi pemilik/tampilan Angular.
- Perubahan struktur tabel dilaporkan dulu. Jangan menambah remote git, push, atau menempel secret.

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
   safety check. Spesifikasinya di [API_CONTRACT.md](API_CONTRACT.md) bagian 5. Perubahan tabel
   lain tetap harus dibawa ke diskusi lebih dulu.

**Lima aturan dummy** (Fase 3, PLAYBOOK bagian III): karantina di `libs/*-dummy/` /
`apps/shell-dummy/`; kontrak identik dengan dokumentasi platform; isi sesederhana mungkin;
tidak boleh naik ke production (build gagal kalau dummy masih ter-resolve); tercatat di
[DUMMY_REGISTRY.md](DUMMY_REGISTRY.md).

**Catatan lintas platform (Windows dev / Linux production):** kapitalisasi nama file & import
harus persis sama, akhir baris LF, path forward-slash (`Path.Combine()` di C#), script npm
lintas platform (`rimraf`, `cross-env`), verifikasi build di container Linux sejak awal — detail
lengkap di PLAYBOOK Bagian I.

---

## 4. Dokumen belum ada / masih perlu dibuat

- `AGENTS.md` — belum ada di repo. Dibuat di P4.3 (Fase 4), isi mengikuti spesifikasi di
  PLAYBOOK P4.3.
- ~~`DUMMY_REGISTRY.md`~~ — **sudah dibuat 18 Sep 2026** bersama dummy pertama
  (`libs/keu-ui-dummy`, P3.1). Perbarui tiap ada dummy atau asumsi baru.
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
  Perpindahan ini **sudah dijalankan dan di-push** (18 Sep 2026).
- **Prototipe Next.js** (rujukan utama migrasi), salinan lokal: `C:\dev\MKB APPS\App`. Prompt
  PLAYBOOK yang menyebut "prototipe" merujuk ke sini. **Di laptop lain, clone ke jalur yang
  sama persis** (`C:\dev\sigap` dan `C:\dev\MKB APPS\App`), supaya seluruh rujukan jalur di
  dokumen ini tetap berlaku.
- **Baseline porting: prototipe commit `1b1487a` (branch `main`, `atepagung/sigap-prototipe`).**
  API_CONTRACT, PERMISSION_MAP, dan `BUSINESS_RULES_INDEX.md` diturunkan dari keadaan prototipe
  pada commit itu. Perubahan prototipe sesudahnya **tidak** otomatis masuk kontrak. Sebelum
  porting (P4.4), periksa `git log 1b1487a..origin/main` di repo prototipe dan putuskan per
  perubahan.
- Di luar git, hanya di laptop pertama (`C:\dev\MKB APPS\`): folder `Input\` (termasuk
  `Alur Sistem MKB.pdf`, `23082026 Probis_MKB_update.pdf`, `2. Presentasi SIKUAD.pptx`, yang
  tidak ada di `docs/`), folder `Output\` (termasuk `Dokumen BaTII\`), dan dokumen Kebutuhan
  Teknis `.docx`. Salin manual bila diperlukan.
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
| P2.2 | Inventaris 76 komponen prototipe, dikelompokkan jadi kandidat design system / khusus SIGAP / jangan diporting | `COMPONENT_INVENTORY.md` di repo ini |
| P3.1 | Dummy design system + registry dummy. SCSS terverifikasi kompilasi bersih (`sass` 1.80.6); TypeScript bersih selain `@angular/*` yang memang belum di-install | `libs/keu-ui-dummy/`, `tsconfig.base.json`, `DUMMY_REGISTRY.md` |
| P3.2 | Shell dummy + sigap-web sebagai remote Native Federation (Angular 22.1). Identitas remote di satu berkas. Diuji di browser: mode mandiri, deep link, sinkronisasi URL dua arah (klik sidebar shell, klik di dalam remote, back/forward, pindah ke modul lain dan kembali), remote mati → shell tetap hidup. 9 unit test lulus. Alias `@danarakca/keu-ui` terbukti ter-resolve. Jalankan lewat `npm run start:web` / `npm run start:shell`, bukan `ng serve` | `apps/shell-dummy/`, `apps/sigap-web/`, `package.json` root (npm workspaces) |
| P3.3 | Dummy `iam.plugin` (.NET 10) + directive `*hasPermission`. Kebijakan IAM sebagai data di `apps/sigap-api/iam-policy.sigap.json` — terbukti identik dengan draf PERMISSION_MAP bagian 7 (blok JSON di dokumen itu diganti rujukan ke berkas). 38 tes .NET: aturan PERMISSION_MAP pada kebijakan asli, Scope dieksekusi di SQLite, SQL PostgreSQL diperiksa (`WHERE ... = ANY (@UnitIds)`, `WHERE FALSE` tanpa izin), pipeline HTTP dengan JWT (401/403/Sieve). 4 tes directive | `libs/iam-dummy/`, `libs/iam-dummy-web/`, `apps/sigap-api/iam-policy.sigap.json` |
| P3.4 | Realm Keycloak `kemenkeu` sesuai Kebutuhan Teknis bagian E: dua client, klaim `nip`/`kode_satker`/`kode_eselon1`/`groups`, aud `sigap-api`, 15 menit/8 jam, sepuluh akun uji (NIP palsu `9000…`). Kredensial acak di `.env` (tidak masuk git). Terverifikasi pada Keycloak berjalan: 18/18 pemeriksaan spesifikasi, 13/13 uji ujung ke ujung token asli → `libs/iam-dummy` (jumlah permission per peran cocok dengan PERMISSION_MAP; Admin & dua akun Fase 2 = 0). Skrip verifikasi disimpan di repo | `infra/keycloak/`, `docker-compose.yml`, `.env.example` |
| P4.3 | Dasar kualitas kode. `AGENTS.md` di root dan `apps/sigap-web` (identitas remote, routing, styling, keamanan, scope Fase 1, larangan proyek, bagian **Lintas platform**). sigap-web: ESLint (aturan platform ditegakkan: tanpa `children`, tanpa hex/rgb, impor lewat `@danarakca/*`, tanpa `document.title`), Prettier, Stylelint (larangan warna di luar `_tokens.scss`), Husky + lint-staged + commitlint, skrip `rimraf`/`cross-env`. sigap-api: `.editorconfig` (LF), analyzer, `dotnet format` sebagai penjaga penamaan. CI GitHub Actions dengan filter path (`sigap-web`, `sigap-api`, `repo`). `scripts/periksa-repo.mjs` (LF di index, kapitalisasi, skrip npm, `Path.Combine`) dan `scripts/verifikasi-linux.mjs` (pipeline di container Linux). `CONTRIBUTING.md` (Conventional Commits). **Seluruh pipeline terbukti lulus di container Linux** (node:24 dan dotnet sdk 10). Workflow belum pernah berjalan di GitHub sungguhan | `AGENTS.md`, `apps/sigap-web/`, `.github/workflows/`, `scripts/`, `CONTRIBUTING.md` |
| P4.2 | EF Core schema-first ke 33 tabel (32 prototipe + tabel ke-33), entity dikelompokkan per domain, 13 tabel Fase 2 dipetakan terpisah. Sumber skemanya DDL Prisma apa adanya di `infra/skema/` (prototipe tidak punya folder migrasi), terpasang ke PostgreSQL dev lewat `terapkan.mjs`. Konvensi Prisma ditiru di aplikasi: cuid, `updatedAt`, waktu UTC tanpa zona. `OrganisasiDariTabelUserUnit` kini membaca `"User"`/`"Unit"`; `/health/ready` memeriksa database. 191 tes sigap-api lulus; penjaga skema dibuktikan menggigit lewat tiga uji mutasi. Dokumen kandidat Scope/Sieve dibandingkan dengan PERMISSION_MAP | `infra/skema/`, `apps/sigap-api/src/Sigap.Infrastructure/Persistensi/`, [KANDIDAT_SCOPE_SIEVE.md](KANDIDAT_SCOPE_SIEVE.md) |
| P4.1 | Solusi .NET 10 `apps/sigap-api`: 4 proyek layer + 3 proyek tes, dikelompokkan per domain (11 domain dari bagian 2 API_CONTRACT). Lima aspek asesmen jadi sub-struktur di dalam domain Asesmen, **bukan** lima domain — satu asesmen ditulis ke dua tabel dalam satu transaksi dan tidak punya endpoint per aspek. Controller tipis, satu kelas per use case, tanpa MediatR. Konvensi di `Directory.Build.props` + `.editorconfig`; OpenAPI bawaan .NET 10 + Scalar (Development saja). `GET /me/konteks` berjalan. 39 tes lulus; `dotnet publish` gagal selama dummy masih ter-resolve | `apps/sigap-api/`, [README-nya](apps/sigap-api/README.md) |
| P3.6 | Abstraksi kanal notifikasi + tiga kanal (`dalam-aplikasi`, `web-push` nonaktif, `log` dummy), dipilih lewat `Notifikasi:Kanal` di konfigurasi. **Abstraksinya bukan dummy** dan ada di `libs/notifikasi`; yang dummy hanya kanal log dan pengisi port sementara. Tanpa perubahan skema — idempotensi memakai `"KirimanPush"` yang sudah ada. Konfigurasi tanpa kanal tahan luring ditolak saat proses mulai (syarat P5.3). 58 tes lulus | `libs/notifikasi/`, `libs/notifikasi-dummy/` |
| P4.4 (aturan Domain) | Aturan murni `src/logic/` Fase 1 diporting ke `Sigap.Domain` (Referensi, SafetyCheck, Laporan, Lampiran, Broadcast, Asesmen, Integrasi). Tes pembanding membaca fikstur emas hasil fungsi **asli** prototipe `1b1487a` (334 kasus, `tests/pembanding-prototipe/buat-fikstur.mjs`); selisih API_CONTRACT bagian 6 ditandai per kasus dan dibuktikan berbeda. Tiga uji mutasi terbukti menggagalkan tes. Satu selisih nyata tertangkap dan diperbaiki: `toFixed(1)` JS ≠ `ToString("F1")` .NET pada nilai tengah. Kontrol akses tidak diporting, dicatat di [ACCESS_RULES.md](ACCESS_RULES.md) (A1–A11, empat ⚖). 558 tes solusi lulus, `dotnet format` bersih | `apps/sigap-api/src/Sigap.Domain/`, `apps/sigap-api/tests/`, `ACCESS_RULES.md` |
| P4.5 (putaran 1) | 7 dari 47 endpoint: **Laporan + Lampiran + Verifikasi** (#7–#11, #17, #18), tiap endpoint dengan permission, Scope di `WHERE` (dibuktikan lewat SQL yang dijalankan), tanpa kolom sensitif di respons, OpenAPI, dan tes termasuk peran yang tidak berhak. Tes berjalan di database PostgreSQL yang dibuat per run dari DDL asli. 878 tes lulus, **0 dilewati**; pipeline `api` dan `repo` lulus di container Linux. Tes menemukan tiga bug produksi yang sudah diperbaiki: paginasi `halaman=` tak pernah berfungsi (nama parameter bertabrakan dengan awalan model), galat pengikatan model keluar `application/json`, dan pembersihan penyimpan lampiran menghapus berkas milik lampiran lain. Lima uji mutasi (Scope, "hanya pelapor", atomisitas verifikasi, Scope asesmen, pembersihan berkas) semuanya tertangkap | `apps/sigap-api/src/*/Laporan`, `*/Lampiran`, `tests/Sigap.Api.Tests` |
| P4.5 (audit) | Jejak audit terpusat (API_CONTRACT 1.7): interseptor `SaveChanges` untuk 12 entitas Fase 1, `IJejakAudit` untuk yang melewati pelacak (`ExecuteUpdate`) dan akses baca, dalam transaksi yang sama dengan datanya; tanpa identitas pelaku penulisan ditolak. Penjaga arsitektur: setiap `ExecuteUpdate`/`ExecuteDelete` wajib mencatat jejak, dan tabel baru di model wajib diputuskan. 1.005 tes lulus (termasuk Linux); tujuh mutasi tertangkap | `Infrastructure/Audit`, `Application/Audit`, `tests/*/Audit` |
| P4.5 (putaran 2) | Domain **Referensi** (#37-#42), enam endpoint dengan Scope atas `"Unit"."id"` (UNIT / WILAYAH / ESELON_I / NASIONAL) di klausa WHERE dan bentuk respons yang dicatat sebagai asumsi (DUMMY_REGISTRY bagian 9 butir 7) | `Application/Referensi`, `Infrastructure/Referensi`, `Api/Referensi` |
| P4.5 (putaran 3) | Domain **Asesmen + Layanan Kritis + Tanggap Darurat** (#19-#29), 11 endpoint. Satu versi = dua tabel (pasangan S5), seri dan persetujuan diturunkan, penulisan di bawah kunci unit (`pg_advisory_xact_lock`), Sieve dua catatan SDM (pemantau menerima `null`), gangguan layanan memulai hitung mundur RTO, notifikasi ke Pimpinan dan pemantau. 1.318 tes lulus (Domain 449, Application 24, Infrastructure 217, Api 628), 0 dilewati. Tes menemukan **satu kebocoran nyata** yang sudah ditutup: catatan SDM tertulis polos di `"JejakPerubahan"."ringkasan"` (kini disamarkan). Tujuh belas uji mutasi (Scope baca/daftar/layanan/tanggap darurat, Sieve, kunci unit, pemeriksaan kembar, pasangan tanpa waktu, atomisitas dua tabel, gangguan ganda, penerima pemantau, penyamaran jejak, dan lainnya) semuanya tertangkap; tiga yang awalnya lolos (Scope penyelesaian tanggap darurat, pasangan tanpa waktu, penyimpanan dua tahap) memicu tes tambahan. Tanpa perubahan skema | `Domain/Asesmen`, `Application/Asesmen`, `Infrastructure/Asesmen`, `Api/Asesmen`, `tests/*/Asesmen` |
| P4.5 (putaran 4) | Domain **Broadcast (trigger safety check)** (#12-#16), 5 endpoint. Lingkup pemicu dipilih lewat `DataScope.Terluas()` (ekstensi baru `Kemenkeu.Iam.Dummy`), bukan nama peran (ACCESS_RULES A1 diselesaikan tanpa data prioritas baru). Satu unit dipegang satu broadcast aktif per jenis bencana (indeks unik parsial); trigger tidak pernah ditolak, unit yang sudah dipegang dilewati; balapan dua trigger ditangkap lewat penanganan pelanggaran indeks unik dan savepoint. Peran/profil/unit pemicu dititipkan di jejak audit (`"ActiveBroadcast"` tidak punya kolomnya). Otorisasi `PEMICU_ATAU_MENCAKUP` (#16) dibaca dari `DataScope.Grants`, diserialkan lewat `IUnitKerja`. 1.412 tes lulus (Domain 449, Application 24, Infrastructure 217, Api 722, iam-dummy 43), 0 dilewati. Enam uji mutasi (Scope daftar, otorisasi #16, kunci #16, validasi data wilayah, validasi penyempit, savepoint) — lima tertangkap; savepoint eksplisit ternyata redundan dengan penyavepointan implisit Npgsql per `SaveChanges` dalam transaksi manual, tetap dipertahankan untuk kejelasan dan tidak bergantung pada perilaku provider tertentu. Tanpa perubahan skema | `Domain/Broadcast`, `Application/Broadcast`, `Infrastructure/Broadcast`, `Api/Broadcast`, `tests/*/Broadcast`, `libs/iam-dummy` |
| P4.5 (putaran 5) | Domain **Safety Check / SOS** (#1-#6), 6 endpoint. `SASARAN_SAYA`/`UNIT` (rekap) selalu berarti unit pemanggil sendiri, jadi diambil dari identitas tanpa lewat `GetScope`/`ApplyScope` (pola `UNIT_SENDIRI`). Upsert #2 (pegawai sendiri) dan #6 (dicatatkan Satgas) menimpa `"createdAt"` sebagai waktu jawab (tabel tidak punya `updatedAt`); jawaban sendiri selalu mengosongkan `dicatatOlehId`/`keterangan` peninggalan Satgas. Penyebut rekap (#4/#5) — Pegawai Umum aktif di unit — dibaca dari `"UserRole"` (ACCESS_RULES A5, `[ASUMSI]`), LEFT JOIN ke jawaban bersyarat `broadcastId` yang diminta, diurutkan BUTUH_BANTUAN/BELUM/AMAN/nama di SQL. 1.515 tes lulus (Domain 449, Application 24, Infrastructure 217, Api 825, iam-dummy 43), 0 dilewati. Enam uji mutasi (penyebut tanpa filter peran, validasi disasar, pengosongan catatan Satgas, LEFT JOIN rekap, validasi alasan, validasi unit pegawai) semuanya tertangkap. Tanpa perubahan skema | `Domain/SafetyCheck`, `Application/SafetyCheck`, `Infrastructure/SafetyCheck`, `Api/SafetyCheck`, `tests/*/SafetyCheck` |
| P4.5 (putaran 6) | Domain **Monitor SC & Sumber Daya** (#30-#35), 6 endpoint, read only mutlak (Pimpinan Satker tidak termasuk). Agregat safety check (#30/#31) dan asesmen (#30/#32) dirakit dengan memanggil ulang `IAsesmenStore`/`PerakitAsesmen.Persetujuan` dengan lingkup `monitor:read`, bukan menulis ulang mesin seri. Sumber angka `layanan` (#30/#33/#34) = `"LayananKritis"` + `"GangguanLayanan"` langsung (status kini, bukan JSON `layananTerdampak` asesmen) — konsumen pertama `Rto.Hitung` yang sudah ada sejak putaran 3 tapi belum dipakai endpoint mana pun. Agregat lima aspek (#33) membaca "versi terkini tiap seri" lewat pola anti-join yang sama dengan `AsesmenStore.Dasar`, lalu menghitung histogram kode per field di memori (`PemetaKolom` dipakai lintas namespace `Infrastructure.Asesmen`/`Infrastructure.Monitor`, keduanya satu assembly). Enam tebakan dicatat sebagai `[ASUMSI]` (DUMMY_REGISTRY butir 16): populasi "unit", makna `sejak`, filter geografis yang tidak berlaku untuk sub-agregat asesmen, sumber `layanan`, urutan fallback `asesmenTerkini` #35, dan label kelompok/lingkup buatan sendiri. 1.545 tes lulus (Domain 449, Application 24, Infrastructure 217, Api 855, iam-dummy 43), 0 dilewati; 30 tes baru mencakup matriks izin empat pemantau, batas lingkup WILAYAH/ESELON_I/NASIONAL (404 di luar lingkup termasuk lintas Eselon I dalam provinsi sama), agregat safety check/asesmen/tanggapDarurat/layanan, histogram aspek, RTO gangguan, dan Sieve catatan SDM pada `asesmenTerkini`. Tanpa perubahan skema | `Application/Monitor`, `Infrastructure/Monitor`, `Api/Monitor`, `tests/*/Monitor` |
| P4.5 (putaran 7, **terakhir**) | Domain **Notifikasi** (#43-#45), 3 endpoint — **menutup seluruh 47 endpoint kontrak**. #43 menghitung lima jenis peringatan Fase 1 (`SC_BELUM_DIJAWAB`, `LAYANAN_RTO_MENDEKATI`/`MELANGGAR`, `LAPORAN_MENUNGGU_VERIFIKASI`, `ASESMEN_MENUNGGU_PERSETUJUAN`, `PICU_BELUM`) hanya bila pemanggil memegang permission sumber datanya, dengan Scope permission itu (ACCESS_RULES A8, kini diterapkan) — Fase 2 dan kabar BMKG/MAGMA prototipe sengaja dibuang. ⚖ temuan A8 diselesaikan: `PICU_BELUM` kini di Scope `sigap:broadcast:trigger` pemanggil, bukan seluruh Kemenkeu. #44/#45 menulis `"LanggananPush"` (upsert per `endpoint`) lewat `INotifikasiStore` baru, terpisah dari `IGudangLanggananPush` (`libs/notifikasi`, sisi pengirim push — tetap dummy dalam memori, di luar cakupan putaran ini). 1.563 tes lulus (Domain 449, Application 24, Infrastructure 217, Api 873, iam-dummy 43), 0 dilewati; 18 tes baru mencakup matriks izin tujuh peran, tiap peringatan muncul lalu hilang sesuai keadaan (jawab safety check, verifikasi laporan, setujui asesmen, picu broadcast), Scope RTO lintas Eselon I, Scope `PICU_BELUM` lintas unit, upsert/hapus langganan tanpa membocorkan kepemilikan, dan `p256dh`/`auth` tidak pernah terproyeksi. Tanpa perubahan skema | `Domain/Notifikasi`, `Application/Notifikasi`, `Infrastructure/Notifikasi`, `Api/Notifikasi`, `tests/*/Notifikasi` |
| P4.6 (putaran 1) | **Seluruh 20 halaman Angular** yang menggantikan prototipe, dibangun dari nol (sebelumnya hanya `beranda`/404 tanpa `HttpClient` atau auth). `safety-check/` (7 halaman, #1-#6/#12-#16), `verifikasi-alert/` (4 halaman, #7-#11/#17-#18), `asesmen-bencana/` (4 halaman, #19-#29), `dashboard/` (6 halaman, #30-#35), `notifikasi/` (1 halaman, #43). Fondasi baru: `core/auth` (seam login dev ke Keycloak dummy — DUMMY_REGISTRY butir 51 — plus `HasPermissionDirective`/`IamPermissions` disambungkan sungguhan ke `/me/konteks`), `core/referensi` (opsi-asesmen dipakai data-driven membangun form 5 aspek, bukan field tertulis manual), `HttpInterceptor` Bearer token, `authGuard`. Dua kebutuhan katalog baru dilaporkan sebagai gap, bukan dibangun sendiri: paginasi (sudah tercatat) dan grafik/chart untuk histogram #33 (baru, DUMMY_REGISTRY 2.8) — keduanya ditampilkan sebagai tabel/amplop besar sebagai gantinya. sigap-api ikut disentuh minimal: kebijakan CORS development-only dan `webOrigins` client Keycloak `sigap-uji-lokal` (DUMMY_REGISTRY butir 58), tanpa itu peramban tidak bisa memanggil API sama sekali. **Diverifikasi hidup**: login sungguhan (SATGAS dan KOORDINATOR) di peramban, menu ter-*hasPermission* dengan benar per peran, dashboard Monitor menampilkan data nyata (nol, karena `"User"`/`"Unit"` masih kosong — bagian 5.3), `dotnet test`/`npm run check:web`/`periksa-repo` dan `verifikasi-linux.mjs api web repo` seluruhnya hijau | `apps/sigap-web/src/app/{safety-check,verifikasi-alert,asesmen-bencana,dashboard,notifikasi,core,masuk}`, `apps/sigap-api/src/Sigap.Api/Program.cs`, `infra/keycloak/import/kemenkeu-realm.json` |
| P4.6 (putaran 1, tes) | **`.spec.ts` untuk seluruh 23 komponen baru** (20 halaman + `masuk` + `beranda`), 61 tes lulus. Layanan API dipalsukan lewat `{ provide: XService, useValue: fake }` (bukan `HttpClientTestingModule`) supaya tes tidak bergantung bentuk request HTTP; permission diuji lewat `provideIamPermissions`. Ditemukan dan diperbaiki sambil menulis tes: (a) `fixture.whenStable()` Angular zoneless **tidak** menunggu `Promise` dari layanan tiruan di `ngOnInit` — dibuatkan `flushAsync()` (`shared/testing/flush-async.ts`); (b) tombol jawab pada modal wajib `SafetyCheckSaya` tidak ikut dibungkus `*hasPermission` seperti baris tabelnya — pengguna tanpa `sigap:safety-check:respond` akan melihat tombol yang pasti ditolak API, sudah diperbaiki | `apps/sigap-web/src/app/**/*.spec.ts`, `apps/sigap-web/src/app/shared/testing/flush-async.ts`, `apps/sigap-web/src/app/safety-check/safety-check-saya.ts` |
| Seeder organisasi | `"Unit"` (444 OTK asli sampai Eselon III + 5 demo), `"User"` (10 akun uji dibaca dari realm Keycloak + 12 pegawai pelengkap), `"UserRole"`. Tanpa `passwordHash`/`email`, tanpa perubahan skema. Terverifikasi: 7 tes `bangun.spec.mjs` (termasuk terhadap OTK asli), seeding sungguhan dua kali dengan hash isi tabel identik, tiga penjaga dev-only menolak, dan kueri lingkup: Perwakilan Riau 5 unit, Subkoordinator DJP 81, nasional 449. Belum dijalankan lewat sigap-api/peramban | `infra/organisasi-seed/` |
| Seeder skenario | Gempa Pasaman di KPP Madya Pekanbaru: 1 broadcast, 10 jawaban safety check (8 aman, 2 butuh bantuan, 3 dari 13 pegawai sengaja belum menjawab), 2 versi asesmen berpasangan, 3 layanan kritis, 1 gangguan. Tanpa deklarasi, jadi menunggu Pimpinan. 8 tes `bangun.spec.mjs` (label dicocokkan dengan `OpsiAsesmen.cs`; satu mutasi label tertangkap), seeding dua kali idempoten. **Diverifikasi hidup di peramban sebagai Perwakilan**: rekap Riau/unit/Eselon I = 13/8/2/3 (77%), unit disasar 1/4, asesmen #2 `MENUNGGU_PIMPINAN` (dua versi menjadi satu seri), layanan terganggu sisa RTO 2,5 jam, Aspek 5 membaca versi terkini. Jam tampil sesuai WIB, tanpa pergeseran zona. **Pimpinan juga diverifikasi hidup (24 Sep 2026):** daftar dan detail asesmen (nilai berskala terbaca sebagai label), klik "Setujui Asesmen" -> `POST /asesmen/{id}/persetujuan` 200, status `DISETUJUI`, tanggap darurat `DARURAT`; baris `"DisasterDeclaration"` tertulis dengan `declaredById` = Pimpinan (dari konteks pengguna) dan jejak audit `DIBUAT`. Temuan kecil: beranda Pimpinan menampilkan kartu "Dashboard Monitor" kosong (Pimpinan memang tak punya `sigap:monitor:read`; kartu tanpa tombol seharusnya ikut disembunyikan). **Dampak persetujuan terbukti di dashboard Perwakilan:** Disetujui 1, Menunggu Pimpinan 0, Unit Tanggap Darurat 1; Asesmen Masuk `DISETUJUI`/`DARURAT`; detail unit (#35) memuat `tanggapDarurat` dan sisa RTO yang terus turun. **Sieve terbukti hidup:** `catatanKondisiPegawai` = `null` bagi Perwakilan padahal terisi di database dan terbaca Pimpinan; catatan Aset/TIK tetap terkirim. Celah seeder `pemicu.peran` = `"?"` (dan `lingkup` jatuh ke `NASIONAL`) **sudah ditutup 24 Sep 2026**: seeder kini menulis baris audit `DIPICU` (`SATGAS|UNIT|<unit>`) yang dibaca `BroadcastStore`; terbukti di respons `/safety-check/rekap`: `peran: SATGAS`, `lingkup: UNIT`. **Pegawai Umum diverifikasi hidup:** modal wajib jawab + tombol "Saya Aman"/"Butuh Bantuan" -> `PUT /safety-check/broadcast/{id}/respons-saya` 200; baris tersimpan dengan `userId`/`unitId` dari konteks pengguna dan `dicatatOlehId` kosong; mengganti jawaban = upsert (tetap satu baris, audit `DIBUAT` lalu `DIUBAH`); Riwayat Saya dan Rekap Unit ikut berubah (Belum 3 -> 2). **Sieve kedua terbukti:** `keterangan` rekap `null` bagi Pegawai (hanya SATGAS/PIMPINAN). Seeder diperbaiki: `keterangan` kini sepaket dengan `dicatatOlehId` = Satgas, karena API hanya menghasilkan keterangan pada jawaban yang dicatatkan Satgas (jawaban sendiri mengosongkannya); tes penjaganya ditambah (9 tes). Seeder belum dijalankan ulang agar keadaan uji tidak terhapus. Kartu menu kosong di beranda (judul tanpa tombol; Pimpinan dan Pegawai) **sudah diperbaiki 24 Sep 2026** dengan CSS `.beranda__kartu:has(> .beranda__isi:empty)` (tanpa API baru ke dummy `@danarakca/iam`), terbukti di peramban sebagai Pegawai: dua kartu tanpa tombol `display: none`, tiga lainnya tampil; 2 tes baru, `check:web` 63 tes hijau. **Subkoordinator diverifikasi hidup:** dashboard lingkup ESELON_I DJP, Unit Disasar / Belum = 1 / 80 (81 unit, cocok dengan kueri SQL), rekap 13/8/2/3; batas lingkup lewat API: KPP Madya dan KPP Pratama Tampan (DJP) 200, KPPN Pekanbaru (DJPb) dan Kanwil Riau (Setjen, provinsi sama tapi Eselon I lain) 404. **Temuan:** 404/401/403/500 dari API tidak ditangani di halaman mana pun, jadi halaman yang mengambil data tampil KOSONG (terbukti di `dashboard-unit`; komentar "ditangani global" di komponen itu keliru, hanya ada `provideBrowserGlobalErrorListeners()` yang mencatat ke konsol); juga `DashboardUnit` membaca `route.snapshot` sekali sehingga berpindah unit lewat parameter rute yang sama tidak memuat ulang. Diajukan sebagai tugas terpisah. **Koordinator diverifikasi hidup:** lingkup NASIONAL, Unit Disasar / Belum = 1 / 448 (449 unit, cocok dengan SQL); KPPN Pekanbaru (DJPb) dan Kanwil Riau (Setjen), yang 404 bagi Subkoordinator, 200 bagi Koordinator. **Seluruh peran inti kini teruji hidup di peramban** (kedelapan peran aktif). **Sekjen diverifikasi hidup (24 Sep 2026):** beranda hanya Daftar Asesmen, Dashboard, dan Notifikasi; dashboard NASIONAL 1 disasar / 448 belum (sama dengan Koordinator); tanpa tombol aksi tulis di dashboard maupun detail asesmen; Sieve `catatanKondisiPegawai` "Tidak tersedia"; pratinjau trigger ditolak API 403 dan UI menampilkan "Anda tidak memiliki izin untuk melakukan aksi ini."; notifikasi hanya peringatan RTO GENTING (tanpa peringatan yang izin sumbernya tidak dipegang, ACCESS_RULES A8). **Admin diverifikasi hidup:** `/me/konteks` `permission: []`, `peran: ["ADMIN"]`, lingkup cadangan `SELF`; kelima kartu beranda tersembunyi tanpa satu pun tautan; sembilan halaman bisnis dibuka langsung lewat URL (dashboard, daftar asesmen, notifikasi, rekap, laporan saya, verifikasi alert, riwayat broadcast, layanan kritis, safety check) semuanya ditolak API 403 dengan pesan "Anda tidak memiliki izin untuk membuka halaman ini." tanpa layar kosong. **Temuan kecil UI (bukan keamanan, API sudah menolak) — sudah diperbaiki 24 Sep 2026:** halaman aksi seperti `trigger-safety-check` bisa dibuka langsung oleh peran tanpa izin dan tombol "Pratinjau Sasaran" tidak dibungkus `*hasPermission`. Kini kartu formulir trigger dan tombol picu dibungkus `sigap:broadcast:trigger`, formulir lapor bencana `sigap:laporan:create`, input unggah `sigap:lampiran:upload` (string dicocokkan dengan `iam-policy.sigap.json`); 4 tes baru (109 total), tiga mutasi tertangkap; terbukti di peramban sebagai Sekjen: halaman hanya menampilkan judul, tanpa formulir dan tombol. Audit seluruh tombol aksi lain: semuanya sudah dibungkus. `form-asesmen` juga sudah ditutup (24 Sep 2026): seluruh isi formulir dibungkus izin dinamis (`sigap:asesmen:update` bila seri sudah berjalan, `sigap:asesmen:create` bila belum); 4 tes baru (113 total), dua mutasi tertangkap, lulus di container Linux; terbukti di peramban: Sekjen melihat 0 kolom isian dan 0 tombol di `/asesmen-bencana` (plus pesan 403 pemuatan), Satgas tetap melihat formulir penuh (31 kolom, enam kartu, "Update Asesmen"). `/me/konteks` masih mengisi `nama` dan rincian unit dengan `null` (sudah tercatat di README API). **Satgas diverifikasi hidup (24 Sep 2026):** formulir Update Asesmen terisi dari versi sebelumnya, judul "Memperbarui asesmen versi #3", tombol "Update Asesmen" (koreksi 9); `POST /asesmen/{id}/revisi` 201 -> versi #3 berpasangan dengan `"ChecklistKondisiLapangan"` (`createdAt` sama), gangguan layanan tepat dua (sistem lama tidak digandakan, arsip baru), audit `DIBUAT`. Rekap Unit sebagai Satgas menampilkan keterangan dan pencatat penuh (Pegawai: `null`); "Catatkan" jawaban pegawai -> `PUT /safety-check/broadcast/{id}/respons/{userId}` 200, baris `AMAN` dengan `dicatatOlehId` = Satgas, audit `DICATATKAN`; alasan kosong ditolak API 400 `VALIDASI_GAGAL`. **Bug ditemukan dan diperbaiki:** `KeuTabComponent.active` (dummy `libs/keu-ui-dummy`) properti biasa sehingga di aplikasi zoneless header tab berganti tapi isi panel tidak (tab per kondisi, koreksi 11, tidak berfungsi); kini signal, tes `shared/keu-tabs.spec.ts` gagal pada kode lama dan lulus sesudahnya, terbukti di peramban. **Celah galat saat MENYIMPAN sudah ditutup (24 Sep 2026):** sebelumnya `kirimCatatAsync` dan delapan aksi tulis lain hanya `try/finally` tanpa `catch`, sehingga galat 400/403/409/500 tak tertangani dan pengguna tidak melihat apa pun (modal tetap terbuka tanpa pesan). Kini `PenampungGalat.jalankanAksiAsync` + `pesanAksi` (terpisah dari galat memuat) dipakai di sembilan komponen (detail asesmen, form asesmen, layanan kritis, detail broadcast, rekap, Safety Check Saya, trigger, detail laporan, lapor bencana). Aturan pesan: 400/409/422 menampilkan `detail` dari API apa adanya (ditulis untuk pengguna); status lain memakai pesan tetap (tanpa rincian teknis; 404 tidak dibedakan dari "tidak ada"); 401 mengakhiri sesi dan mengarahkan ke masuk. Pesan tampil dekat aksinya, DI DALAM modal untuk rekap dan modal wajib Safety Check Saya (yang tidak bisa ditutup). Jebakan: `PUT` sukses berbadan kosong (`null`) tak boleh dianggap gagal, jadi aksi tanpa nilai kembali membungkus hasilnya dengan `true`. 27 tes baru (105 total, `check:web` hijau); empat mutasi tertangkap (pesan modal dihapus, penanda `true` diganti, detail 500 dibocorkan, `catch` dilepas). **Terbukti di peramban sebagai Satgas:** alasan kosong -> pesan API tampil di dalam modal, modal tetap terbuka, Simpan aktif; Batal + buka ulang menghapus pesan; alasan sah -> `PUT` 200, modal menutup, keterangan baru tampil; pratinjau trigger dengan provinsi di luar lingkup -> 400 dan pesan "Field berikut tidak berlaku untuk lingkup Anda: provinsi." tampil; konsol tidak lagi memuat `ERROR {...}` tak tertangani dari aksi tulis. Belum tercakup: `langgananAsync`/`hapusLanggananAsync` (belum ada UI Web Push), unggah lampiran asesmen (belum ada UI). Temuan kecil bawaan (bukan akibat perubahan ini): textarea lebih lebar dari modal sehingga ada gulir horizontal; setelah menyimpan, rekap dimuat ulang dan tab kembali ke tab pertama | `infra/skenario-seed/` |
| P4.6 (putaran 2) | **UI langganan Web Push** di halaman Notifikasi. Keputusan pemilik (25 Sep 2026): service worker milik shell (remote hanya `getRegistration` + `pushManager.subscribe`, tidak pernah `register()`), kunci publik VAPID lewat token runtime `VAPID_PUBLIC_KEY` (kosong bawaan), tanpa endpoint baru. `LanggananPush` (status: memeriksa, tidak-didukung, tidak-dikonfigurasi, tanpa-service-worker, ditolak, mati, aktif) menjaga peramban dan server sepakat: langganan dilepas lagi bila #44 gagal, dan #45 dipanggil dulu sebelum melepas di peramban. `kunciAplikasiDari` menolak kunci selain titik P-256 tak terkompresi 65 byte (subscribe hanya menjawab AbortError tanpa sebab). Panel di `NotifikasiPage` memakai `*hasPermission="sigap:notifikasi:subscribe"` dan menjelaskan tiap keadaan, bukan menyembunyikannya. Shell dummy: `public/sw.js` (handler push + klik) dan pendaftarannya. **Tes:** web +32 (145 total, `check:web` hijau); dua mutasi tertangkap (rollback peramban dihapus, urutan hapus dibalik). **Terbukti:** build shell menyertakan `sw.js`, remote memuat tanpa galat. **Belum terbukti:** panel dan alur subscribe di peramban sungguhan: pane Browser menolak mendaftarkan service worker ("unknown error when fetching the script", padahal `sw.js` dilayani 200 `text/javascript`) dan izin notifikasinya `denied`, serta login butuh pemilik; pengiriman push nyata menunggu P5.3 dan jawaban BaTII (Lampiran E #13) | `apps/sigap-web/src/app/notifikasi`, `apps/shell-dummy/public/sw.js` |
| P4.6 (putaran 3) | **Penutupan P4.6: paginasi dan grafik**, dummy atas keputusan pemilik (25 Sep 2026). Empat halaman berdaftar (`daftar-asesmen`, `dashboard-asesmen-masuk`, `dashboard-layanan`, `dashboard-safety-check`) sebelumnya **diam-diam hanya menampilkan halaman pertama** (20 atau 50 baris) walau API mengembalikan `total` lebih besar; kini tiap halaman memegang `halaman`/`ukuran`/`total` dari amplop dan memuat halaman yang diminta, dan mengganti filter (status layanan, kelompok rekap) kembali ke halaman 1. `dashboard-aspek` (#33) menampilkan sembilan histogram sebagai diagram batang (`keu-bar-chart`), bukan tabel kode-jumlah. Dua komponen dummy baru di `libs/keu-ui-dummy` (+ `_pagination.scss`, `_bar-chart.scss`). **Tes:** web 145 -> 161; empat tes lama diperbarui (kini dipanggil dengan nomor halaman); tiga mutasi tertangkap (reset filter ke halaman 1 pada layanan dan rekap, rumus lebar batang). **Tes yang sempat lemah:** mock membalas `halaman: 1` apa pun yang diminta sehingga mutan reset-filter lolos; diperbaiki dengan mock yang membalas nomor yang diminta. **Belum:** tampilan diagram dan paginasi belum dilihat di peramban (login butuh pemilik), hanya build dan tes; respons yang datang tidak berurutan (klik cepat Berikutnya dua kali) tidak dijaga, hasil terakhir yang tiba yang tampil | `libs/keu-ui-dummy`, `apps/sigap-web/src/app/{asesmen-bencana,dashboard}`, `apps/sigap-web/src/app/shared` |
| P4.7 (putaran 1) | **Unit test tambahan: batas paginasi dan Scope lintas wilayah**, dua cacat nyata ditemukan dan diperbaiki. **(1) Meluap pada paginasi:** `PermintaanHalaman.Lewati` = `(Halaman - 1) * Ukuran` dalam `int`; `halaman=2147483647&ukuran=100` menghasilkan `-200`, jadi PostgreSQL menolak kueri (500) atau `Skip()` di memori menganggapnya 0 dan halaman pertama tampil sebagai halaman terakhir. Kini dihitung dalam `long` dan dijepit ke `int.MaxValue`. Dengan kode lama 8 dari 9 endpoint koleksi gagal. **(2) Kebocoran keberadaan data di #16:** `AkhiriBroadcast` membaca broadcast tanpa Scope, jadi pemegang `broadcast:close` di unit mana pun mendapat 403 untuk id yang ada dan 404 untuk yang tidak ada. ACCESS_RULES A2 menulis 403 hanya untuk broadcast yang **terlihat**; kini yang di luar lingkup baca dijawab 404 seperti yang tidak ada. Tes lama `Perwakilan_yang_tidak_mencakup_seluruh_sasaran_ditolak_403` memakai provinsi yang sama sekali lain (broadcast tak terlihat), jadi premisnya keliru dan ditulis ulang: kini broadcast dipicu Subkoordinator lintas provinsi sehingga Perwakilan **melihat** tetapi tidak mencakup seluruh sasaran (403 tetap benar), ditambah tes 404 untuk yang tak terlihat. **Tes baru:** Application +5 (Lewati); Api +41 (`BatasPaginasiTests`: 9 endpoint koleksi x ukuran/halaman di luar batas, halaman jauh, nilai bukan bilangan bulat; `LingkupLintasWilayahTests`: detail, daftar, rekap, akhiri, dan Eselon I lain di provinsi yang sama, masing-masing dengan kontrol positif). Total solusi 1.758 tes; tiga mutasi tertangkap (Lewati lama, pemeriksaan lingkup di #16 dihapus); lulus di container Linux. **Belum:** sapuan Scope untuk endpoint selain Broadcast dan Rekap belum ditinjau ulang satu per satu (sebagian besar sudah punya tes lintas unit/wilayah/Eselon I dari putaran P4.5); P4.8 (Dockerfile sementara sigap-api dan sigap-web) belum dikerjakan | `Application/Umum/Halaman.cs`, `Application/Broadcast/AkhiriBroadcast.cs`, `tests/Sigap.Api.Tests/{Paginasi,Broadcast}` |
| P4.8 (putaran 1) | **Dockerfile sementara dan verifikasi container Linux.** `apps/sigap-api/Dockerfile` (SDK 10 -> aspnet 10, `dotnet build -c Release -o`, **bukan `publish`**: publish sengaja digagalkan SIGAP001 selama dummy IAM ter-resolve) dan `apps/sigap-web/Dockerfile` (node 24 `npm ci` + `build:prod` -> nginx statis dengan CORS untuk `remoteEntry.json`), plus `.dockerignore`. **Konteks build = akar repo (snapshot git), bukan folder app** seperti diminta prompt, karena sigap-api merujuk `libs/iam-dummy` dan `libs/notifikasi`, dan sigap-web memakai workspace/lockfile akar, `tsconfig.base.json`, dan `libs/keu-ui-dummy`; dapat dipersempit setelah dummy diganti paket asli. `node scripts/verifikasi-container.mjs [api|web|semua]` (`npm run verifikasi:container`): membangun image dari arsip git (LF, tanpa berkas ter-ignore), menyalakan PostgreSQL sementara berskema, menjalankan API (`/health/live` 200, `/health/ready` 200 = database terjangkau, endpoint bisnis tanpa token 401) dan web (`remoteEntry.json` 200 + JSON sah + CORS, modul yang diekspos tersaji sebagai JavaScript); bila build gagal, mencocokkan log dengan tiga penyebab khas (kapitalisasi, CRLF, backslash) dan menjalankan `periksa-repo`. Pembantu bersama dipindah ke `scripts/lib/wadah.mjs` dan dipakai juga `verifikasi-linux.mjs`. **Terbukti:** run penuh lulus di Linux (build api 582 dtk, web 29 dtk); jalur gagal dicoba dengan impor `./Notifikasi/notifikasi` (kapitalisasi salah): build Linux gagal, skrip menunjuk kategori 1 dan `app.routes.ts:130`. **Cacat ditemukan oleh percobaan itu:** `periksa-repo` **tidak menangkap impor berkapitalisasi salah** (cek #2 hanya mendeteksi dua berkas yang beda huruf), padahal AGENTS.md dan P4.8 menyebutnya kegagalan Linux yang paling khas; kini ada `imporKapitalisasiSalah` (from, `import()`, side-effect, `require`; relatif saja) dengan 4 tes, tanpa false positive di 567 berkas. **Belum terbukti:** diagnosis kategori 2 (CRLF) dan 3 (backslash) hanya berupa pencocokan pola log, belum dipicu dengan kegagalan sungguhan; API dijalankan `Production` dengan `Iam__Authority` palsu (autentikasi sungguhan tidak diuji karena Keycloak tidak ikut), dan tanpa `Notifikasi` dummy karena `#if DEBUG` (Release tetap hidup) | `apps/sigap-{api,web}/Dockerfile`, `apps/sigap-web/nginx.conf`, `scripts/verifikasi-container.mjs`, `scripts/lib/wadah.mjs`, `scripts/periksa-repo.mjs` |
| P4.7 (putaran 2) | **Penyapuan "tidak membocorkan keberadaan data" di seluruh endpoint berparameter id.** Kelas cacat yang ditemukan di #16 (id yang ada dijawab beda dari yang tidak ada bagi pemanggil di luar Scope) kini diuji sistematis: `TandaJawaban.TegaskanSamaAsync` menjalankan probe dengan id yang ada dan id yang tidak ada lalu menegaskan keduanya 404 dan identik (status, tipe konten, kode, judul, rincian; `traceId` sengaja dikecualikan), dengan kontrol positif di sisi asal dan pemeriksaan bahwa data tidak berubah. Cakupan (`tests/Sigap.Api.Tests/Scope`): asesmen (detail, versi, revisi, persetujuan, `terkini?unitId`, selesai tanggap darurat), laporan (detail, verifikasi, unggah lampiran), safety check (jawab, catat untuk broadcast dan untuk pegawai, detail, selesai, rekap, ringkasan), monitor (`unit/{unitId}` untuk Perwakilan provinsi lain, Subkoordinator Eselon I lain, dan Eselon I lain di provinsi yang sama). **Hasil: tidak ada cacat baru**; keempat keluarga sudah benar. Uji mutasi: dengan perbaikan #16 dicabut, penyapuan ini menangkapnya sendiri (`Mengakhiri_dan_membaca_broadcast_unit_lain...` gagal). Total solusi 1.772 tes (Api 959), lulus di container Linux. **Belum:** `GET /lampiran/{id}` (id lampiran butuh berkas tersimpan, sudah punya tes Scope sendiri di `LampiranTests`), penyaring daftar di luar lingkup untuk endpoint Asesmen dan Broadcast (Monitor dan Referensi sudah), dan pembedaan ada/tidak-ada lewat `total` pada daftar (broadcast sengaja menghitung penuh; keputusan desain, bukan cacat). Dengan ini sapuan P4.7 dianggap selesai untuk Scope id; sisa P4.x adalah tinjauan berkala | `tests/Sigap.Api.Tests/Scope` |
| P5.1 (putaran 1) | **Pemicu Safety Check otomatis dari BMKG**, mati bawaan (`Bmkg:Aktif`). Domain: `NamaWilayah` (nama inti harus sama persis; **jenis Kota/Kabupaten dibedakan** bila kedua sisi menyebutnya, beda dari pencocokan gedung). Dummy IAM: `IServiceIdentity` (identitas layanan tanpa peran, lingkup kosong; DUMMY_REGISTRY butir 102). Application: `PicuBroadcastOtomatis` (jendela 180 menit, satu kejadian sekali lewat `sumberKejadian`, tanpa akun layanan tidak ada yang ditulis, `IBroadcastStore` +2 metode, `NaskahBroadcast` +`SumberOtomatis`). Infrastructure: `PenguraiBmkg` (toleran bentuk, tegas terhadap JSON rusak, koordinat tepat dua bagian), `KlienBmkg` (cadangan hasil sah terakhir di memori, **tidak pernah daftar kosong bila BMKG mati**), `PemantauBmkg` (worker, log sekali per kejadian), paket `Microsoft.Extensions.Http` dan `Hosting.Abstractions`. Seeder: akun layanan + `kabkota` unit demo. **Tes:** Domain +39, iam-dummy +6, Api +22 (DB sungguhan: memicu, dedup, jendela, jenis Kota/Kab, nonaktif, tanpa akun, kepemilikan unit, penyebut rekap, audit `SISTEM|BMKG|...`, API `OTOMATIS_BMKG`), Infrastructure +47 (dua respons BMKG ASLI 24 Sep 2026 sebagai fikstur, klien, worker). Total solusi 1.665 tes + 49 library; sembilan mutasi tertangkap (jendela, jenis wilayah, penyebut rekap, kedua-sumber-gagal, pembatalan, BOM, NIP seeder, dan lainnya); lulus di container Linux. **Terbukti dengan BMKG ASLI di host sungguhan:** worker menyala terhadap dev DB, mengambil dua endpoint BMKG, kejadian nyata "II-III Kota Bima, II-III Kabupaten Bima" (ambang diturunkan untuk peragaan) memicu broadcast `otomatis` atas nama `SISTEM-BMKG` untuk dua unit (Kota Bima dan Kab. Bima tidak tertukar), kemunculan kedua dalam putaran yang sama `sudah-dipicu`, 14 kejadian lain `tanpa-sasaran`; data peragaan dibersihkan. **Temuan dari data asli:** (1) rentang "IV-V Luwuk" (M 5,3) sudah dibaca MMI V oleh Domain, tetapi tak menjangkau unit karena "Luwuk" bukan kabupaten/kota; usulan memperluas pencocokan ke akhiran nama unit ("KPPN Luwuk") **belum dikerjakan**, keputusan pemilik; (2) feed memuat kejadian berhari-hari, maka jendela waktu wajib; (3) `tanpa-sasaran` semula diperingatkan tiap putaran, kini sekali per kejadian. **Belum:** cache Redis (PLAYBOOK meminta), peringatan cuaca CAP, BNPB, atribusi BMKG di tampilan, UI untuk melihat broadcast otomatis. Dua tes lama (`PersetujuanTests`: persetujuan serentak dan tanggap darurat pulih) gagal sekali pada run solusi penuh dan lulus pada tiga run berikutnya; diduga sensitif beban, belum diselidiki | `Domain/Integrasi/NamaWilayah.cs`, `Application/Integrasi`, `Infrastructure/Integrasi`, `libs/iam-dummy`, `infra/organisasi-seed`, `tests/*/Integrasi` |
| P5.1 (putaran 2) | **Peringatan ke pemantau untuk wilayah berguncang tanpa unit** (keputusan pemilik 24 Sep 2026, setelah diketahui 9 dari 28 nama wilayah di feed BMKG asli hari itu tidak punya kantor Kemenkeu). Ditemukan dulu: kanal notifikasi dalam-aplikasi **sengaja tidak mengirim apa pun** (peringatan #43 dihitung saat diminta, tabel notifikasi tidak ada), jadi pemberitahuan terkirim tidak akan terlihat. Maka dibuat peringatan #43 terhitung `GEMPA_KUAT_TANPA_KANTOR` dari cadangan hasil BMKG (`ICadanganGempa`), tanpa tabel baru, hanya untuk pemegang `monitor:read` berlingkup nasional (Koordinator, Sekjen; provinsi wilayah tanpa kantor tidak dapat diketahui, jadi Perwakilan/Subkoordinator tidak ditentukan). Penilaian kejadian dipisah menjadi `PenilaiKejadianBmkg` yang dipakai bersama worker dan #43 (refaktor tanpa mengubah perilaku: 22 tes pemicu tetap lulus). **Cacat ditemukan tes:** penguraian waktu longgar menerima `"24 Sep 2026"` sebagai tengah malam **zona waktu server** (hasil bergantung mesin, tanggal tanpa jam tampak segar pada dini hari); kini hanya ISO 8601 dengan offset atau `Z`. Tes: Application +46 (penilai murni, batas jendela inklusif, toleransi jam), Api +8 (DB sungguhan: Koordinator dan Sekjen menerima, peran lain tidak, wilayah berunit tidak, sebagian, di bawah ambang/di luar jendela, hilang sendiri, duplikat dua berkas, pemicu mati); empat mutasi tertangkap. **Belum:** Web Push untuk peringatan ini; jawaban atas pertanyaan usulan memperluas pencocokan ke akhiran nama unit/kantor (`KantorBmn`) dan tautan kantor ke unit (kolom `unitId` kosong di 1.431 baris) | `Application/Integrasi/PenilaiKejadianBmkg.cs`, `Application/Notifikasi/BacaPeringatan.cs`, `tests/*/Integrasi` |
| P5.1 (putaran 3) | **Cache Redis untuk data publik BMKG** (PLAYBOOK P5.1: berawalan `sigap:`, hanya data publik, fallback ke cache terakhir). `CadanganBmkg` dipindah ke berkasnya sendiri dan kini memakai `IDistributedCache` (`Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.0, `InstanceName = "sigap:"`, kunci `bmkg:{sumber}`, kedaluwarsa 7 hari) dengan salinan memori proses sebagai cadangan terakhir; kegagalan atau isi rusak di Redis dicatat sebagai peringatan, tidak dilempar. `ICadanganGempa.Terakhir()` menjadi `TerakhirAsync(ct)` (kanal I/O tidak boleh tampak sinkron). `ConnectionStrings:Redis` kosong = `AddDistributedMemoryCache` (dev tanpa Redis); `appsettings.Development.json` menunjuk `localhost:6379`. **Tes:** Infrastructure +11 (bulat-pergi, instans baru membaca dari cache bersama, kunci dan kedaluwarsa, urutan, kosong, Redis mati saat tulis/baca, isi rusak, pembatalan tidak ditelan); tiga mutasi tertangkap (fallback memori dihapus, kedaluwarsa diubah). **Terbukti dengan Redis sungguhan** (container `sigap-redis-1`): kunci `sigap:bmkg:uji`, TTL 604799 detik, dibaca utuh instans baru; kunci uji dihapus. Total solusi 1.712 tes + 107 library, lulus di container Linux. **Belum:** BNPB dan CAP memakai cache yang sama begitu klien-nya ada | `Infrastructure/Integrasi/CadanganBmkg.cs`, `BmkgServiceCollectionExtensions.cs`, `tests/Sigap.Infrastructure.Tests/Integrasi/CadanganBmkgTests.cs` |
| P5.1 (putaran 4) | **Info bencana terkini #48: peringatan dini cuaca BMKG (CAP) dan rekap BNPB** (keputusan pemilik 27 Sep 2026: endpoint baru, `sigap:referensi:read` tanpa Scope). Permintaan "Dirasakan"/MMI ≥ V/fikstur/Redis/fallback sudah dipenuhi putaran 1-3; tes variasi "Dirasakan" (rentang, nama berspasi, banyak wilayah, kosong/null/rusak) sudah ada di `NamaWilayahTests`. Domain: `PeringatanCuaca` (`MasihBerlaku`), `RekapBencana`. Application: `BacaInfoBencana` (buang peringatan kedaluwarsa, `diperbarui` per bagian = saat sumber terakhir terbaca, gempa memakai yang terlama dari dua sumber, atribusi selalu ikut), `ICadanganInfoBencana`, `ICadanganGempa.TerkiniAsync`. Infrastructure: `SimpananDataPublik` (inti cache Redis+memori yang dulu di `CadanganBmkg`, kini dipakai bersama; 58 tes lama tetap lulus), `KlienCap` (RSS lalu berkas CAP; dipakai ulang bila sudah utuh; tautan di luar `UrlDasarCap` tidak diikuti; RSS-saja bila berkas gagal), `KlienBnpb` (CKAN `resource_show` + `datastore_search`), `PenguraiCap` (XML tanpa DTD, batas 2 juta karakter, waktu wajib berzona, lewati `Cancel`/latihan/non-publik), `PenguraiBnpb` (baris Total dipisah, angka hilang tetap null), `PembatasLajuBmkg` (satu `SlidingWindowRateLimiter` 50/menit untuk semua host BMKG), `PemantauInfoBencana` (per sumber, log terstruktur `Sumber`/`Kapan`; gempa hanya bila `Bmkg:Aktif` mati), respons dibatasi 4 MB. **Tes:** Domain +5, Application +7 (dan 57 total), Infrastructure +60 (fikstur ASLI 27 Sep 2026: RSS 9 peringatan, 2 berkas CAP, 2 respons CKAN), Api +12 (izin 8 peran, 401, kosong, bentuk, kedaluwarsa dibuang, sama untuk semua peran) + penjaga kontrak 47→48. Total solusi **1.856** tes + 107 library, 0 gagal, 0 dilewati; lima mutasi tertangkap (filter kedaluwarsa, pemeriksaan awalan tautan, gempa dibaca ganda, rekap kosong diterima, RSS-saja dianggap utuh); `dotnet format` bersih; `verifikasi-linux api` dan `repo` lulus. **Terbukti dengan sumber ASLI:** API dev dijalankan (InfoBencana aktif, Bmkg mati) mengisi `sigap:bmkg:cap` (8 peringatan, semuanya berkedaluwarsa dari berkas CAP), `sigap:bnpb:rekap` (9 baris), dan kedua kunci gempa, TTL 7 hari, tanpa satu log peringatan; `/api/v1/info-bencana` hidup (401 tanpa token, ada di OpenAPI). Gangguan sungguhan (RSS diarahkan ke URL 404) mencatat `Sumber bmkg-cap gagal diperbarui; info bencana #48 memakai hasil sah terakhir dari …` dan cadangan 8 peringatan tetap utuh. **Belum terbukti:** respons #48 ber-token di peramban (login butuh pemilik). **Belum:** halaman Angular + atribusi di tampilan, MAGMA, API kejadian waktu nyata BNPB (DIBI; data.bnpb.go.id hanya kompilasi tahunan) | `Domain/Integrasi`, `Application/Integrasi/BacaInfoBencana.cs`, `Infrastructure/Integrasi`, `Api/InfoBencana`, `tests/*/Integrasi`, API_CONTRACT #48, PERMISSION_MAP #19 |
| P5.2 | **Object storage lampiran (S3/MinIO)**, plus keputusan pemilik atas pertanyaan terbuka #9 API_CONTRACT: dokumen (PDF/DOCX/XLSX) ditambahkan ke `AturanLampiran` untuk laporan **dan** asesmen (tipe tersimpan `DOKUMEN`, sudah ada di enum `"AttachmentType"` skema); video/audio tetap khusus laporan (UR menyebutnya eksplisit; foto kerusakan asesmen tidak berubah menjadi rekaman). `IPenyimpanLampiran` mendapat implementasi kedua, `PenyimpanLampiranS3`, dipilih lewat `Lampiran:Driver` (`disk` bawaan, `s3` untuk MinIO lokal/BaTII); tidak ada perubahan kode fitur saat berpindah driver — hanya konfigurasi. Bucket dibuat otomatis saat unggahan pertama menemukan `NoSuchBucket` (prototipe mengasumsikan bucket sudah ada; tidak ada langkah setup MinIO terpisah di repo), idempoten terhadap dua replika yang mulai bersamaan. **Tidak pernah ada URL S3/presigned yang diteruskan ke klien** — `"Attachment"."url"` sudah selalu path API sejak sebelum putaran ini (`LampiranStore`), jadi unduhan tetap satu-satunya jalan lewat `GET /lampiran/{id}` (permission + Scope); bucket dev berkebijakan `private`. **Tes:** Domain +4 (tipe/ekstensi dokumen di `AturanLaporanTambahanTests`, pembanding prototipe `LaporanPembandingTests` — `application/pdf` ditambah ke daftar selisih disengaja), Infrastructure +11 (`PenyimpanLampiranS3Tests` terhadap **MinIO sungguhan**: bulat-pergi, kunci tak ada, hapus, biner utuh, pembuatan bucket otomatis dan idempoten, host tak terjangkau — `Skipped` beserta alasan bila MinIO tidak terjangkau seperti `FaktaDatabaseAttribute` PostgreSQL, sehingga **Skipped di container Linux**), Api +7 (tipe dokumen diterima/ditolak, tes lama yang berasumsi PDF ditolak diperbarui). Total solusi 1.878 tes, 0 gagal, 0 dilewati (di Linux: 10 Skipped MinIO, sisanya lulus); tiga mutasi tertangkap (tipe dokumen asesmen, pemetaan `DOKUMEN`, pengabaian "bucket sudah ada"); satu mutasi pada pemeriksaan `NoSuchKey` tidak tertangkap karena berlebih dengan pemeriksaan `StatusCode` di baris yang sama (MinIO selalu mengirim keduanya, bukan celah tes — dicatat, bukan diperbaiki karena redundansi ini disengaja untuk kompatibilitas S3 asli); `dotnet format` bersih; `verifikasi-linux api`+`repo` dan `verifikasi:container` (Production, driver tetap `disk`) lulus. **Terbukti dengan MinIO sungguhan lewat HTTP nyata**: login Keycloak asli, `POST /laporan-bencana/{id}/lampiran` dengan PDF asli tersimpan di MinIO (`mc ls` menunjukkan kunci `tanggal/guid.pdf`), `GET /lampiran/{id}` mengembalikan isi persis dengan header yang benar, tanpa token 401, akses langsung ke objek MinIO tanpa kredensial 403; data uji dibersihkan (baris DB + objek MinIO). **Belum:** enkripsi at-rest, pemindaian virus, isi berkas tidak diperiksa (hanya `Content-Type` klien) — DUMMY_REGISTRY bagian 9 butir 3/4 | `Domain/Lampiran/AturanLampiran.cs`, `Infrastructure/Lampiran/{PenyimpanLampiranS3,LampiranOptions}.cs`, `Infrastructure/Laporan/KamusKodeLaporan.cs`, `tests/*/Lampiran`, API_CONTRACT bagian 9 butir 9 (selesai) |
| P5.3 | **Logika notifikasi broadcast Safety Check.** Ketiga syarat PLAYBOOK sudah terbangun sejak P4.5 putaran 4 (sasaran `DISASAR`/`DILEWATI` + indeks unik `"sasaran_satu_pemegang_aktif"`) dan P3.6/#43 (kanal `dalam-aplikasi` tahan luring, `SC_BELUM_DIJAWAB` dihitung saat diminta), dan sudah diuji di tingkat DB. Yang dikerjakan: (1) **`PemberitahuBroadcast`** (Application) menyatukan kode penerima+kirim yang tadinya disalin di #13 dan `PicuBroadcastOtomatis`, dengan 13 unit test tanpa DB di atas pengirim `libs/notifikasi` yang sungguhan (penerima hanya unit DISASAR, gabungan tanpa duplikat, dua peran beririsan diberi tahu sekali, pemicuan ulang/serentak broadcast yang sama sekali kirim, jenis berbeda tidak saling menelan, seluruh penerima luring tidak menggagalkan broadcast, konfigurasi tanpa kanal tahan luring ditolak); (2) **bug Production**: `ICatatanKiriman`/`IGudangLanggananPush`/`IPengirimWebPush` hanya diisi `AddNotifikasiDummy()` (DEBUG + Development), sehingga di Production `IPengirimNotifikasi` tidak dapat dibuat dan trigger broadcast, lapor, verifikasi, asesmen, serta jawab safety check gagal 500 — health check tetap hijau, dan tes tidak menangkapnya karena host uji memalsukan `IPengirimNotifikasi`. Dibuktikan merah dulu lewat `PerakitanProduksiTests` (host dirakit sebagai Production), lalu diisi `CatatanKirimanPostgres` (`"KirimanPush"`, benturan indeks unik → `false`, baris dilepas dari pelacak), `GudangLanggananPushPostgres` (`"LanggananPush"`, tabel yang sama yang ditulis #44 — dulu pengirim push membaca gudang memori yang berbeda), `PengirimWebPushVapid`; `AddNotifikasiDummy()` dicabut dari Program.cs; (3) **Web Push sungguhan** (keputusan pemilik): paket `Lib.Net.Http.WebPush` 3.3.1, **bukan** `WebPush` yang semula saya usulkan, karena `WebPush` 1.0.13 hanya mengenkripsi `aesgcm` (diukur langsung) sedangkan Safari/iOS hanya menerima `aes128gcm`; `AutoRetryAfter` mati (uji mutasi membuktikan pustaka mengulang tanpa batas pada 429 + `Retry-After`); (4) **SSRF**: #44 kini hanya menerima URL `https` (≤ 2048, kunci ≤ 256), pengirim hanya ke host `Notifikasi:WebPush:HostDiizinkan` tanpa mengikuti pengalihan; (5) `dipakaiPada` diabaikan jejak audit, penghapusan langganan usang tercatat dengan kunci tersamar. **Tes:** Application +13, Infrastructure +29 (DB: kunci baru/kembar, 10 pemanggil serentak tepat satu lolos, benturan tidak meninggalkan baris terlacak, benturan dalam transaksi tidak meracuninya, gudang; pengirim terhadap peladen push tiruan: `aes128gcm`, VAPID, TTL, `Urgency`, 404/410 usang vs 503/429 sementara, 9 endpoint SSRF ditolak tanpa satu permintaan pun), Api +17 (perakitan Production, validasi #44, audit gudang, `BroadcastLuringTests` ujung ke ujung: pegawai yang tidak membuat satu permintaan pun melihat broadcast saat kembali daring sampai ia menjawab, peringatan tidak bergantung pada kiriman, dua peran beririsan → tepat satu peringatan dan satu kiriman per pegawai), libs +12. Total solusi **1.937** tes + 70 libs notifikasi, 0 gagal, 0 dilewati (Linux: 10 Skipped MinIO seperti P5.2); **12 dari 12 mutasi tertangkap** (unit DILEWATI ikut, tanpa kunci idempotensi, kunci tidak per broadcast, benturan dianggap lolos, baris gagal tetap terlacak, pengguna nonaktif ikut, http diizinkan, wildcard tanpa titik, ulang otomatis 429 — setelah tesnya diberi batas waktu 5 detik, #44 terima http, `dipakaiPada` diaudit, port catatan tidak didaftarkan); `dotnet format` sigap-api bersih; `verifikasi-linux api`+`repo` dan `verifikasi:container` lulus. **Terbukti dengan API yang dijalankan sebagai Production** (Postgres/Keycloak lokal, login asli): trigger #13 Gempa → 409 karena unit dipegang broadcast skenario (dedup bekerja), Banjir → 201, baris `"KirimanPush"` `broadcast-dipicu:{id}` tertulis, pegawai unit itu melihat `SC_BELUM_DIJAWAB` untuk broadcast baru; data uji dibersihkan. **Belum:** push nyata ke peramban (menunggu Lampiran E #13 + kunci VAPID, dan pane Browser tidak dapat mendaftarkan service worker — P4.6 putaran 2); retensi `"KirimanPush"`; pengiriman perangkat berurutan (DUMMY_REGISTRY bagian 9 butir 22). **Tidak disentuh:** whitespace `libs/notifikasi/tests/.../Bantuan.cs` sudah tidak lolos `dotnet format` sejak P3.6 (di luar gerbang) | `Application/Broadcast/PemberitahuBroadcast.cs`, `Infrastructure/Notifikasi/*`, `libs/notifikasi/.../OpsiNotifikasi.cs`, `Application/Notifikasi/KelolaLangganan.cs`, `Api/Program.cs`, `tests/*` |
| P3.5 | Seeder 1.431 gedung kantor (Master Aset BMN) dari `data/kantor-bmn.json` prototipe → tabel `"KantorBmn"` Postgres dev. Koordinat digenerate deterministik per provinsi (34 provinsi, bbox kasar) untuk seluruh baris (SIMAN belum kirim satu pun), ditandai kolom baru `isKoordinatDummy` — **perubahan skema disetujui pemilik proyek 18 Sep 2026 sebelum dikerjakan**. Terverifikasi: 7 tes generator, seeding sungguhan 1.431/1.431 baris, idempotensi dibuktikan (jalan 2× → koordinat identik), 3 penjaga dev-only diuji sampai benar-benar menolak. `unitId` sengaja `NULL` (pencocokan ke unit organisasi di luar cakupan) | `infra/kantor-bmn-seed/`, `docker-compose.yml` |

### 5.3 Belum selesai / perlu tindakan pemilik proyek
Sudah beres 18 Sep 2026: remote prototipe dipindah dan di-push; penulis 8 commit sesi itu
(`cf9d727`…`1b1487a`) diganti dari Donny ke `atepagung` dengan isi kode identik; identitas git
repo prototipe dibetulkan; dokumen di repo ini di-commit dan di-push. Branch
`cadangan/sebelum-ganti-penulis` hanya ada di laptop pertama dan boleh dihapus.

1. **Build prototipe belum terverifikasi lulus.** `npm run build` sempat gagal karena galat tipe
   hasil P1.3. Sudah diperbaiki (`1b1487a`) tetapi belum dijalankan ulang. Jalankan
   `npm install` lalu `npm run build` di repo prototipe.
2. **UAT tim SOPB belum memuat koreksi 7** (lihat butir "Remote prototipe dipindah" di bagian 5.1).
   Perlu melibatkan Donny.
3. **Pertanyaan ke BaTII:** Lampiran E PLAYBOOK ditambah API_CONTRACT bagian 9 butir 1–7 (termasuk
   siapa menjalankan migrasi tabel ke-33).
4. **Pertanyaan ke pemilik proses bisnis:** API_CONTRACT bagian 9 butir 8–10 (wewenang mengakhiri
   broadcast & tanggap darurat, jenis/batas lampiran, pemilik butir 1.1).
5. **P4.4 berjalan: aturan Domain sudah diporting (21 Sep 2026); use case menyusul bersama endpoint
   P4.5.** Yang belum diporting dan alasannya:
   - `bmkg.ts`, `magma.ts`, `kabar.ts`: pengambilan/penguraian data luar dan pengiriman kabar →
     klien integrasi Infrastructure (P5.1). `peringatan.ts`: bagian angkanya ada di
     `RekapSafetyCheck`; mesin peringatannya dibangun bersama #43.
   - Label dan kelas tampilan (`labelStatusSafety`, `labelHasilAsesmen`, `aspek-asesmen.ts`,
     `kelasTagKategori`, `GAYA_RTO`, `kehadiran.ts`) → Angular. Pemetaan kode ↔ nilai tersimpan
     opsi asesmen tidak ada di `src/logic/` (ada di halaman formulir) dan dibangun sebagai
     `KamusKode` bersama #21/#38.
   - Tanpa endpoint di kontrak: `validasiCatatanPemulihan` (`"PemulihanLogEntry"`), `aset.ts`
     (menu Status Aset dihapus koreksi 4), `potensiRisiko`/`rupiah`, fitur Fase 2.
   - Empat butir ⚖ di ACCESS_RULES.md perlu jawaban sebelum endpointnya: **A1** prioritas lingkup
     trigger multi-peran (#13), **A5** sumber keanggotaan peran untuk penyebut rekap (#4, #5,
     #30, #31, #35), **A8** peringatan `picu-belum` yang menghitung tanpa Scope, **A11** identitas
     layanan untuk pemicu otomatis BMKG (P5.1).

   Jejak audit sudah dibangun (baris "P4.5 (audit)" di 5.2), sehingga endpoint tulis sudah meninggalkan jejak.

   Yang menunggu sebelum putaran endpoint berikutnya:
   - ~~Tabel `"User"` dan `"Unit"` kosong di database dev~~ — **sudah diisi 24 Sep 2026** oleh
     [infra/organisasi-seed](infra/organisasi-seed/README.md) (idempoten, terbukti lewat hash isi tabel
     identik setelah dijalankan ulang). Unit OTK tidak punya `provinsi`, jadi lingkup WILAYAH hanya
     menjangkau lima unit demo Riau.
   - Keputusan ⚖ di [KANDIDAT_SCOPE_SIEVE.md](KANDIDAT_SCOPE_SIEVE.md) bagian 3, terutama
     **Sieve NIP** (V1) dan **aturan pasangan dua separuh asesmen** (S5), sebaiknya diambil
     sebelum domain Asesmen diporting di P4.4.
   - Catatan: P2.2 sudah selesai — prototipe punya 76 berkas `.tsx` di `src/components`, bukan
     69 seperti di PLAYBOOK.
6. **Hook Git belum terpasang di clone ini.** Husky memasang hook lewat `core.hooksPath` saat
   `npm install` — mengubah konfigurasi git, wewenang pemilik repo, jadi dependensi dipasang
   dengan `--ignore-scripts` dan hook hanya diuji langsung (lint-staged atas berkas yang
   di-stage). Jalankan `npm install` (atau `npx husky`) sekali. Di Linux/macOS, hook perlu bit
   eksekusi: `git update-index --chmod=+x .husky/pre-commit .husky/commit-msg`.
7. **CI GitHub tidak berjalan dan tidak akan dipulihkan (keputusan pemilik, 24 Sep 2026).** Ketiga
   workflow (`sigap-web`, `sigap-api`, `repo`) gagal dalam ~6 detik tanpa runner: "The job was not
   started because your account is locked due to a billing issue" (akun `atepagung`; repo publik
   pun terdampak). Pemilik memutuskan tidak memulihkan billing. Gerbang verifikasi kini
   `node scripts/verifikasi-linux.mjs semua` (pipeline yang sama di container Linux, PostgreSQL
   sementara, snapshot persis dari yang di-commit): lulus untuk `bf5b986` (repo OK; web 105 tes
   + build produksi; api `dotnet format` bersih, 1.563 tes + 101 tes library, build Release,
   `dotnet publish` gagal via `SIGAP001` sesuai aturan dummy #4). Yang hanya ada di GitHub (cache,
   izin, filter path, service container) tetap **belum terbukti**. Folder `.github/workflows/`
   dibiarkan apa adanya; pematokan runner (`ubuntu-latest` -> Ubuntu 26 pada 19 Okt 2026) dan
   branch protection tidak dikerjakan selama CI tidak dipakai.
8. **Angka baris aturan bisnis yang sebenarnya (dihitung 21 Sep 2026):** `src/logic/` prototipe
   berisi **4.284** baris, bukan ~3.700 seperti di PLAYBOOK P4.1. Yang benar-benar diporting ke
   C# di Fase 1 **2.789** baris; 603 baris sengaja tidak diporting — termasuk `wewenang.ts`
   (208) dan `lingkup.ts` (99) yang **sudah menjadi** `iam-policy.sigap.json`, sehingga
   memportingnya sama dengan menulis ulang logika keamanan yang Lampiran A larang; sisanya 892
   baris milik Fase 2. Peta lengkapnya ada di riwayat sesi P4.1 dan diringkas per domain di
   [apps/sigap-api/README.md](apps/sigap-api/README.md).
9. **Aturan dummy #4 sebagian ditegakkan (21 Sep 2026, P4.1):** `dotnet publish` sigap-api
   gagal dengan `SIGAP001` selama masih ada rujukan ber-nama `*Dummy*` — terbukti berhenti di
   `Kemenkeu.Iam.Dummy`. `libs/notifikasi-dummy` bahkan tidak ikut disusun pada konfigurasi
   Release. Sejak P4.3, workflow `sigap-api` menjalankan `dotnet publish` dan **gagal bila publish
   berhasil**, atau gagal bukan karena `SIGAP001` — jadi aturannya dijaga di CI. Yang belum:
   pemeriksa yang menyapu seluruh solusi dan rujukan `package.json` (P6.2).
10. **Node.js tersedia di laptop kedua** (v24.21.0, npx 11.19.0) — berbeda dari laptop pertama.
   Agen bisa menjalankan verifikasi build sendiri di sini.
11. **Asumsi integrasi shell ↔ remote** (DUMMY_REGISTRY.md bagian 3.3, butir 42–51) paling mahal
   kalau meleset. Layak ditanyakan ke BaTII bersamaan dengan Lampiran E #1 (`starter.mfe`) —
   kalau `starter.mfe` sudah membawa semuanya, cukup minta template itu.
12. **Docker Desktop dinyalakan pemilik proyek sendiri**; agen tidak menyalakan atau memperbaiki
   Docker Desktop. Bila muncul dialog galat soal `sailor-ingest.sock` (socket basi di
   `%LOCALAPPDATA%\Docker\run`), **jangan** pilih "Reset to factory defaults" — itu menghapus
   semua image, container, dan volume.
13. Di laptop lain: jalankan `node infra/keycloak/buat-env.mjs` untuk membuat `.env` sendiri —
    berkas itu sengaja tidak ikut git.
14. **Laptop ini punya PostgreSQL 17 native Windows yang juga mendengarkan di port 5432**,
    bentrok dengan Docker. Karena itu `docker-compose.yml` memetakan Postgres ke **port host
    5433** (bukan 5432 standar) — lihat catatan di berkas itu dan di
    `infra/kantor-bmn-seed/README.md`. Semua koneksi dev ke Postgres dari host Windows pakai
    `localhost:5433`, bukan `:5432`. Instalasi native itu **tidak disentuh**; pemilik proyek
    yang memutuskan sesuatu terhadapnya bila diperlukan.

### 5.4 Keputusan yang mengikat sistem baru
- **Skema database hidup di `infra/skema/`** (sejak P4.2, 21 Sep 2026): DDL Prisma apa adanya
  + satu berkas per perubahan yang disetujui. EF Core schema-first, tanpa migrasi EF. Satu
  perbaikan dilakukan di database dev tanpa menunggu persetujuan terpisah, karena arahnya
  **kembali ke** skema yang disepakati, bukan menjauh: kolom waktu `"KantorBmn"` buatan seeder
  P3.5 bertipe `timestamptz` dikembalikan ke `TIMESTAMP(3)` sesuai Prisma, tanpa kehilangan
  baris (1.431/1.431 cocok dengan sumber).
- **Prototipe sengaja TIDAK disamakan** dengan API_CONTRACT (keputusan 18 Sep 2026). Seluruh
  selisih di API_CONTRACT bagian 6 (12 butir) **wajib diterapkan di sigap-api/sigap-web**. Saat
  porting P4.4 ("logika identik"), butir bagian 6 mengalahkan perilaku prototipe.
- Tabel ke-33 `"BroadcastSasaranUnit"` disetujui (lihat aturan mutlak no. 5 di atas).
- Aturan trigger: satu unit dipegang satu broadcast aktif per jenis bencana; trigger berikutnya
  melewati unit itu; sasaran dikunci saat dipicu; unit berdata kosong tidak ikut disasar
  (API_CONTRACT bagian 3.3.1).

### 5.5 Kesepakatan cara kerja dengan agen AI
Ditulis di sini karena memori agen tidak ikut berpindah laptop.
- **Tanyakan dulu bila permintaan bisa dibaca lebih dari satu cara.** Pakai pertanyaan pilihan
  yang terfokus, dengan contoh konkret dari domain dan rekomendasi. Pekerjaan non-desain
  (verifikasi, dokumentasi) langsung dikerjakan.
- **Perubahan struktur tabel database selalu dilaporkan dulu**, tidak langsung dikerjakan.
- Agen **tidak** menambah/mengganti remote, tidak push, dan tidak mengubah konfigurasi git.
  Semua itu dijalankan pemilik proyek. Agen boleh commit bila diminta, dengan identitas
  `atepagung <agungteja64@gmail.com>`.
- Dokumen dan jawaban memakai bahasa Indonesia.
- Periksa dulu apakah Node.js tersedia di shell agen. Di laptop pertama tidak tersedia, jadi
  build dijalankan pemilik proyek dan agen memverifikasi secara statis dengan Python.
