using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sigap.Infrastructure.Persistensi;
using Sigap.Infrastructure.Persistensi.Konvensi;
using Sigap.Infrastructure.Persistensi.Notifikasi;
using Sigap.Notifikasi;

namespace Sigap.Infrastructure.Notifikasi;

/// <summary>
/// <see cref="ICatatanKiriman"/> di atas tabel <c>"KirimanPush"</c> yang sudah ada (indeks unik
/// <c>"KirimanPush_kunci_key"</c>). Penanda dibuat sebelum pengiriman; dari dua pemanggil bersamaan dengan kunci yang
/// sama, tepat satu menerima <c>true</c> karena yang kalah membentur indeks unik — bukan karena pemeriksaan di memori.
///
/// <para>
/// Dipanggil sesudah transaksi bisnis di-commit (<c>IPengirimNotifikasi</c>), di <c>DbContext</c> yang sama. Baris
/// yang gagal karena benturan dilepas dari pelacak, supaya <c>SaveChanges</c> berikutnya di permintaan yang sama tidak
/// mencoba menulisnya lagi. <c>"KirimanPush"</c> adalah pembukuan internal dan tidak diaudit
/// (<c>PencatatJejakInterceptor</c>).
/// </para>
/// </summary>
internal sealed class CatatanKirimanPostgres(SigapDbContext db, TimeProvider waktu) : ICatatanKiriman
{
    public async Task<bool> CobaCatatAsync(string kunci, string judul, string? penggunaId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kunci);

        var baris = new KirimanPush
        {
            Id = PembuatCuid.Buat(),
            Kunci = kunci,
            Judul = judul,
            UserId = penggunaId,
            CreatedAt = waktu.GetUtcNow().UtcDateTime
        };
        var entri = db.KirimanPush.Add(baris);

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "KirimanPush_kunci_key"
        })
        {
            return false;
        }
        finally
        {
            entri.State = EntityState.Detached;
        }
    }
}
