using System.Security.Claims;
using System.Security.Cryptography;
using Kemenkeu.Iam;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Sigap.Api.Tests;

/// <summary>Penerbit token uji. Kunci dibuat acak per proses — tidak ada rahasia di repo.</summary>
internal static class TokenUji
{
    public const string Issuer = "https://sso.uji.invalid/realms/kemenkeu";
    public const string Audience = "sigap-api";

    public static SymmetricSecurityKey Kunci { get; } = new(RandomNumberGenerator.GetBytes(32));

    public static string Untuk(string nip, params string[] grup)
    {
        var klaim = new List<Claim> { new("nip", nip) };
        klaim.AddRange(grup.Select(g => new Claim("groups", g)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(klaim),
            Expires = DateTime.UtcNow.AddMinutes(15),
            SigningCredentials = new SigningCredentials(Kunci, SecurityAlgorithms.HmacSha256)
        });
    }
}

/// <summary>
/// Menjalankan sigap-api sungguhan di dalam proses.
///
/// <para>
/// Yang dipalsukan hanya <b>penerbit token</b>: validasi memakai kunci uji, bukan metadata
/// SSO lewat jaringan. Seluruh sisanya berjalan apa adanya — kebijakan IAM yang asli,
/// otorisasi tiga lapis, penangan galat, routing, OpenAPI — sehingga tes ini benar-benar
/// menguji perakitannya, bukan tiruannya.
/// </para>
/// </summary>
public sealed class AplikasiUji : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);

        // Sama dengan Program.cs: kebijakan IAM dicari di folder keluaran.
        builder.UseContentRoot(AppContext.BaseDirectory);

        builder.ConfigureAppConfiguration((_, konfigurasi) => konfigurasi.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                // Kosong supaya JwtBearer tidak menghubungi Keycloak; kunci uji dipasang di bawah.
                ["Iam:Authority"] = string.Empty,
                ["Iam:RequireHttpsMetadata"] = "false",
                ["Iam:Audience"] = TokenUji.Audience,
                ["Iam:PolicyFile"] = "iam-policy.sigap.json",

                // Wajib ada supaya host mau dirakit; tes di sini tidak pernah membukanya.
                ["ConnectionStrings:Sigap"] = "Host=localhost;Port=5433;Database=sigap_dev;Username=sigap_app;Password=sigap_password",

                // Kanal notifikasi paling sederhana yang lolos pemeriksaan awal.
                ["Notifikasi:Kanal:0"] = "dalam-aplikasi",
                ["Notifikasi:Kanal:1"] = null,
                ["Notifikasi:Kanal:2"] = null
            }));

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.TokenValidationParameters.ValidIssuer = TokenUji.Issuer;
                jwt.TokenValidationParameters.IssuerSigningKey = TokenUji.Kunci;
            });

            // Tes perakitan tidak boleh bergantung pada isi database. Pembacaan "User"/"Unit"
            // yang sesungguhnya diuji di Sigap.Infrastructure.Tests terhadap PostgreSQL dev.
            services.RemoveAll<IOrganizationResolver>();
            services.AddScoped<IOrganizationResolver, OrganisasiKosong>();
        });
    }

    public HttpClient Klien(string? token = null)
    {
        var klien = CreateClient();

        if (token is not null)
        {
            klien.DefaultRequestHeaders.Authorization = new("Bearer", token);
        }

        return klien;
    }
}

/// <summary>Resolver tanpa data: setiap pengguna tidak dikenal, lingkupnya kosong (fail-closed).</summary>
internal sealed class OrganisasiKosong : IOrganizationResolver
{
    public Task<UserOrganization?> FindByNipAsync(string nip, CancellationToken cancellationToken) =>
        Task.FromResult<UserOrganization?>(null);

    public Task<IReadOnlyCollection<string>> GetUnitIdsInProvinceAsync(string provinsi, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>([]);

    public Task<IReadOnlyCollection<string>> GetUnitIdsInEselonIAsync(string eselonIKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>([]);
}
