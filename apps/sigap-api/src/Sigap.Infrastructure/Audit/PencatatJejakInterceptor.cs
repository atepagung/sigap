using Kemenkeu.Iam;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Sigap.Application.Audit;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Audit;
using Sigap.Infrastructure.Persistensi.Broadcast;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.Lampiran;
using Sigap.Infrastructure.Persistensi.Laporan;
using Sigap.Infrastructure.Persistensi.Notifikasi;
using Sigap.Infrastructure.Persistensi.SafetyCheck;
using Sigap.Infrastructure.Persistensi.TanggapDarurat;

namespace Sigap.Infrastructure.Audit;

/// <summary>
/// Mencatat setiap pembuatan, pengubahan, dan penghapusan entitas Fase 1 ke <c>"JejakPerubahan"</c>,
/// terpusat di <c>SaveChanges</c> (API_CONTRACT 1.7) — bukan per endpoint.
///
/// <para>
/// Baris jejak ditambahkan ke <b>unit kerja yang sama</b> sebelum penyimpanan, jadi keduanya berhasil
/// atau gagal bersama. <b>Tanpa identitas pelaku, penyimpanan ditolak</b>: lebih baik gagal keras
/// daripada menulis data tanpa jejak. Penulisan oleh proses latar (pemicu otomatis BMKG) akan
/// membutuhkan identitas layanan dari platform (ACCESS_RULES A11).
/// </para>
/// <para>
/// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> melewati pelacak perubahan dan <b>tidak</b> terjangkau di
/// sini; pemakainya wajib memanggil <see cref="IJejakAudit.CatatAsync"/> (dijaga tes arsitektur).
/// </para>
/// </summary>
internal sealed class PencatatJejakInterceptor(ICurrentUserContext pengguna, KonteksAudit konteks, TimeProvider waktu)
    : SaveChangesInterceptor
{
    /// <summary>
    /// Entitas yang diaudit: tabel yang ditulis endpoint Fase 1. Tabel organisasi ("User", "Unit") dan
    /// data rujukan tidak ditulis sigap-api. "KirimanPush" adalah pembukuan internal, dan
    /// <c>"JejakPerubahan"</c> sendiri tidak pernah diaudit.
    /// </summary>
    private static readonly HashSet<Type> Diaudit =
    [
        typeof(DisasterAlert), typeof(Attachment), typeof(SafetyCheckResponse), typeof(ActiveBroadcast),
        typeof(BroadcastSasaranUnit), typeof(DamageAssessment), typeof(ChecklistKondisiLapangan),
        typeof(LayananKritis), typeof(GangguanLayanan), typeof(DisasterDeclaration), typeof(PemulihanLogEntry),
        typeof(LanggananPush)
    ];

    /// <summary>Kolom yang berubah otomatis dan tidak berarti apa-apa bagi audit.</summary>
    private static readonly HashSet<string> Diabaikan = new(StringComparer.Ordinal) { PengisiUpdatedAt.NamaProperti };

    public static IReadOnlySet<Type> EntitasDiaudit => Diaudit;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Catat(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Catat(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        konteks.Bersihkan();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        konteks.Bersihkan();
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => konteks.Bersihkan();

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        konteks.Bersihkan();
        return Task.CompletedTask;
    }

    private void Catat(DbContext? db)
    {
        if (db is null)
        {
            return;
        }

        var entri = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && Diaudit.Contains(e.Entity.GetType()))
            .ToList();
        if (entri.Count == 0)
        {
            return;
        }

        string oleh = pengguna.UserId ?? throw new InvalidOperationException(
            "Penulisan data tanpa identitas pelaku ditolak: setiap perubahan wajib meninggalkan jejak audit. " +
            "Proses latar membutuhkan identitas layanan (ACCESS_RULES A11).");

        var sekarang = waktu.GetUtcNow().UtcDateTime;
        foreach (var e in entri)
        {
            var (aksi, sebelum, sesudah) = Petakan(e);
            if (aksi is null)
            {
                continue; // hanya kolom otomatis yang berubah
            }

            db.Set<JejakPerubahan>().Add(new JejakPerubahan
            {
                Id = PembuatCuid.Buat(),
                Entitas = e.Metadata.ClrType.Name,
                EntitasId = (string)e.Property("Id").CurrentValue!,
                Aksi = konteks.Aksi ?? aksi,
                Alasan = konteks.Alasan,
                Ringkasan = RingkasanJejak.Susun(sebelum, sesudah, pengguna.Roles, pengguna.UnitId),
                OlehId = oleh,
                CreatedAt = sekarang
            });
        }
    }

    private static (string? Aksi, Dictionary<string, object?>? Sebelum, Dictionary<string, object?>? Sesudah) Petakan(EntityEntry e)
    {
        switch (e.State)
        {
            case EntityState.Added:
                return (AksiJejak.Dibuat, null, Nilai(e, asli: false, hanyaUbah: false));
            case EntityState.Deleted:
                return (AksiJejak.Dihapus, Nilai(e, asli: true, hanyaUbah: false), null);
            default:
                var berubah = e.Properties.Where(p => p.IsModified && !Diabaikan.Contains(p.Metadata.Name)).ToList();
                if (berubah.Count == 0)
                {
                    return (null, null, null);
                }

                return (
                    AksiJejak.Diubah,
                    berubah.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue, StringComparer.Ordinal),
                    berubah.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue, StringComparer.Ordinal));
        }
    }

    private static Dictionary<string, object?> Nilai(EntityEntry e, bool asli, bool hanyaUbah) =>
        e.Properties
            .Where(p => !hanyaUbah || p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => asli ? p.OriginalValue : p.CurrentValue, StringComparer.Ordinal);
}
