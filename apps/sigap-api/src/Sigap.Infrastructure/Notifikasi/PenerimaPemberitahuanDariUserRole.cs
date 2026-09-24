using Microsoft.EntityFrameworkCore;
using Sigap.Application.Notifikasi;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Notifikasi;

/// <summary>
/// <b>[ASUMSI — ACCESS_RULES.md A5, DUMMY_REGISTRY bagian 9]</b> Keanggotaan peran orang lain dibaca
/// dari tabel <c>"UserRole"</c>. Ini satu-satunya tempat sigap-api membaca tabel itu, dan hanya
/// untuk memilih <i>penerima pemberitahuan</i> — <b>bukan</b> untuk memutuskan akses. Akses tetap
/// dari klaim <c>groups</c> token lewat <c>iam.plugin</c>. Bila BaTII menyediakan direktori grup,
/// cukup ganti kelas ini.
/// </summary>
internal sealed class PenerimaPemberitahuanDariUserRole(SigapDbContext db) : IPenerimaPemberitahuan
{
    public Task<IReadOnlyCollection<string>> SatgasUnitAsync(string unitId, CancellationToken ct) =>
        Penerima(unitId, RoleKey.Satgas, ct);

    public Task<IReadOnlyCollection<string>> PimpinanUnitAsync(string unitId, CancellationToken ct) =>
        Penerima(unitId, RoleKey.Pimpinan, ct);

    public Task<IReadOnlyCollection<string>> PegawaiUnitAsync(string unitId, CancellationToken ct) =>
        Penerima(unitId, RoleKey.Pegawai, ct);

    public async Task<IReadOnlyCollection<string>> PemantauUnitAsync(string unitId, CancellationToken ct)
    {
        var unit = await db.Unit.AsNoTracking().Where(u => u.Id == unitId)
            .Select(u => new { u.Provinsi, u.EselonIKey }).SingleOrDefaultAsync(ct);
        if (unit is null)
        {
            return [];
        }

        string? provinsi = unit.Provinsi;
        string? eselon = unit.EselonIKey;

        // Perwakilan dan Subkoordinator hanya bila unit punya provinsi / Eselon I: nilai kosong tidak cocok
        // dengan apa pun (sama dengan lingkup WILAYAH/ESELON_I yang menyempit). Koordinator dan Sekjen nasional.
        return await db.User.AsNoTracking()
            .Where(u => u.Aktif && u.Roles.Any(r =>
                (r.Role == RoleKey.Perwakilan && provinsi != null && u.Unit.Provinsi == provinsi)
                || (r.Role == RoleKey.Subkoordinator && eselon != null && u.Unit.EselonIKey == eselon)
                || r.Role == RoleKey.Koordinator
                || r.Role == RoleKey.Sekjen))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyCollection<string>> Penerima(string unitId, RoleKey peran, CancellationToken ct) =>
        await db.User.AsNoTracking()
            .Where(u => u.Aktif && u.UnitId == unitId && u.Roles.Any(r => r.Role == peran))
            .Select(u => u.Id)
            .ToListAsync(ct);
}
