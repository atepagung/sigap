using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Application.Referensi;
using Sigap.Application.Umum;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Referensi;

/// <summary>
/// Kueri rujukan atas <c>"Unit"</c>. Scope <c>{unit} = "id"</c> (PERMISSION_MAP bagian 2.2) diterapkan
/// di klausa WHERE; nilai yang dikembalikan hanya dari unit yang terlihat.
/// </summary>
internal sealed class ReferensiStore(SigapDbContext db) : IReferensiStore
{
    public async Task<IReadOnlyList<string>> ProvinsiAsync(DataScope lingkup, CancellationToken ct) =>
        await Terlihat(lingkup)
            .Where(u => u.Provinsi != null)
            .Select(u => u.Provinsi!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> KabupatenKotaAsync(DataScope lingkup, string? provinsi, CancellationToken ct)
    {
        var q = Terlihat(lingkup).Where(u => u.Kabkota != null);
        if (provinsi is not null)
        {
            q = q.Where(u => u.Provinsi == provinsi);
        }

        return await q.Select(u => u.Kabkota!).Distinct().OrderBy(k => k).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Eselon1Dto>> Eselon1Async(DataScope lingkup, CancellationToken ct)
    {
        var kunci = await Terlihat(lingkup)
            .Where(u => u.EselonIKey != null)
            .Select(u => u.EselonIKey!)
            .Distinct()
            .OrderBy(k => k)
            .ToListAsync(ct);

        if (kunci.Count == 0)
        {
            return [];
        }

        // Nama Eselon I = nama unit berjenjang ESELON_I dengan kunci yang sama. Ini hanya nama, bukan
        // data yang dibatasi lingkup, jadi dicari tanpa Scope. Tanpa unit Eselon I di data, kodenya
        // ditampilkan huruf besar.
        var nama = (await db.Unit.AsNoTracking()
                .Where(u => u.Tingkat == TingkatUnit.EselonI && u.EselonIKey != null && kunci.Contains(u.EselonIKey))
                .OrderBy(u => u.Id)
                .Select(u => new { Kunci = u.EselonIKey!, u.Nama })
                .ToListAsync(ct))
            .GroupBy(x => x.Kunci, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Nama, StringComparer.Ordinal);

        return [.. kunci.Select(k => new Eselon1Dto(k, nama.GetValueOrDefault(k) ?? k.ToUpperInvariant()))];
    }

    public async Task<Halaman<RingkasUnit>> UnitAsync(
        DataScope lingkup, FilterUnit filter, PermintaanHalaman halaman, CancellationToken ct)
    {
        var q = Terlihat(lingkup);

        if (filter.Provinsi is { } provinsi)
        {
            q = q.Where(u => u.Provinsi == provinsi);
        }

        if (filter.KabupatenKota is { } kabkota)
        {
            q = q.Where(u => u.Kabkota == kabkota);
        }

        if (filter.EselonI is { } eselon)
        {
            q = q.Where(u => u.EselonIKey == eselon);
        }

        if (filter.Cari is { } cari)
        {
            // ILIKE dengan karakter khusus di-escape: "%" dan "_" yang diketik pengguna dibaca apa adanya,
            // bukan sebagai pola.
            string pola = "%" + cari.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
            q = q.Where(u => EF.Functions.ILike(u.Nama, pola));
        }

        int total = await q.CountAsync(ct);
        var baris = await q
            .OrderBy(u => u.Nama).ThenBy(u => u.Id)
            .Skip(halaman.Lewati).Take(halaman.Ukuran)
            .Select(u => new RingkasUnit(u.Id, u.Nama, u.Provinsi, u.Kabkota, u.EselonIKey))
            .ToListAsync(ct);

        return new Halaman<RingkasUnit>(baris, halaman.Halaman, halaman.Ukuran, total);
    }

    private IQueryable<Unit> Terlihat(DataScope lingkup) =>
        db.Unit.AsNoTracking().ApplyScope(lingkup, unit: u => u.Id);
}
