using Kemenkeu.Iam;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Sigap.Api;
using Sigap.Api.Umum;
using Sigap.Application;
using Sigap.Infrastructure;
using Sigap.Infrastructure.Persistensi;
using Sigap.Notifikasi;
using Sigap.Notifikasi.Kanal;

#if DEBUG
using Sigap.Notifikasi.Dummy;
#endif

// ContentRoot disetel ke folder keluaran, bukan folder proyek. Berkas kebijakan IAM yang
// asli tinggal di apps/sigap-api/iam-policy.sigap.json — satu-satunya salinannya — dan hanya
// disalin ke folder keluaran saat build. Tanpa baris ini, "dotnet run" mencarinya di folder
// proyek dan tidak menemukannya, sementara hasil publish menemukannya: dua perilaku berbeda
// untuk berkas yang sama. sigap-api tidak menyajikan berkas statis, jadi tidak ada yang hilang.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// ── Keamanan tiga lapis ──────────────────────────────────────────────────────
// Didelegasikan seluruhnya ke iam.plugin (Lampiran A). Tidak ada logika keamanan di
// sigap-api: lapis 1 berupa atribut di controller, lapis 2 dipilih use case (GetScope) dan
// diterapkan kueri Infrastructure di klausa WHERE, lapis 3 berupa penanda [Sieve] pada DTO.
// Kunci penukaran: satu ProjectReference di Sigap.Application, dan satu baris di bawah ini.
builder.Services.AddKemenkeuIam(builder.Configuration);

// ── Notifikasi (P3.6) ────────────────────────────────────────────────────────
// Kanal mana yang berjalan ditentukan "Notifikasi:Kanal" di appsettings, bukan di sini.
builder.Services.AddNotifikasi(builder.Configuration, kanal =>
{
    kanal.Tambah<KanalDalamAplikasi>();
    kanal.Tambah<KanalWebPush>();
#if DEBUG
    kanal.Tambah<KanalLog>();
#endif
});

// ── Layer aplikasi ───────────────────────────────────────────────────────────
builder.Services.AddSigapApplication();
builder.Services.AddSigapInfrastructure(builder.Configuration);

#if DEBUG
if (builder.Environment.IsDevelopment())
{
    // DUMMY, hanya pengembangan. Memakai TryAdd, jadi implementasi sungguhan (P4.2/P5.3)
    // otomatis menang begitu didaftarkan.
    builder.Services.AddNotifikasiDummy();
}
#endif

// ── HTTP ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// Galat berbentuk application/problem+json (API_CONTRACT bagian 1.5).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GalatAturanBisnisHandler>();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck("proses", () => HealthCheckResult.Healthy("Proses hidup."), tags: ["live"])
    // "ready" berarti database terjangkau (API_CONTRACT #47).
    .AddDbContextCheck<SigapDbContext>("database", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// #46–#47: publik, di luar awalan /api/v1.
app.MapHealthChecks(Rute.Hidup, new HealthCheckOptions { Predicate = c => c.Tags.Contains("live") })
    .AllowAnonymous();
app.MapHealthChecks(Rute.Siap, new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })
    .AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    // Dokumen OpenAPI dan peramban API hanya terbuka saat pengembangan; di lingkungan lain
    // permukaan API tidak perlu diumumkan.
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();

/// <summary>Titik masuk yang dapat dirujuk <c>WebApplicationFactory</c> di tes integrasi.</summary>
public partial class Program;
