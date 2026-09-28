using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sigap.Application.Asesmen;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Domain.Referensi;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure.Asesmen;

/// <summary>Kueri dan tulis <c>"LayananKritis"</c>. Scope <c>{unit} = "unitId"</c> di klausa WHERE.</summary>
internal sealed class LayananKritisStore(SigapDbContext db) : ILayananKritisStore
{
    public async Task<IReadOnlyList<LayananKritisDto>> DaftarAsync(DataScope lingkup, CancellationToken ct)
    {
        var baris = await db.LayananKritis.AsNoTracking()
            .ApplyScope(lingkup, unit: l => l.UnitId)
            .Where(l => l.Kritis)
            .OrderBy(l => l.Nama).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Nama, l.RtoJam, DariAdb = l.AdbPada != null })
            .ToListAsync(ct);

        return [.. baris.Select(l => Dto(l.Id, l.Nama, l.RtoJam, l.DariAdb))];
    }

    public async Task<IReadOnlyList<LayananKritisUnit>> KritisUnitAsync(string unitId, CancellationToken ct) =>
        [.. (await db.LayananKritis.AsNoTracking()
            .Where(l => l.UnitId == unitId && l.Kritis)
            .OrderBy(l => l.Nama).ThenBy(l => l.Id)
            .Select(l => new { l.Id, l.Nama, l.RtoJam })
            .ToListAsync(ct)).Select(l => new LayananKritisUnit(l.Id, l.Nama, l.RtoJam))];

    public async Task<LayananKritisDto?> TambahAsync(string unitId, string nama, int rtoJam, CancellationToken ct)
    {
        if (await db.LayananKritis.AnyAsync(l => l.UnitId == unitId && l.Nama == nama, ct))
        {
            return null;
        }

        var entitas = new LayananKritis { Id = PembuatCuid.Buat(), UnitId = unitId, Nama = nama, Kritis = true, RtoJam = rtoJam };
        db.LayananKritis.Add(entitas);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dua pendaftaran serentak melewati pemeriksaan di atas; indeks unik (unitId, nama) yang menahan yang kedua.
            db.ChangeTracker.Clear();
            return null;
        }

        return Dto(entitas.Id, entitas.Nama, entitas.RtoJam, dariAdb: false);
    }

    private static LayananKritisDto Dto(string id, string nama, int rtoJam, bool dariAdb) =>
        new(id, nama, rtoJam, PeriodeAdb.LabelJam(rtoJam), dariAdb ? "ADB" : "MANUAL");
}
