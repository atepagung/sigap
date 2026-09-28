namespace Kemenkeu.Iam;

/// <summary>
/// [DUMMY — TITIK SAMBUNG] Diimplementasikan aplikasi dengan membaca tabel <c>"User"</c> dan
/// <c>"Unit"</c> yang sudah ada. Tidak ada tabel pengguna baru dan tidak ada login di sini.
///
/// Di platform asli, data organisasi pengguna kemungkinan disediakan IAM sendiri (database
/// identitas). Interface ini hanya ada selama dummy, dan kode fitur tidak boleh memakainya
/// langsung — lihat DUMMY_REGISTRY.md.
/// </summary>
public interface IOrganizationResolver
{
    Task<UserOrganization?> FindByNipAsync(string nip, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetUnitIdsInProvinceAsync(string provinsi, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> GetUnitIdsInEselonIAsync(string eselonIKey, CancellationToken cancellationToken);
}

public sealed record UserOrganization(string UserId, string? UnitId, string? Provinsi, string? EselonIKey);
