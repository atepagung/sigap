using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Sigap.Application.Notifikasi;
using Sigap.Domain.Asesmen.Layanan;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure.Notifikasi;

/// <summary>
/// Sisi API dari domain Notifikasi: bahan peringatan #43 (RTO, broadcast aktif) dan langganan Web Push
/// #44/#45. Lingkup diterapkan di klausa WHERE lewat <c>ApplyScope</c>, sama seperti store lain.
/// </summary>
internal sealed class NotifikasiStore(SigapDbContext db) : INotifikasiStore
{
    public async Task<IReadOnlyList<GangguanRtoDto>> GangguanRtoAsync(DataScope lingkup, DateTime sekarang, CancellationToken ct)
    {
        var baris = await (
            from g in db.GangguanLayanan.AsNoTracking()
                .Where(g => !g.Dibatalkan && g.PulihPada == null && (g.Status == StatusGangguan.Terganggu || g.Status == StatusGangguan.BerhentiTotal))
            join l in db.LayananKritis.AsNoTracking().ApplyScope(lingkup, unit: x => x.UnitId) on g.LayananId equals l.Id
            join u in db.Unit.AsNoTracking() on l.UnitId equals u.Id
            select new { l.Id, l.Nama, l.RtoJam, UnitNama = u.Nama, g.Mulai })
            .ToListAsync(ct);

        var hasil = new List<GangguanRtoDto>(baris.Count);
        foreach (var b in baris)
        {
            var r = Rto.Hitung(b.Mulai, b.RtoJam, sekarang);
            if (r.Status == StatusRto.Aman)
            {
                continue;
            }

            hasil.Add(new GangguanRtoDto(b.Id, b.Nama, b.UnitNama, r.Status, r.Label));
        }

        return hasil;
    }

    public Task<bool> AdaBroadcastAktifAsync(DataScope lingkup, CancellationToken ct) =>
        db.BroadcastSasaranUnit.AsNoTracking().ApplyScope(lingkup, unit: s => s.UnitId)
            .AnyAsync(s => s.Status == StatusSasaran.Disasar && s.Aktif && s.Broadcast.SelesaiPada == null, ct);

    public async Task TambahLanggananAsync(
        string userId, string endpoint, string p256dh, string auth, string? peramban, DateTime pada, CancellationToken ct)
    {
        var ada = await db.LanggananPush.SingleOrDefaultAsync(l => l.Endpoint == endpoint, ct);
        if (ada is null)
        {
            db.LanggananPush.Add(new Persistensi.Notifikasi.LanggananPush
            {
                Id = PembuatCuid.Buat(),
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                Peramban = peramban,
                CreatedAt = pada
            });
        }
        else
        {
            // Perangkat yang sama login ulang lewat pengguna lain: berpindah pemilik dan kunci diperbarui.
            ada.UserId = userId;
            ada.P256dh = p256dh;
            ada.Auth = auth;
            ada.Peramban = peramban;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> HapusLanggananAsync(string userId, string endpoint, CancellationToken ct)
    {
        var ada = await db.LanggananPush.SingleOrDefaultAsync(l => l.Endpoint == endpoint && l.UserId == userId, ct);
        if (ada is null)
        {
            return false;
        }

        db.LanggananPush.Remove(ada);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
