using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Infrastructure.Persistensi;

namespace Sigap.Infrastructure.Keamanan;

/// <summary>
/// Titik sambung <see cref="IOrganizationResolver"/>: data organisasi pengguna dibaca dari
/// tabel <c>"User"</c> dan <c>"Unit"</c> yang sudah ada. Tidak ada tabel pengguna baru dan
/// tidak ada login di sini.
///
/// <para>
/// Pengguna yang tidak ditemukan, atau yang ditandai tidak <c>aktif</c>, dikembalikan
/// <c>null</c> — lingkup datanya kosong (fail-closed). Pengguna nonaktif sengaja diperlakukan
/// sama dengan tidak dikenal: tokennya mungkin masih sah, tetapi ia tidak lagi berhak atas data
/// unit mana pun.
/// </para>
///
/// <para>
/// Unit yang provinsi atau Eselon I-nya kosong dikembalikan apa adanya; iam.plugin yang
/// menyempitkan lingkupnya ke UNIT, bukan kode ini (PERMISSION_MAP bagian 2.2).
/// </para>
/// </summary>
public sealed class OrganisasiDariTabelUserUnit(SigapDbContext db) : IOrganizationResolver
{
    public async Task<UserOrganization?> FindByNipAsync(string nip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nip);

        return await db.User
            .AsNoTracking()
            .Where(u => u.Nip == nip && u.Aktif)
            .Select(u => new UserOrganization(u.Id, u.UnitId, u.Unit.Provinsi, u.Unit.EselonIKey))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetUnitIdsInProvinceAsync(
        string provinsi, CancellationToken cancellationToken) =>
        await db.Unit.AsNoTracking()
            .Where(u => u.Provinsi == provinsi)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<string>> GetUnitIdsInEselonIAsync(
        string eselonIKey, CancellationToken cancellationToken) =>
        await db.Unit.AsNoTracking()
            .Where(u => u.EselonIKey == eselonIKey)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
}
