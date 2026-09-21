# sigap-api

Microservice SIGAP, .NET 10, Clean Architecture. Kontraknya di
[API_CONTRACT.md](../../API_CONTRACT.md) dan [PERMISSION_MAP.md](../../PERMISSION_MAP.md).

```bash
docker compose up -d postgres
node infra/skema/terapkan.mjs --yes-development   # sekali, memasang 33 tabel ke database dev
dotnet test apps/sigap-api/sigap-api.slnx          # 191 tes
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

Penjaga OpenAPI sengaja satu arah: setiap endpoint terpasang wajib ada di kontrak. Arah
sebaliknya belum ditegakkan karena dari 47 endpoint baru sebagian dibangun; memaksakannya
sekarang hanya menghasilkan tes merah yang lama-lama diabaikan.

## Keadaan sekarang

Yang sudah berjalan: perakitan, keamanan, galat, OpenAPI, `GET /me/konteks` (#36), pemetaan
33 tabel, dan health check `ready` yang benar-benar memeriksa database (200 / 503).

- **Data organisasi kini dibaca dari `"User"`/`"Unit"`** — tetapi kedua tabel itu masih
  **kosong** di database dev, jadi lingkup data akun uji Keycloak tetap kosong (fail-closed)
  sampai datanya diisi dari `otk_bundle.json` prototipe. Pengguna ber-`aktif = false`
  diperlakukan sama dengan tidak dikenal.
- Nama pengguna dan rincian unit di `/me/konteks` masih `null`; diisi saat use case-nya
  membaca tabel (P4.4).
- 46 endpoint lain menyusul di P4.4 dan seterusnya, satu domain per putaran.
