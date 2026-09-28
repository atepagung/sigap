using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.Audit;

/// <summary>
/// Tabel <c>"JejakPerubahan"</c> — jejak audit lintas entitas (API_CONTRACT bagian 1.7).
/// Nilai sebelum/sesudah disimpan sebagai JSON di <c>"ringkasan"</c> karena tabel ini tidak
/// punya kolom khusus untuk itu. Diisi interceptor terpusat (P6.4), bukan per endpoint.
/// </summary>
public sealed class JejakPerubahan
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string Entitas { get; set; } = null!;
    public string EntitasId { get; set; } = null!;
    public string Aksi { get; set; } = null!;
    public string? Alasan { get; set; }
    public string? Ringkasan { get; set; }
    public string OlehId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public User Oleh { get; set; } = null!;
}

internal sealed class KonfigurasiJejakPerubahan : IEntityTypeConfiguration<JejakPerubahan>
{
    public void Configure(EntityTypeBuilder<JejakPerubahan> e)
    {
        e.HasIndex(x => new { x.Entitas, x.EntitasId }).HasDatabaseName("JejakPerubahan_entitas_entitasId_idx");
        e.HasOne(x => x.Oleh).WithMany().HasForeignKey(x => x.OlehId)
            .HasConstraintName("JejakPerubahan_olehId_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}
