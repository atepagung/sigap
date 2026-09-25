using Kemenkeu.Iam;
using Sigap.Application.Audit;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Audit;
using Sigap.Infrastructure.Persistensi.Konvensi;

namespace Sigap.Infrastructure.Audit;

/// <summary>Implementasi <see cref="IJejakAudit"/> di atas <c>"JejakPerubahan"</c>.</summary>
internal sealed class JejakAudit(SigapDbContext db, ICurrentUserContext pengguna, KonteksAudit konteks, TimeProvider waktu) : IJejakAudit
{
    public void Tandai(string aksi, string? alasan = null) => konteks.Tandai(aksi, alasan);

    public async Task CatatAsync(
        string entitas,
        string entitasId,
        string aksi,
        IReadOnlyDictionary<string, object?>? sebelum,
        IReadOnlyDictionary<string, object?>? sesudah,
        string? alasan,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitas);
        ArgumentException.ThrowIfNullOrWhiteSpace(entitasId);
        ArgumentException.ThrowIfNullOrWhiteSpace(aksi);

        string oleh = pengguna.UserId ?? throw new InvalidOperationException(
            "Jejak audit tanpa identitas pelaku ditolak (ACCESS_RULES A11 untuk proses latar).");

        db.JejakPerubahan.Add(new JejakPerubahan
        {
            Id = PembuatCuid.Buat(),
            Entitas = entitas,
            EntitasId = entitasId,
            Aksi = aksi,
            Alasan = alasan,
            Ringkasan = RingkasanJejak.Susun(sebelum, sesudah, pengguna.Roles, pengguna.UnitId),
            OlehId = oleh,
            CreatedAt = waktu.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);
    }

    public Task CatatAksesAsync(string entitas, string entitasId, string? alasan, CancellationToken ct) =>
        CatatAsync(entitas, entitasId, AksiJejak.Diakses, null, null, alasan, ct);
}
