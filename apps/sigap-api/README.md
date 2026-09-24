# sigap-api

Microservice SIGAP, .NET 10, Clean Architecture. Kontraknya di
[API_CONTRACT.md](../../API_CONTRACT.md) dan [PERMISSION_MAP.md](../../PERMISSION_MAP.md).

```bash
docker compose up -d postgres
node infra/skema/terapkan.mjs --yes-development   # sekali, memasang 33 tabel ke database dev
dotnet test apps/sigap-api/sigap-api.slnx          # 1.515 tes
dotnet format apps/sigap-api/sigap-api.slnx --verify-no-changes
dotnet run --project apps/sigap-api/src/Sigap.Api
```

Peramban API (Development saja): `http://localhost:5299/scalar/v1`.

## Layer

```
Api → Infrastructure → Application → Domain
```

| Proyek | Isi | Tidak boleh memuat |
| --- | --- | --- |
| `Sigap.Domain` | Entitas, tipe nilai, perhitungan, aturan status | ketergantungan apa pun |
| `Sigap.Application` | Use case, DTO, port. Lapis 2 (Scope) dan penanda lapis 3 (Sieve) | EF Core, ASP.NET |
| `Sigap.Infrastructure` | EF Core ke 33 tabel (schema-first), kueri ber-Scope, lampiran, klien BMKG/BNPB, pengisi port | aturan bisnis |
| `Sigap.Api` | Controller tipis, perakitan DI, OpenAPI, middleware | aturan bisnis |

## Domain

Setiap layer dikelompokkan per domain dengan nama folder yang sama. Sebelas domain, diturunkan
dari bagian 2 API_CONTRACT:

`SafetyCheck` · `Broadcast` · `Laporan` · `Asesmen` · `TanggapDarurat` · `Monitor` ·
`Referensi` · `Notifikasi` · `Lampiran` · `Auth` · `Integrasi`

**Lima aspek asesmen bukan lima domain.** Satu asesmen tersimpan di dua tabel yang ditulis
dalam satu transaksi (`"DamageAssessment"` + `"ChecklistKondisiLapangan"`), dan SDM/Aset/TIK/
Arsip adalah kelompok kolom pada satu baris yang sama (API_CONTRACT bagian 3.5.3). Tidak ada
endpoint maupun permission per aspek. Karena itu aspek menjadi sub-struktur vertikal di dalam
domain `Asesmen` — masing-masing dengan validator, pemeta, dan tipe nilainya sendiri:

```
Sigap.Domain/Asesmen/Aspek/AspekSdm.cs, AspekAset.cs, AspekTik.cs, AspekArsip.cs, AspekLayanan.cs
Sigap.Application/Asesmen/Aspek/ValidatorSdm.cs, ValidatorAset.cs, …
```

Satu kelas per use case (`KirimAsesmen`, `VerifikasiLaporan`), bukan satu service besar per
domain. Controller hanya memasang atribut izin dan mendelegasikan.

## Keamanan

Tidak ada logika keamanan di sigap-api. Semuanya didelegasikan ke `iam.plugin`
(saat ini [libs/iam-dummy](../../libs/iam-dummy/README.md)), sesuai PLAYBOOK Lampiran A.

| Lapis | Letak | Bentuk |
| --- | --- | --- |
| 1 — Izin | controller | `[KemenkeuAuthorize(Izin.AsesmenApprove)]` |
| 2 — Scope | use case memilih, kueri menerapkan | use case: `pengguna.GetScope(Izin.AsesmenRead)`; kueri di Infrastructure: `.ApplyScope(lingkup, unit: a => a.UnitId)` sebelum materialisasi |
| 3 — Sieve | DTO respons | `[Sieve("asesmen.sdm.catatanKondisiPegawai")]` |

- **Permission ditulis sebagai konstanta** [`Izin`](src/Sigap.Application/Keamanan/Izin.cs).
  Sumber kebenarannya tetap [iam-policy.sigap.json](iam-policy.sigap.json); `IzinTests`
  menggagalkan build kalau kedua daftar berbeda ke arah mana pun.
- **`[KemenkeuAuthorize]` dan `GetScope` wajib memakai permission yang sama.** Lingkup
  ditentukan peran yang memberi permission itu, bukan peran terluas pengguna
  (API_CONTRACT bagian 6 butir 5).
- **Dilarang `if (peran == …)`**, dilarang menyaring hasil query di memori.
- **Pembagian lapis 2:** entity EF tinggal di Infrastructure, jadi use case tidak menyusun
  kuerinya sendiri. Use case yang tahu permission mana yang berlaku, jadi ialah yang memanggil
  `GetScope` dan meneruskan `DataScope` ke port kueri; Infrastructure menerapkannya di klausa
  `WHERE`. Keputusan "lingkup mana" tetap satu tempat, penerapannya di SQL.
- `Sigap.Application` merujuk `Kemenkeu.Iam` langsung dan **tidak** membungkusnya dengan
  abstraksi sendiri. Membungkusnya berarti mencerminkan `DataScope` dengan tipe kita, dan itu
  sama dengan menyalin logika keamanan yang Lampiran A larang. Yang boleh dipakai dari sana
  hanya abstraksinya; perakitannya (`AddKemenkeuIam`, JwtBearer) tetap di `Sigap.Api`.

## Persistensi

Schema-first ke 32 tabel prototipe + dua perubahan yang disetujui, dari DDL di
[infra/skema](../../infra/skema/README.md). EF Core **tidak pernah** membuat maupun mengubah
tabel: tidak ada migrasi EF, tidak ada `EnsureCreated`.

```
Sigap.Infrastructure/Persistensi/
├─ SigapDbContext.cs          33 DbSet, nama sama dengan tabelnya (db.DisasterAlert)
├─ Konvensi/                  WaktuUtc, PembuatCuid, PengisiUpdatedAt
├─ Organisasi/                Unit, User, UserRole — kernel bersama
├─ SafetyCheck/ Broadcast/ Laporan/ Asesmen/ TanggapDarurat/ Lampiran/ Notifikasi/ Referensi/ Audit/
└─ PraBencana/ PascaBencana/  13 tabel Fase 2 — dipetakan, tidak dipakai
```

Tiga perilaku Prisma yang harus ditiru di sisi aplikasi, karena **tidak** menjadi DEFAULT di
database:

| Prisma | Di database | Di sigap-api |
| --- | --- | --- |
| `@default(cuid())` | tanpa DEFAULT | `PembuatCuid` — 25 karakter base36 diawali `c`, sama bentuknya dengan baris lama |
| `@updatedAt` | `NOT NULL` tanpa DEFAULT (13 tabel) | `PengisiUpdatedAt` (interceptor `SaveChanges`) |
| `DateTime` | `TIMESTAMP(3)` tanpa zona waktu, berisi jam UTC | `WaktuUtc` — di aplikasi selalu `DateTimeKind.Utc` |

Enum PostgreSQL dipetakan ke enum C# ber-`[PgName]` (`TingkatUnit.InstansiVertikal` ↔
`'INSTANSI_VERTIKAL'`). Bawaan database yang **bukan** anggota pertama enum diisi eksplisit di
entity (`Unit.Tingkat`, `DisasterDeclaration.Status`) — tanpa itu EF akan mengirim anggota
pertama.

**Dua separuh asesmen.** `"DamageAssessment"` dan `"ChecklistKondisiLapangan"` tidak punya kunci
penghubung. Keduanya wajib ditulis dalam satu transaksi dengan `"createdAt"` dibiarkan diisi
database — `CURRENT_TIMESTAMP` bernilai sama sepanjang satu transaksi (terbukti di PostgreSQL
dev) — lalu dipasangkan lewat `("unitId", "submittedById", "createdAt")`. Lihat
[KANDIDAT_SCOPE_SIEVE.md](../../KANDIDAT_SCOPE_SIEVE.md) S5.

## Konvensi

- **Penamaan:** PascalCase untuk kelas dan method, dengan kosakata domain berbahasa Indonesia
  (`KirimAsesmen`, `HitungRto`, `AspekSdm`) — konsisten dengan `libs/notifikasi` dan
  `iam-policy.sigap.json`. JSON camelCase otomatis (API_CONTRACT bagian 1.3).
- **Nilai berskala memakai kode**, bukan enum yang diserialkan otomatis. `RUSAK_RINGAN`
  tidak sama dengan `RusakRingan`; pemetaannya eksplisit di `Infrastructure/*/KamusKode.cs`
  mengikuti API_CONTRACT bagian 3.5.3.
- **Galat:** aturan bisnis melempar `AturanBisnisException` bermuatan `Kode`; satu penangan di
  `Sigap.Api` menerjemahkannya jadi `application/problem+json`. Aturan bisnis tidak tahu HTTP.
- **Awalan alamat** ditulis sekali di [`Rute.Awalan`](src/Sigap.Api/Rute.cs) — nilainya masih
  asumsi sampai standar gateway ICS diketahui.
- **Analyzer:** `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`,
  `EnforceCodeStyleInBuild`. Aturannya di `.editorconfig`; yang dimatikan diberi alasan.
- **Penamaan field:** `_camelCase` hanya untuk field *instans* privat; `const` dan `static`
  (termasuk `static readonly`) PascalCase. **Aturan penamaan baru ditegakkan penuh oleh
  `dotnet format --verify-no-changes`, bukan oleh `dotnet build`** — build lulus bersih sementara
  `dotnet format` melaporkan pelanggaran. Jalankan keduanya; CI menjalankan keduanya.
- **Akhir baris LF** (`end_of_line = lf`) dan path memakai `Path.Combine()`. Lihat
  [AGENTS.md](../../AGENTS.md) bagian Lintas platform; `npm run periksa:repo` memeriksanya.
- **ContentRoot = folder keluaran** (`Program.cs`), supaya `iam-policy.sigap.json` ditemukan
  dengan cara yang sama saat `dotnet run` maupun setelah publish.

## Penjaga otomatis

| Tes | Menjaga |
| --- | --- |
| `IzinTests` | 23 konstanta `Izin` ↔ 23 permission di `iam-policy.sigap.json`, dua arah |
| `OpenApiTests` | Setiap endpoint terpasang tercatat di API_CONTRACT; semua di bawah awalan versi |
| `PerakitanTests` | Host terakit, health publik, 401 tanpa token, peran dari kebijakan asli |
| `GalatAturanBisnisTests` | Bentuk `problem+json`, kode, dan status per jenis galat |
| `SkemaTests` | Model EF = DDL `infra/skema`: tabel, kolom, tipe, nullability, PK, 52 FK + aksi hapus, indeks, CHECK, label enum — tanpa database |
| `DatabaseTests` | DDL = database dev; setiap tabel terbaca dan tertulis lewat EF (transaksi di-rollback). **Skipped** bila database tidak terjangkau |
| Target `LarangDummyDiPublish` | `dotnet publish` gagal selama masih ada dummy ter-resolve |
| `*PembandingTests` | Aturan bisnis Domain = keluaran fungsi **asli** prototipe pada 334 kasus (lihat di bawah) |
| `IzinLaporanTests` | Lapis 1: tiap endpoint × 8 peran menurut PERMISSION_MAP (tabel ditulis tangan, bukan dihitung dari kebijakan), plus tanpa token → 401 |
| `BacaLaporanTests`, `VerifikasiTests`, `LampiranTests` | Lapis 2 terhadap PostgreSQL sungguhan: data orang lain → 404, kolom Scope tampil sebagai predikat `WHERE` di SQL yang dijalankan, penulisan verifikasi atomik, pembersihan berkas yatim |
| `OpenApiLaporanTests` | Tiap operasi punya ringkasan, uraian, dan daftar respons; galat berbentuk `problem+json` |
| `FiksturTests` | Fikstur dari baseline `1b1487a`, dan setiap fungsi di fikstur punya tes pembanding |

## Aturan bisnis hasil porting (P4.4)

Aturan murni dari `src/logic/` prototipe diporting ke `Sigap.Domain`, satu folder per domain:

| Folder | Isi | Sumber prototipe |
| --- | --- | --- |
| `Referensi/` | taksonomi bencana UU 24/2007, periode baku ADB | `bencana.ts`, `adb.ts` (periode saja) |
| `SafetyCheck/` | bentuk jawaban (klem koordinat), alasan catatan Satgas, angka rekap | `safety-check.ts`, `safety.ts`, `peringatan.ts` |
| `Laporan/`, `Lampiran/` | validasi laporan & verifikasi, batas lampiran | `lapor-verifikasi.ts` |
| `Broadcast/` | kriteria sasaran & kalimat lokasi, validasi trigger | `trigger-sasaran.ts` |
| `Asesmen/`, `Asesmen/Layanan/` | validasi dasar & waktu kejadian, penilaian layanan, RTO | `asesmen-terpadu.ts`, `rto.ts` |
| `Integrasi/` | urai "Dirasakan" BMKG, skala MMI, gedung terdampak, pemilihan gempa pemicu otomatis | `terdampak.ts`, `picu-otomatis.ts` |

- **Tes pembanding memakai fikstur emas.** `tests/pembanding-prototipe/buat-fikstur.mjs` memuat
  berkas `.ts` prototipe apa adanya di Node 24 (tanpa build), menjalankannya atas skenario uji, dan
  merekam masukan/keluaran ke `tests/Sigap.Domain.Tests/Pembanding/fikstur/*.json`. Fikstur ikut
  di-commit; CI tidak butuh prototipe. Jalankan ulang skrip hanya bila skenario ditambah.
- **Selisih disengaja** (API_CONTRACT bagian 6) diputuskan di kelas tesnya, dan tesnya juga
  membuktikan keluaran port memang *berbeda* dari prototipe pada kasus itu.
- **`SemantikJs`** meniru `trim`, `\s`, `Math.round`, `toFixed`, dan `Number()` JavaScript, yang
  berbeda halus dari padanan .NET. Jangan diganti dengan bawaan .NET — `SemantikJsTests`
  menunjukkan bedanya.
- **Kontrol akses tidak diporting.** Logika prototipe yang memutuskan siapa boleh apa dicatat di
  [ACCESS_RULES.md](../../ACCESS_RULES.md) untuk diterjemahkan ke permission/Scope/Sieve.
- Use case lahir bersama endpointnya (P4.5) dan memanggil aturan Domain ini. `HasilValidasi`
  diterjemahkan use case menjadi `AturanBisnisException` lewat `HarusSah()`: 413/415 untuk lampiran,
  selain itu 400 `VALIDASI_GAGAL` beserta `errors` per field.

Penjaga OpenAPI sengaja satu arah: setiap endpoint terpasang wajib ada di kontrak. Arah
sebaliknya belum ditegakkan karena dari 47 endpoint baru sebagian dibangun; memaksakannya
sekarang hanya menghasilkan tes merah yang lama-lama diabaikan.

## Endpoint yang sudah dibangun

Dibangun bertahap per domain (P4.5), setiap putaran satu irisan vertikal: controller → use case →
port → Infrastructure → tes. Putaran 1 = **Laporan + Lampiran + Verifikasi** (7 endpoint), putaran 2 = **Referensi** (6 endpoint), putaran 3 = **Asesmen + Layanan Kritis + Tanggap Darurat** (11 endpoint), putaran 4 = **Broadcast (trigger safety check)** (5 endpoint), putaran 5 = **Safety Check / SOS** (6 endpoint), putaran 6 = **Monitor SC & Sumber Daya** (6 endpoint), putaran 7 = **Notifikasi** (3 endpoint): **47 dari 47** — seluruh endpoint bisnis kontrak sudah dibangun (#46–#47 health check sudah ada sejak P4.1, di luar awalan `/api/v1`).

| # | Endpoint | Permission | Scope | Controller |
| --- | --- | --- | --- | --- |
| 1 | `GET /safety-check/aktif` | `sigap:safety-check:read` | `SASARAN_SAYA` = unit pemanggil | `SafetyCheck/SafetyCheckController` |
| 2 | `PUT /safety-check/broadcast/{id}/respons-saya` | `sigap:safety-check:respond` | idem | `SafetyCheck/SafetyCheckController` |
| 3 | `GET /safety-check/respons-saya` | `sigap:safety-check:read` | SELF | `SafetyCheck/SafetyCheckController` |
| 4 | `GET /safety-check/rekap` | `sigap:safety-check-rekap:read` | UNIT = unit pemanggil, tanpa pilihan unit lain | `SafetyCheck/SafetyCheckController` |
| 5 | `GET /safety-check/rekap/ringkasan` | `sigap:safety-check-rekap:read` | idem | `SafetyCheck/SafetyCheckController` |
| 6 | `PUT /safety-check/broadcast/{id}/respons/{pegawaiId}` | `sigap:safety-check:record` | UNIT atas unit pegawai sasaran | `SafetyCheck/SafetyCheckController` |
| 12 | `GET /safety-check/broadcast/pratinjau` | `sigap:broadcast:trigger` | lihat catatan Broadcast di bawah | `Broadcast/BroadcastController` |
| 13 | `POST /safety-check/broadcast` | `sigap:broadcast:trigger` | idem | `Broadcast/BroadcastController` |
| 14 | `GET /safety-check/broadcast` | `sigap:broadcast:read` | `TERSENTUH(UNIT/WILAYAH/ESELON_I)` atau `NASIONAL`, atau pemicu sendiri | `Broadcast/BroadcastController` |
| 15 | `GET /safety-check/broadcast/{id}` | `sigap:broadcast:read` | idem | `Broadcast/BroadcastController` |
| 16 | `POST /safety-check/broadcast/{id}/selesai` | `sigap:broadcast:close` | `PEMICU_ATAU_MENCAKUP` | `Broadcast/BroadcastController` |
| 7 | `POST /laporan-bencana` | `sigap:laporan:create` | tulis atas nama pemanggil | `Laporan/LaporanController` |
| 8 | `POST /laporan-bencana/{id}/lampiran` | `sigap:lampiran:upload` | scope ∩ **pelapor = saya** | `Laporan/LaporanController` |
| 9 | `GET /laporan-bencana/saya` | `sigap:laporan:read` | scope ∩ pelapor = saya | `Laporan/LaporanController` |
| 10 | `GET /laporan-bencana/{id}` | `sigap:laporan:read` | SELF / UNIT | `Laporan/LaporanController` |
| 11 | `GET /lampiran/{id}` | `sigap:lampiran:read` | `IKUT_INDUK` (laporan → `laporan:read`, asesmen → `asesmen:read`) | `Lampiran/LampiranController` |
| 17 | `GET /laporan-bencana` | `sigap:laporan:read` | SELF / UNIT | `Laporan/LaporanController` |
| 18 | `POST /laporan-bencana/{id}/verifikasi` | `sigap:laporan:verify` | UNIT | `Laporan/LaporanController` |
| 19 | `GET /layanan-kritis` | `sigap:layanan-kritis:read` | UNIT | `Asesmen/LayananKritisController` |
| 20 | `POST /layanan-kritis` | `sigap:layanan-kritis:create` | unit dari identitas, bukan dari body | `Asesmen/LayananKritisController` |
| 21 | `POST /asesmen` | `sigap:asesmen:create` | unit dan pengirim dari identitas | `Asesmen/AsesmenController` |
| 22 | `POST /asesmen/{id}/revisi` | `sigap:asesmen:update` | UNIT | `Asesmen/AsesmenController` |
| 23 | `POST /asesmen/{id}/lampiran` | `sigap:lampiran:upload` | UNIT atas asesmen (Pegawai pemegang izin: **404**, bukan akses) | `Asesmen/AsesmenController` |
| 24 | `GET /asesmen` | `sigap:asesmen:read` | UNIT / WILAYAH / ESELON_I / NASIONAL atas `"unitId"` | `Asesmen/AsesmenController` |
| 25 | `GET /asesmen/terkini` | `sigap:asesmen:read` | idem | `Asesmen/AsesmenController` |
| 26 | `GET /asesmen/{id}` | `sigap:asesmen:read` | idem, **Sieve** `aspek.sdm.catatanKondisiPegawai` dan `.catatanTambahan` | `Asesmen/AsesmenController` |
| 27 | `GET /asesmen/{id}/versi` | `sigap:asesmen:read` | idem | `Asesmen/AsesmenController` |
| 28 | `POST /asesmen/{id}/persetujuan` | `sigap:asesmen:approve` | UNIT | `Asesmen/AsesmenController` |
| 29 | `POST /tanggap-darurat/{id}/selesai` | `sigap:tanggap-darurat:close` | UNIT | `Asesmen/TanggapDaruratController` |
| 37 | `GET /referensi/jenis-bencana` | `sigap:referensi:read` | tanpa Scope | `Referensi/ReferensiController` |
| 38 | `GET /referensi/opsi-asesmen` | `sigap:referensi:read` | tanpa Scope | `Referensi/ReferensiController` |
| 39 | `GET /referensi/provinsi` | `sigap:referensi:read` | UNIT / WILAYAH / ESELON_I / NASIONAL atas `"Unit"."id"` | `Referensi/ReferensiController` |
| 40 | `GET /referensi/kabupaten-kota?provinsi=` | `sigap:referensi:read` | idem | `Referensi/ReferensiController` |
| 41 | `GET /referensi/eselon-1` | `sigap:referensi:read` | idem | `Referensi/ReferensiController` |
| 42 | `GET /referensi/unit` | `sigap:referensi:read` | idem | `Referensi/ReferensiController` |
| 30 | `GET /monitor/ringkasan` | `sigap:monitor:read` | WILAYAH / ESELON_I / NASIONAL atas `"Unit"."id"` (Pimpinan Satker tidak termasuk) | `Monitor/MonitorController` |
| 31 | `GET /monitor/safety-check` | `sigap:monitor:read` | idem | `Monitor/MonitorController` |
| 32 | `GET /monitor/asesmen-masuk` | `sigap:monitor:read` | idem (dirakit lewat `IAsesmenStore` dengan lingkup ini, bukan `sigap:asesmen:read`) | `Monitor/MonitorController` |
| 33 | `GET /monitor/aspek` | `sigap:monitor:read` | idem | `Monitor/MonitorController` |
| 34 | `GET /monitor/layanan` | `sigap:monitor:read` | idem | `Monitor/MonitorController` |
| 35 | `GET /monitor/unit/{unitId}` | `sigap:monitor:read` | idem; unit di luar lingkup → 404. `asesmenTerkini` tunduk **Sieve** #26 (selalu `null` di sini — SATGAS/PIMPINAN tidak memegang `monitor:read`) | `Monitor/MonitorController` |
| 43 | `GET /notifikasi` | `sigap:notifikasi:read` | tanpa profil tunggal — tiap jenis peringatan dihitung hanya bila pemanggil memegang permission sumber datanya, dengan Scope permission itu (ACCESS_RULES A8) | `Notifikasi/NotifikasiController` |
| 44 | `POST /notifikasi/langganan` | `sigap:notifikasi:subscribe` | selalu atas nama pemanggil (`userId` dari identitas), bukan tulis bisnis | `Notifikasi/NotifikasiController` |
| 45 | `DELETE /notifikasi/langganan` | `sigap:notifikasi:subscribe` | idem; selalu 204, endpoint milik pengguna lain diam-diam diabaikan | `Notifikasi/NotifikasiController` |

Pola yang dipakai ulang di putaran berikutnya:

- **Use case** di `Sigap.Application/<Domain>/`, satu kelas per tindakan, dibangun dari `ICurrentUserContext`
  + port + `TimeProvider`. Use case memilih Scope (`GetScope(Izin.X)`), tidak pernah membacanya dari peran.
- **Port** (`ILaporanStore`, `ILampiranStore`, `IPenyimpanLampiran`, `IPenerimaPemberitahuan`) dideklarasikan
  di Application dan diimplementasikan Infrastructure. `DataScope` diteruskan ke port dan diterapkan di
  klausa `WHERE` lewat `ApplyScope`, sebelum baris apa pun dimuat. Kueri baca memakai proyeksi ke kolom
  yang dibutuhkan, jadi `"User"."email"`, `"passwordHash"`, dan `"Attachment"."storageKey"` tidak pernah dipilih.
- **Penyempit bisnis di atas Scope** (mis. "pelapor = saya") dipasang dengan AND, bukan menggantikan Scope.
  Permission yang dipegang dua peran (`lampiran:upload`: pegawai dan Satgas) tidak boleh melebarkan hak.
- **Galat** hanya lewat `AturanBisnisException` (dan turunannya) dan dua penangan terpusat di `Sigap.Api/Umum`:
  `GalatAturanBisnisHandler`, `PermintaanBurukHandler` (body melebihi batas → 413), serta `GalatModel` untuk
  galat pengikatan model. Tidak ada `try/catch` per endpoint.
- **Dokumentasi OpenAPI** dari komentar XML (`<summary>`, `<remarks>`) + `[ProducesResponseType]` dan
  `[ProduksGalat(status)]` untuk respons `problem+json`.

### Asesmen (putaran 3)

- **Satu versi = dua tabel.** `"DamageAssessment"` dan `"ChecklistKondisiLapangan"` tidak punya kunci penghubung.
  Keduanya ditulis dalam **satu** `SaveChanges` dengan `"createdAt"` yang sama persis (`Waktu.Milidetik`: kolomnya
  `TIMESTAMP(3)`), lalu dipasangkan lewat `("unitId", "submittedById", "createdAt")` (KANDIDAT_SCOPE_SIEVE S5,
  asumsi). Separuh tanpa pasangan (data lama prototipe) tampil dengan blok aspek `null`.
- **Seri tidak disimpan.** Urutan versi dan status persetujuan **diturunkan** (`Domain/Asesmen/SeriAsesmen`): seri
  ditentukan broadcast yang memegang unit atau rantai versi yang berjarak paling lama 24 jam, persetujuan dari
  `"DisasterDeclaration"` yang jatuh dalam seri itu. Tidak ada kolom yang dapat menyimpang.
- **Kunci penulisan per unit** (`IUnitKerja`, `pg_advisory_xact_lock` di `UnitKerjaPostgres`): kirim, revisi,
  persetujuan, dan penyelesaian berjalan di dalam transaksi yang memegang kunci unit, dan pemeriksaan kembar /
  versi terkini / seri sudah disetujui / unit sudah darurat dilakukan **di dalam** kunci. Tanpa kunci, dua
  persetujuan serentak membuat dua deklarasi; ditangkap tes serentak dan uji mutasi.
- **Revisi memperlakukan blok sebagai satuan** (API_CONTRACT #22 "kirim hanya aspek yang berubah"): `kondisiBencana`
  atau satu aspek dikirim utuh, yang tidak dikirim disalin dari versi asal. Mengirim satu field saja dijawab 400.
- **Dua catatan SDM** memuat nama dan keadaan medis. Di API mereka di-Sieve; di jejak audit mereka disamarkan
  (`RingkasanJejak.Rahasia`: `CatatanPegawai`, `SdmCatatan`). Tes menangkap kebocoran lewat jejak.
- **Layanan** disimpan sebagai JSONB `{ id, nama, status, rtoJam }` pada `"DamageAssessment"."layananTerdampak"`.
  Setiap layanan kritis unit wajib dinilai; yang `TERGANGGU`/`BERHENTI_TOTAL` memulai `"GangguanLayanan"` bila belum
  ada gangguan berjalan.

### Broadcast — trigger safety check (putaran 4)

- **Lingkup pemicu dipilih dengan `DataScope.Terluas()`, bukan dengan membaca peran** (ACCESS_RULES.md A1). Prioritas
  prototipe (Koordinator → Kepala Perwakilan → Subkoordinator → Satgas) persis sama dengan urutan keluasan profil
  generik (NASIONAL → WILAYAH → ESELON_I → UNIT) pada permission `sigap:broadcast:trigger`, jadi tidak perlu data
  "urutan peran" baru di kebijakan — cukup ekstensi generik `DataScope.Terluas()` di `Kemenkeu.Iam.Dummy` (dokumentasi
  `[ASUMSI]` di sana). `PembangunSasaran` memakai `grant.Profile`/`grant.Area`, tidak pernah nama peran.
- **Satu unit dipegang satu broadcast aktif per jenis bencana** (indeks unik parsial `sasaran_satu_pemegang_aktif`).
  Trigger tidak pernah ditolak karena ini: unit yang sudah dipegang **dilewati**, sisanya tetap disasar. Balapan dua
  trigger ditangkap lewat `catch` pada pelanggaran indeks tadi, dibaca ulang pemegangnya, dicatat `DILEWATI`.
- **Peran, profil lingkup, dan unit pemicu bukan kolom** (`"ActiveBroadcast"` hanya menyimpan `dikirimOlehId`).
  Ketiganya dititipkan di `"JejakPerubahan"."alasan"` pada baris `DIPICU`, format `"{peran}|{profil}|{unitId}"`
  (`IJejakAudit.AksiJejak.Dipicu`, `[ASUMSI]`). `BroadcastStore` adalah satu-satunya tempat fitur membaca
  `"JejakPerubahan"` langsung, karena di situlah nilainya tersimpan.
- **Otorisasi `PEMICU_ATAU_MENCAKUP`** (#16) dibaca langsung dari `DataScope.Grants` (`g.Area.IsNational` atau
  `unitDisasarAktif.All(g.Area.UnitIds.Contains)`), bukan `if (peran == …)` — grant untuk profil domain ini tetap
  membawa `Area` yang sudah diresolusi walau `IsGeneric = false`.
- **Penyelesaian (#16) memakai `IUnitKerja`** (port yang sama dengan Asesmen, kuncinya cuma sebuah string
  `"broadcast:{id}"`) supaya dua permintaan "selesai" bersamaan tidak lolos bersama-sama tanpa saling mengunci.
- **Angka `jumlah*` pada #14/#15 selalu penuh**, dihitung dari `"BroadcastSasaranUnit"`/`"SafetyCheckResponse"`
  langsung, tidak disaring lingkup pembaca — hanya daftar broadcast mana yang boleh dilihat yang disaring Scope.

### Safety Check / SOS (putaran 5)

- **`SASARAN_SAYA` dan `UNIT` (rekap) berarti "unit pemanggil sendiri"** — permission ini tidak punya varian
  WILAYAH/ESELON_I/NASIONAL, jadi #1, #3, #4, #5 mengambil unit langsung dari identitas, bukan lewat
  `GetScope`/`ApplyScope`. Tidak ada query param `unitId` untuk memilih unit lain.
  Sesuai pola `UNIT_SENDIRI` yang sudah dipakai domain Asesmen.
- **`"createdAt"` merangkap "waktu jawab"** — `"SafetyCheckResponse"` tidak punya kolom `updatedAt`, jadi upsert
  (#2, #6) menimpa `createdAt` dengan waktu jawaban terbaru, bukan hanya mengisinya sekali. Diperlukan agar
  "waktu jawab diperbarui meski statusnya sama" (`DITEGASKAN_ULANG`) terpenuhi.
  **[asumsi]** kolom ini boleh dipakai ganda karena tidak ada kandidat lain di skema.
- **Jawaban dari pegawainya sendiri (#2) selalu mengosongkan `dicatatOlehId`/`keterangan`** milik catatan
  Satgas sebelumnya (#6) — kabar terbaru dari pemiliknya sendiri menggantikan, bukan menambah, pencatatan Satgas.
- **Penyebut rekap (#4/#5) adalah LEFT JOIN, bukan filter di memori**: pegawai aktif berperan Pegawai Umum di
  unit itu di-`LEFT JOIN` ke `"SafetyCheckResponse"` bersyarat `broadcastId` yang diminta (A5/A7 —
  jawaban hanya dihitung dari kelompok yang sama dengan penyebutnya, dan hanya jawaban yang terikat
  `broadcastId` ini, bukan jawaban lama untuk broadcast lain). Tanpa baris jawaban berarti `BELUM`.
- **Urutan `BUTUH_BANTUAN` → `BELUM` → `AMAN` → nama** dihitung di SQL lewat ekspresi peringkat pada `OrderBy`,
  bukan diurutkan di memori setelah dimuat — supaya paginasi tetap benar.
- **`RingkasBroadcastDto` (peran/profil/unit pemicu dari jejak audit) dibaca ulang secara terpisah** di
  `SafetyCheckStore`, bukan dipinjam dari `BroadcastStore` — dua store berdiri sendiri, sama seperti
  `AsesmenStore`/`BroadcastStore` masing-masing punya `UnitAsync` sendiri.

### Jebakan yang sudah ditemui

- **Jangan menamai parameter kompleks `[FromQuery]` sama dengan properti modelnya.** Parameter
  `PermintaanHalaman halaman` membuat binder mengira `halaman=2` sebagai awalan model, lalu diam-diam memakai
  bawaan. Pakai `paginasi`. Ditangkap `Paginasi_dijalankan_di_database_dengan_amplop_kontrak`.
- **Jangan pasang `[Produces("application/json")]` di tingkat controller**: ia menimpa `application/problem+json`
  pada galat pengikatan model.
- **`WebApplicationFactory`: nilai yang dibaca `Program.cs` saat merakit layanan (connection string, folder
  lampiran) hanya terlihat lewat `UseSetting`**, bukan `ConfigureAppConfiguration`. Tanpa itu tes memakai
  `appsettings.Development.json`, yaitu database dev.
- **Rincian galat (mis. `belumDinilai` pada `LAYANAN_BELUM_DINILAI`) dikirim di field `detail`** dan menggantikan
  string `detail`: begitulah `GalatAturanBisnisHandler` sejak awal (`AturanBisnisException.Rincian`). Tes membacanya
  di `detail.belumDinilai`. Bila BaTII menetapkan bentuk lain, ubah di satu tempat.
- **Tes yang membuat unit harus membersihkannya.** Database uji dipakai bersama seluruh tes dan tes Referensi
  menghitung unit. `TesAsesmen.DisposeAsync` membuang unit, pengguna, dan turunannya menurut urutan kunci asing.
- **Kolom teks bebas yang sensitif harus masuk `RingkasanJejak.Rahasia`.** Sieve hanya menjaga respons API; tabel
  jejak adalah jalan belakang yang sama-sama terbaca. Ditangkap `Jejak_audit_mencatat_pelaku_dan_tidak_membocorkan_catatan_SDM`.
- **Pengikat model tidak peka huruf besar, tetapi OpenAPI menampilkan `Halaman`/`Ukuran`** (nama properti
  `PermintaanHalaman`), bukan `halaman`/`ukuran` seperti kontrak. Berfungsi; hanya tampilan dokumen.- **FileMode.CreateNew menolak kunci yang sudah ada**; pembersihan setelah gagal hanya boleh menghapus berkas
  yang dibuat panggilan itu sendiri, kalau tidak berkas milik lampiran lain ikut terhapus.

## Jejak audit

API_CONTRACT 1.7: setiap pembuatan, pengubahan, dan **akses** data dicatat terpusat, bukan per endpoint.

| Bagian | Tugas |
| --- | --- |
| `Infrastructure/Audit/PencatatJejakInterceptor` | Interseptor `SaveChanges`: untuk entitas Fase 1 yang `Added/Modified/Deleted`, menambahkan baris `"JejakPerubahan"` **ke unit kerja yang sama**. Data tersimpan berarti jejaknya ada, dan sebaliknya |
| `IJejakAudit` (Application) | Untuk yang tidak terjangkau interseptor: `CatatAsync` (mis. membungkus `ExecuteUpdate`) dan `CatatAksesAsync` (baca data sensitif: keadaan per pegawai, koordinat). `Tandai(aksi, alasan)` mengganti `DIBUAT/DIUBAH/DIHAPUS` dengan nama aksi bisnis untuk penyimpanan berikutnya |
| `RingkasanJejak` | Isi kolom `"ringkasan"`: JSON `{ sebelum, sesudah, oleh: { peran, unitId } }`. Peran dan unit pelaku ikut direkam karena peran hidup di token, tidak di tabel |

Aturan yang perlu diingat saat menulis kode baru:

- **Tanpa identitas pelaku, penyimpanan ditolak** (`InvalidOperationException`) — tidak ada penulisan tanpa jejak.
  Proses latar (pemicu otomatis BMKG) menunggu identitas layanan dari platform (ACCESS_RULES A11).
- **`ExecuteUpdate`/`ExecuteDelete`/`ExecuteSql` melewati interseptor.** Berkas yang memakainya wajib memanggil
  `jejak.CatatAsync` di transaksi yang sama, dan daftar berkas yang memakainya tertutup
  (`ArsitekturAuditTests`) — menambah pemakaian baru adalah keputusan yang harus disadari.
- **Tabel baru di model wajib diputuskan**: diaudit (`PencatatJejakInterceptor.EntitasDiaudit`) atau
  dikecualikan dengan alasan (`ArsitekturAuditTests.TidakDiaudit`). Kalau tidak, tes gagal.
- **Kolom rahasia disamarkan** (`[DISAMARKAN]`), bukan dibuang: `PasswordHash`, `Email`, dan kunci perangkat push.
  Teks lebih dari 2.000 karakter dipotong.
- Jejak tidak mengaudit dirinya dan tidak ada endpoint yang membacanya (KANDIDAT_SCOPE_SIEVE S7: bila kelak ada,
  lingkupnya `IKUT_INDUK` lewat `("entitas", "entitasId")`).
- Di tes, `AplikasiUjiDb.SebagaiAsync(akun, …)` menjalankan kode lapis penyimpanan sebagai akun tertentu tanpa HTTP.

## Menguji endpoint

Tes endpoint berjalan di atas **database PostgreSQL yang dibuat khusus per run** (`sigap_uji_<acak>`),
diisi skema dari `infra/skema/*.sql` yang asli, lalu dibuang — tidak menyentuh `sigap_dev`. Koneksinya:
`SIGAP_DB_UJI_ADMIN`, atau `SIGAP_DB_UJI` (yang sudah disetel CI dan `scripts/verifikasi-linux.mjs`) dengan
database `postgres`, atau PostgreSQL dev di docker compose (`localhost:5433`). Tanpa server yang terjangkau,
tesnya dilaporkan **Skipped** beserta alasannya. Yang dipalsukan hanya penerbit token, pengirim notifikasi
(dicatat, tidak dikirim), dan folder lampiran (sementara); resolver organisasi, kebijakan IAM, dan EF Core
adalah yang asli. Akun uji ada di `Basisdata/DatabaseUji.cs` (`Data`), NIP-nya diawali `8000`.

## Konfigurasi

| Kunci | Keterangan |
| --- | --- |
| `ConnectionStrings:Sigap` | Wajib; proses menolak mulai bila kosong. Production: `ConnectionStrings__Sigap` dari vault |
| `Lampiran:Folder` | Wajib, sama. Folder penyimpanan lampiran; path relatif dihitung dari folder keluaran. Production: `Lampiran__Folder` ke volume yang bertahan antar restart. Driver disk ini padanan `local` di prototipe; object storage MinIO/S3 (P5.2) cukup menjadi implementasi `IPenyimpanLampiran` lain |
| `Bmkg:Aktif` | Pemicu Safety Check otomatis dari BMKG (P5.1). **Mati bawaan** (`false`): pegawai menerima pemberitahuan genting darinya, jadi menyalakannya keputusan penempatan. Production: `Bmkg__Aktif=true` |
| `Bmkg:NipLayanan` | Wajib bila `Aktif`; proses menolak mulai bila kosong. NIP akun layanan (baris `"User"` tanpa peran), bawaan `SISTEM-BMKG`. Lihat SQL di bawah |
| `Bmkg:AmbangMmi` | Kosong atau di luar 1–12 = MMI V (aturan prototipe `AmbangDariTeks`) |
| `Bmkg:JendelaMenit` / `IntervalMenit` | Bawaan 180 / 5. Gempa yang lebih tua dari jendela tidak memicu; batas BMKG 60 permintaan per menit per IP, satu putaran memakai dua |
| `Bmkg:UrlAutogempa`, `UrlGempaDirasakan`, `UrlDasarGambar`, `TimeoutDetik` | Bawaan data terbuka BMKG; diganti hanya untuk peragaan lewat server lokal |

### Pemicu otomatis BMKG (P5.1, putaran 1)

`PemantauBmkg` (worker) memanggil `PicuBroadcastOtomatis` tiap `IntervalMenit` atas nama akun layanan
(`IServiceIdentity`, DUMMY_REGISTRY butir 102): ambil `autogempa.json` + `gempadirasakan.json`, pilih gempa ber-MMI ≥ ambang
yang masih dalam jendela dan belum pernah memicu (`"sumberKejadian"`), cocokkan wilayah berguncang dengan `"Unit"."kabkota"`
(`NamaWilayah`), lalu `store.PicuAsync` yang sama dengan trigger manual (unit yang sudah dipegang broadcast aktif untuk jenis
yang sama dilewati). BMKG mati: hasil sah terakhir dipakai; keduanya mati tanpa cadangan = `BmkgTidakTersediaException`, tidak
pernah daftar kosong yang menyerupai "tidak ada gempa". Asumsi dan keterbatasan: DUMMY_REGISTRY bagian 9 butir 18.

**Akun layanan di production** (baris ini dibuat pemilik setelah OTK dimuat; di dev sudah dibuat `infra/organisasi-seed`).
NIP harus sama dengan `Bmkg:NipLayanan`, tanpa `"UserRole"`:

```sql
INSERT INTO "User" ("id","isDemo","nip","nama","aktif","unitId","updatedAt")
SELECT 'user-layanan-bmkg', false, 'SISTEM-BMKG', 'Sistem BMKG (akun layanan)', true, "id", now()
FROM "Unit" WHERE "kode" = 'kemenkeu'
ON CONFLICT ("nip") DO NOTHING;
```

**Data BMKG** dipakai sesuai ketentuan data terbuka mereka: sumber wajib disebut (teks pesan broadcast menyebut "data BMKG").

## Keadaan sekarang

Yang sudah berjalan: perakitan, keamanan, galat, OpenAPI, `GET /me/konteks` (#36), pemetaan
33 tabel, health check `ready` yang benar-benar memeriksa database (200 / 503), dan **seluruh 47
endpoint kontrak** (Laporan/Lampiran/Verifikasi, Referensi, Asesmen/Layanan Kritis/Tanggap Darurat,
Broadcast/Trigger Safety Check, Safety Check/SOS, Monitor SC & Sumber Daya, Notifikasi; tabel di atas).

- **Data organisasi dibaca dari `"User"`/`"Unit"`**; di database dev keduanya diisi `infra/organisasi-seed`
  (444 unit OTK asli + 5 demo, sepuluh akun uji, akun layanan BMKG). Data organisasi yang kosong tetap membuat lingkup
  kosong (fail-closed). Pengguna ber-`aktif = false` diperlakukan sama dengan tidak dikenal.
- Nama pengguna dan rincian unit di `/me/konteks` masih `null`, dan bentuk `UnitDto`/`PenggunaDto` di
  sana belum sama dengan `RingkasUnit`/`RingkasPengguna` di kontrak (mis. `kabupatenKota`, `eselonI`,
  `jabatan`); dirapikan bersama domain Auth.
- **Jejak audit sudah ada** (lihat "Jejak audit" di bawah). Setiap penulisan ke tabel Fase 1 meninggalkan
  baris di `"JejakPerubahan"` dalam transaksi yang sama.
- **Pengiriman push sungguhan (`IGudangLanggananPush`, `ICatatanKiriman`) masih dummy dalam memori**
  (`libs/notifikasi-dummy`). Putaran Notifikasi (#43-#45) hanya membangun sisi API: `GET /notifikasi`
  menghitung peringatan langsung dari database (bukan lewat kanal), dan `POST`/`DELETE
  /notifikasi/langganan` menulis `"LanggananPush"` lewat `INotifikasiStore` sendiri — belum menyentuh
  jalur pengiriman. Implementasi EF Core sungguhan untuk kedua port itu (P4.2 lama, belum dikerjakan)
  masih perlu dibangun sebelum aturan dummy #4 benar-benar tuntas untuk domain ini.
- **Seluruh 47 endpoint kontrak sudah dibangun.** Yang tersisa bukan endpoint baru, melainkan pekerjaan
  lanjutan yang sudah tercatat: pengisian data organisasi (`"User"`/`"Unit"`), pengiriman push sungguhan
  (poin di atas), dan butir terbuka di ACCESS_RULES/KANDIDAT_SCOPE_SIEVE (V1, V4, A9, A11).
