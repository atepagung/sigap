using Microsoft.EntityFrameworkCore;
using Sigap.Infrastructure.Persistensi;
using Sigap.Notifikasi.WebPush;

namespace Sigap.Infrastructure.Notifikasi;

/// <summary>
/// <see cref="IGudangLanggananPush"/> di atas tabel <c>"LanggananPush"</c> — tabel yang sama yang ditulis
/// <c>POST</c>/<c>DELETE /notifikasi/langganan</c> (#44/#45, <see cref="NotifikasiStore"/>). Sebelumnya port ini diisi
/// gudang dalam memori, sehingga pengirim push membaca tempat yang berbeda dari tempat langganan disimpan.
///
/// <para>
/// Hanya dipakai kanal <c>web-push</c> untuk mengirim; kunci perangkat (<c>p256dh</c>, <c>auth</c>, <c>endpoint</c>)
/// tidak pernah keluar ke respons mana pun, tidak dicatat log, dan disamarkan di jejak audit
/// (<c>RingkasanJejak.Rahasia</c>).
/// </para>
/// <para>
/// Penghapusan langganan usang memakai entitas terlacak, jadi tercatat di jejak audit seperti #45. Penandaan
/// <c>dipakaiPada</c> juga terlacak, tetapi kolom itu diabaikan audit (pembukuan otomatis per kiriman).
/// </para>
/// </summary>
internal sealed class GudangLanggananPushPostgres(SigapDbContext db) : IGudangLanggananPush
{
    public async Task<IReadOnlyList<LanggananPush>> AmbilUntukAsync(IReadOnlyCollection<string> penggunaIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(penggunaIds);
        if (penggunaIds.Count == 0)
        {
            return [];
        }

        string[] ids = [.. penggunaIds.Distinct(StringComparer.Ordinal)];
        return await db.LanggananPush.AsNoTracking()
            .Where(l => ids.Contains(l.UserId) && l.User.Aktif)
            .OrderBy(l => l.Id)
            .Select(l => new LanggananPush(l.Id, l.UserId, l.Endpoint, l.P256dh, l.Auth))
            .ToListAsync(ct);
    }

    public async Task HapusAsync(IReadOnlyCollection<string> langgananIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(langgananIds);
        if (langgananIds.Count == 0)
        {
            return;
        }

        string[] ids = [.. langgananIds];
        var baris = await db.LanggananPush.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        db.LanggananPush.RemoveRange(baris);
        await db.SaveChangesAsync(ct);
    }

    public async Task TandaiDipakaiAsync(IReadOnlyCollection<string> langgananIds, DateTimeOffset pada, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(langgananIds);
        if (langgananIds.Count == 0)
        {
            return;
        }

        string[] ids = [.. langgananIds];
        var baris = await db.LanggananPush.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        foreach (var l in baris)
        {
            l.DipakaiPada = pada.UtcDateTime;
        }

        await db.SaveChangesAsync(ct);
    }
}
