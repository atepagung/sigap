using System.Security.Claims;
using System.Security.Cryptography;
using Kemenkeu.Iam.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kemenkeu.Iam.Dummy.Tests;

/// <summary>Organisasi uji: dua provinsi, dua Eselon I, satu unit berdata tidak lengkap.</summary>
internal sealed class FakeOrganization : IOrganizationResolver
{
    public static readonly (string Id, string? Provinsi, string? EselonI)[] Units =
    [
        ("U-PKU", "Riau", "DJP"),
        ("U-DUM", "Riau", "DJP"),
        ("U-PDG", "Sumatera Barat", "DJP"),
        ("U-BC-PKU", "Riau", "DJBC"),
        ("U-TANPA", null, null),
    ];

    private static readonly Dictionary<string, UserOrganization> Users = new()
    {
        ["111"] = Of("usr-budi", "U-PKU"),
        ["222"] = Of("usr-sari", "U-PKU"),
        ["333"] = Of("usr-kepala", "U-PKU"),
        ["444"] = Of("usr-subkoor", "U-PDG"),
        ["555"] = Of("usr-tanpa", "U-TANPA"),
    };

    public Task<UserOrganization?> FindByNipAsync(string nip, CancellationToken cancellationToken) =>
        Task.FromResult(Users.GetValueOrDefault(nip));

    public Task<IReadOnlyCollection<string>> GetUnitIdsInProvinceAsync(string provinsi, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>(Units.Where(u => u.Provinsi == provinsi).Select(u => u.Id).ToList());

    public Task<IReadOnlyCollection<string>> GetUnitIdsInEselonIAsync(string eselonIKey, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<string>>(Units.Where(u => u.EselonI == eselonIKey).Select(u => u.Id).ToList());

    private static UserOrganization Of(string userId, string unitId)
    {
        var unit = Units.Single(u => u.Id == unitId);
        return new UserOrganization(userId, unitId, unit.Provinsi, unit.EselonI);
    }
}

internal static class Fixture
{
    public static readonly string PolicyPath = Path.Combine(AppContext.BaseDirectory, "iam-policy.sigap.json");

    public static readonly IamPolicy Policy = IamPolicy.Load(PolicyPath);

    /// <summary>Konteks pengguna persis seperti yang dibangun dari token yang sudah divalidasi.</summary>
    public static async Task<CurrentUserContext> UserAsync(string nip, params string[] groups)
    {
        var claims = new List<Claim> { new(CurrentUserContext.NipClaim, nip) };
        claims.AddRange(groups.Select(g => new Claim(CurrentUserContext.GroupsClaim, g)));
        var context = new CurrentUserContext(Policy);
        await context.LoadAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, "uji")), new FakeOrganization(), CancellationToken.None);
        return context;
    }
}

/// <summary>Penerbit token uji. Kunci dibuat acak per proses — tidak ada rahasia di repo.</summary>
internal static class TestTokens
{
    public const string Issuer = "https://sso.uji.invalid/realms/kemenkeu";
    public const string Audience = "sigap-api";
    public static readonly SymmetricSecurityKey Key = new(RandomNumberGenerator.GetBytes(32));

    public static string For(string nip, string[] groups, string audience = Audience, DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            NotBefore = (expires ?? now.AddMinutes(15)).AddMinutes(-30),
            IssuedAt = (expires ?? now.AddMinutes(15)).AddMinutes(-30),
            Expires = expires ?? now.AddMinutes(15),
            Claims = new Dictionary<string, object> { ["nip"] = nip, ["groups"] = groups },
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        });
    }
}

public sealed class LaporanUji
{
    public string Id { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string PelaporId { get; set; } = string.Empty;
}

public sealed class UjiDbContext(DbContextOptions<UjiDbContext> options) : DbContext(options)
{
    public DbSet<LaporanUji> Laporan => Set<LaporanUji>();
}
