using Microsoft.EntityFrameworkCore;
using Sigap.Application.Asesmen;
using Sigap.Infrastructure.Persistensi;

namespace Sigap.Infrastructure.Asesmen;

/// <summary>
/// <see cref="IUnitKerja"/> dengan <b>advisory lock</b> PostgreSQL berskala transaksi. Kunci diturunkan dari
/// pengenal unit (<c>hashtextextended</c>, 64 bit), dilepas otomatis saat transaksi selesai — commit maupun
/// rollback. Yang dikunci hanya penulis untuk unit yang sama; unit lain dan semua pembaca tidak tertahan.
///
/// <para>
/// Ini <c>ExecuteSql</c> yang <b>tidak menulis data</b> (hanya mengambil kunci), jadi dikecualikan dari
/// aturan "penulisan di luar pelacak wajib mencatat jejak" (<c>ArsitekturAuditTests</c>).
/// </para>
/// </summary>
internal sealed class UnitKerjaPostgres(SigapDbContext db) : IUnitKerja
{
    public async Task<T> DenganKunciUnitAsync<T>(string unitId, Func<CancellationToken, Task<T>> kerja, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);
        ArgumentNullException.ThrowIfNull(kerja);

        await using var transaksi = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"unit-kerja:" + unitId}, 0))", ct);

        var hasil = await kerja(ct);
        await transaksi.CommitAsync(ct);
        return hasil;
    }
}
