using Kemenkeu.Iam.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kemenkeu.Iam;

/// <summary>
/// Konfigurasi dari bagian <c>"Iam"</c> appsettings. [ASUMSI] nama bagian dan kuncinya.
/// </summary>
public sealed class IamOptions
{
    /// <summary>Issuer OIDC SSO, mis. realm <c>kemenkeu</c> di Keycloak lokal (P3.4).</summary>
    public string? Authority { get; set; }

    public string Audience { get; set; } = string.Empty;

    /// <summary>Path berkas kebijakan IAM (JSON), relatif terhadap content root.</summary>
    public string PolicyFile { get; set; } = string.Empty;

    public bool RequireHttpsMetadata { get; set; } = true;
}

public static class IamServiceCollectionExtensions
{
    /// <summary>
    /// Memasang validasi token SSO dan otorisasi tiga lapis. [ASUMSI] Nama method registrasi —
    /// satu baris di Program.cs yang diganti saat iam.plugin asli tiba.
    /// Aplikasi tetap wajib memanggil <c>UseAuthentication()</c> dan <c>UseAuthorization()</c>,
    /// serta mendaftarkan <see cref="IOrganizationResolver"/>.
    /// </summary>
    public static IServiceCollection AddKemenkeuIam(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IamOptions>().Bind(configuration.GetSection("Iam"));
        services.AddHttpContextAccessor();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<IamOptions>>().Value;
            if (string.IsNullOrWhiteSpace(options.PolicyFile))
            {
                throw new InvalidOperationException("Konfigurasi Iam:PolicyFile wajib diisi.");
            }
            var root = sp.GetRequiredService<IHostEnvironment>().ContentRootPath;
            return IamPolicy.Load(Path.Combine(root, options.PolicyFile));
        });

        services.AddScoped<CurrentUserContext>();
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddScoped<IServiceIdentity, ServiceIdentity>();
        services.AddScoped<IClaimsTransformation, IamClaimsTransformation>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationResultHandler>();

        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<IamOptions>>((jwt, iam) =>
            {
                if (!string.IsNullOrWhiteSpace(iam.Value.Authority))
                {
                    jwt.Authority = iam.Value.Authority;
                }
                jwt.Audience = iam.Value.Audience;
                jwt.RequireHttpsMetadata = iam.Value.RequireHttpsMetadata;
                // Pertahankan nama klaim asli (nip, groups) — jangan dipetakan ke skema Microsoft.
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = CurrentUserContext.NipClaim;
            });

        services.AddOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>()
            .Configure<IHttpContextAccessor, IamPolicy>((json, accessor, policy) =>
                SieveJsonModifier.Attach(json.SerializerOptions, accessor, policy));
        services.AddOptions<Microsoft.AspNetCore.Mvc.JsonOptions>()
            .Configure<IHttpContextAccessor, IamPolicy>((json, accessor, policy) =>
                SieveJsonModifier.Attach(json.JsonSerializerOptions, accessor, policy));

        return services;
    }
}
