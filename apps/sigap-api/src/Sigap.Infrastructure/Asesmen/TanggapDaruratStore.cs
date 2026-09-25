using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Application.Asesmen;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.TanggapDarurat;

namespace Sigap.Infrastructure.Asesmen;

/// <summary>Kueri dan tulis <c>"DisasterDeclaration"</c>. Scope <c>{unit} = "unitId"</c> di klausa WHERE.</summary>
internal sealed class TanggapDaruratStore(SigapDbContext db) : ITanggapDaruratStore
{
    public static string Kode(DeklarasiStatus status) => status switch
    {
        DeklarasiStatus.Normal => "NORMAL",
        DeklarasiStatus.Darurat => "DARURAT",
        DeklarasiStatus.Pulih => "PULIH",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public Task<bool> UnitSedangDaruratAsync(string unitId, CancellationToken ct) =>
        db.DisasterDeclaration.AnyAsync(d => d.UnitId == unitId && d.Status == DeklarasiStatus.Darurat && !d.Dibatalkan, ct);

    public async Task<TanggapDaruratDto> BuatAsync(
        string unitId, string pimpinanId, string jenisBencana, string? kategori, string lokasi, DateTime pada, CancellationToken ct)
    {
        var entitas = new DisasterDeclaration
        {
            Id = PembuatCuid.Buat(),
            UnitId = unitId,
            DeclaredById = pimpinanId,
            JenisBencana = jenisBencana,
            KategoriBencana = kategori,
            Lokasi = lokasi,
            Status = DeklarasiStatus.Darurat,
            DeclaredAt = pada
        };
        db.DisasterDeclaration.Add(entitas);
        await db.SaveChangesAsync(ct);

        return new TanggapDaruratDto(entitas.Id, Kode(entitas.Status), entitas.JenisBencana, entitas.DeclaredAt, null);
    }

    public async Task<TanggapDaruratDto?> BacaAsync(string id, DataScope lingkup, CancellationToken ct)
    {
        var d = await db.DisasterDeclaration.AsNoTracking()
            .ApplyScope(lingkup, unit: x => x.UnitId)
            .Where(x => x.Id == id && !x.Dibatalkan)
            .Select(x => new { x.Id, x.Status, x.JenisBencana, x.DeclaredAt, x.ResolvedAt })
            .SingleOrDefaultAsync(ct);

        return d is null ? null : new TanggapDaruratDto(d.Id, Kode(d.Status), d.JenisBencana, d.DeclaredAt, d.ResolvedAt);
    }

    public Task<string?> UnitDeklarasiAsync(string id, DataScope lingkup, CancellationToken ct) =>
        db.DisasterDeclaration.AsNoTracking()
            .ApplyScope(lingkup, unit: x => x.UnitId)
            .Where(x => x.Id == id && !x.Dibatalkan)
            .Select(x => (string?)x.UnitId)
            .SingleOrDefaultAsync(ct);

    public async Task<bool> SelesaikanAsync(string id, DateTime pada, CancellationToken ct)
    {
        // Entitas dilacak (bukan ExecuteUpdate) supaya interseptor audit mencatat perubahannya.
        var d = await db.DisasterDeclaration.SingleOrDefaultAsync(x => x.Id == id && x.Status == DeklarasiStatus.Darurat && !x.Dibatalkan, ct);
        if (d is null)
        {
            return false;
        }

        d.Status = DeklarasiStatus.Pulih;
        d.ResolvedAt = pada;
        d.DiperbaruiPada = pada;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
