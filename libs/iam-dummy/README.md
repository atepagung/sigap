# iam-dummy

**DUMMY** pengganti `iam.plugin` (.NET 10) — keamanan tiga lapis platform ICS Keuangan. Tidak boleh
naik ke production. Tercatat di [DUMMY_REGISTRY.md](../../DUMMY_REGISTRY.md) bagian 4.

```bash
dotnet test libs/iam-dummy     # 38 tes: kebijakan SIGAP asli, Scope di database, pipeline HTTP
```

## Yang resmi vs yang ditebak

| Resmi (slide arsitektur ICS) | Ditebak di sini |
| --- | --- |
| `[KemenkeuAuthorize("app:resource:action")]` di endpoint C# | namespace `Kemenkeu.Iam`, `ICurrentUserContext`, `DataScope`, `ApplyScope`, `[Sieve]`, `AddKemenkeuIam` |
| Scope difilter di klausa WHERE database | bentuk helper-nya |
| Sieve: field sensitif jadi `null` di respons | penandaannya lewat atribut + kunci |
| Kebijakan IAM disimpan sebagai data | format JSON-nya |

## Cara kode aplikasi (sigap-api, P4) memakainya

Tulis seolah `iam.plugin` asli sudah ada. **Semua baris di bawah ini adalah kontrak** — jangan
menambah pemakaian lain dari library ini.

```csharp
// Program.cs — satu baris registrasi, plus resolver organisasi milik aplikasi
builder.Services.AddKemenkeuIam(builder.Configuration);
builder.Services.AddScoped<IOrganizationResolver, OrganisasiDariTabelUserUnit>();
app.UseAuthentication();
app.UseAuthorization();
```

```jsonc
// appsettings — bagian "Iam"
"Iam": {
  "Authority": "http://localhost:8081/realms/kemenkeu",   // Keycloak lokal (P3.4)
  "Audience": "sigap-api",
  "PolicyFile": "iam-policy.sigap.json",
  "RequireHttpsMetadata": false                            // hanya development
}
```

```csharp
// Endpoint — lapis 1 dan 2
[KemenkeuAuthorize(Izin.LaporanRead)]
public async Task<...> DaftarLaporan(ICurrentUserContext pengguna, SigapDbContext db)
{
    var laporan = db.DisasterAlerts
        .ApplyScope(pengguna.GetScope(Izin.LaporanRead), unit: a => a.UnitId, owner: a => a.PelaporId);
    ...
}

// DTO respons — lapis 3
public sealed class AspekSdm
{
    [Sieve("asesmen.sdm.catatanKondisiPegawai")]
    public string? CatatanKondisiPegawai { get; init; }
}
```

Aturan untuk kode aplikasi:

- **Satu `global using Kemenkeu.Iam;`** di satu berkas. Kalau namespace asli berbeda, cukup satu
  baris itu yang berubah.
- **Permission ditulis sebagai konstanta** (`Izin.LaporanRead`), bukan string berulang. Salah
  ketik tetap gagal keras (500, bukan 403 diam-diam), tapi lebih baik tertangkap compiler.
- **Scope untuk permission yang sama dengan `[KemenkeuAuthorize]` endpoint-nya.** Lingkup
  ditentukan peran yang MEMBERI permission itu, bukan peran terluas pengguna.
- **Dilarang `if (role == ...)`**, dilarang menyaring hasil query di memori, dilarang
  `AsEnumerable()`/`ToList()` sebelum `ApplyScope`.
- **Profil domain** (`SASARAN_SAYA`, `TERSENTUH`, `PEMICU_ATAU_MENCAKUP`, `UNIT_SENDIRI`,
  `IKUT_INDUK`) tidak diterapkan `ApplyScope` — dilewati, sehingga hasilnya menyempit, tidak
  melebar. Susun predikatnya di kode aplikasi dari `DataScope.Grants[i].Area` (unit ID yang sudah
  diresolusi), tetap sebagai ekspresi LINQ yang masuk WHERE.
- **`unitId` dan `userId` untuk penulisan selalu dari `ICurrentUserContext`**, tidak pernah dari
  body permintaan (PERMISSION_MAP bagian 2.4).
- **Jangan menulis DTO ber-`[Sieve]` ke log atau cache.** Sieve bekerja saat serialisasi respons;
  nilai aslinya masih ada di objek.
- `IOrganizationResolver` hanya diimplementasikan, **tidak dipanggil** kode fitur. Itu titik
  sambung dummy, bukan kontrak platform.

## Cara kerjanya (ringkas)

1. `JwtBearer` memvalidasi token SSO (issuer, audience, tanda tangan, masa berlaku).
2. Klaim `groups` → peran → permission, menurut berkas kebijakan. Grup berformat path Keycloak
   (`/sigap-pegawai`) diterima.
3. Klaim `nip` → `IOrganizationResolver` → `UserId`, `UnitId`, provinsi, Eselon I.
4. Lingkup WILAYAH/ESELON_I diresolusi menjadi daftar unit ID. Bila provinsi/Eselon I kosong,
   lingkup **menyempit ke UNIT** (aturan `bilaParameterKosong` di kebijakan), tidak pernah melebar.
5. `ApplyScope` menghasilkan ekspresi yang diterjemahkan EF Core. Di PostgreSQL:
   ```sql
   WHERE l."UnitId" = ANY (@UnitIds) OR l."PelaporId" = ANY (@OwnerIds)   -- peran ganda: OR
   WHERE FALSE                                                           -- tanpa permission
   ```
   Nilai lingkup selalu berupa **parameter**, bukan literal di teks SQL.

Predikat WILAYAH di PERMISSION_MAP ditulis sebagai subquery ke `"Unit"`; di sini diresolusi
lebih dulu menjadi daftar unit lalu `= ANY (@UnitIds)`. Hasilnya sama, dan bentuk ini tetap
berlaku kalau data organisasi ternyata tinggal di database identitas IAM yang terpisah.

## Penukaran

1. Di csproj sigap-api, ganti `ProjectReference` ke `libs/iam-dummy/src/Kemenkeu.Iam.Dummy` dengan
   `PackageReference` iam.plugin asli.
2. Sesuaikan `global using` dan baris `AddKemenkeuIam` bila namanya berbeda.
3. Jalankan seluruh tes; yang gagal menunjukkan asumsi mana yang meleset (P7.1).
4. Hapus `libs/iam-dummy/`.
