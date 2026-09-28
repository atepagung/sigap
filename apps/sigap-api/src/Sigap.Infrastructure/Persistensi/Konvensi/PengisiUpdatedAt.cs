using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sigap.Infrastructure.Persistensi.Konvensi;

/// <summary>
/// Mengisi kolom <c>"updatedAt"</c> setiap kali baris dibuat atau diubah.
///
/// <para>
/// Di skema prototipe, <c>@updatedAt</c> adalah kolom <c>NOT NULL</c> <b>tanpa</b> DEFAULT —
/// Prisma mengisinya di sisi aplikasi, termasuk saat baris baru dibuat. Tanpa interceptor ini,
/// setiap INSERT ke 13 tabel bertanda itu gagal melanggar NOT NULL.
/// </para>
/// </summary>
public sealed class PengisiUpdatedAt(TimeProvider waktu) : SaveChangesInterceptor
{
    public const string NamaProperti = "UpdatedAt";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Isi(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Isi(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Isi(DbContext? konteks)
    {
        if (konteks is null)
        {
            return;
        }

        var sekarang = waktu.GetUtcNow().UtcDateTime;

        foreach (var entri in konteks.ChangeTracker.Entries())
        {
            if (entri.State is EntityState.Added or EntityState.Modified &&
                entri.Metadata.FindProperty(NamaProperti) is not null)
            {
                entri.Property(NamaProperti).CurrentValue = sekarang;
            }
        }
    }
}
