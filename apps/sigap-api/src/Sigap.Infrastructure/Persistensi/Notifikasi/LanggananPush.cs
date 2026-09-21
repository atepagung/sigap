using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.Notifikasi;

/// <summary>
/// Tabel <c>"LanggananPush"</c> — langganan Web Push per perangkat. Sumber port
/// <c>IGudangLanggananPush</c> di <c>libs/notifikasi</c>.
///
/// <para>
/// <c>p256dh</c> dan <c>auth</c> adalah kunci perangkat: <b>tidak pernah</b> keluar di respons
/// mana pun, oleh siapa pun — bukan Sieve per peran, melainkan tidak diproyeksikan sama sekali.
/// </para>
/// </summary>
public sealed class LanggananPush
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string Endpoint { get; set; } = null!;
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;
    public string? Peramban { get; set; }
    public string UserId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? DipakaiPada { get; set; }

    public User User { get; set; } = null!;
}

/// <summary>
/// Tabel <c>"KirimanPush"</c> — penanda kiriman sekali saja, sumber port <c>ICatatanKiriman</c>.
/// Indeks unik <c>"kunci"</c> yang membuat dua permintaan bersamaan tidak dapat sama-sama lolos.
/// </summary>
public sealed class KirimanPush
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string Kunci { get; set; } = null!;

    /// <summary>Tanpa foreign key di database — sesuai skema prototipe.</summary>
    public string? UserId { get; set; }

    public string Judul { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

internal sealed class KonfigurasiLanggananPush : IEntityTypeConfiguration<LanggananPush>
{
    public void Configure(EntityTypeBuilder<LanggananPush> e)
    {
        e.HasIndex(x => x.Endpoint).IsUnique().HasDatabaseName("LanggananPush_endpoint_key");
        e.HasIndex(x => x.UserId).HasDatabaseName("LanggananPush_userId_idx");
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .HasConstraintName("LanggananPush_userId_fkey").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class KonfigurasiKirimanPush : IEntityTypeConfiguration<KirimanPush>
{
    public void Configure(EntityTypeBuilder<KirimanPush> e)
    {
        e.HasIndex(x => x.Kunci).IsUnique().HasDatabaseName("KirimanPush_kunci_key");
        e.HasIndex(x => x.CreatedAt).HasDatabaseName("KirimanPush_createdAt_idx");
    }
}
