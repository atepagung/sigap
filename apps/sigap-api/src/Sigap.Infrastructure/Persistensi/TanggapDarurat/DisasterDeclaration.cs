using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Organisasi;

namespace Sigap.Infrastructure.Persistensi.TanggapDarurat;

/// <summary>Enum PostgreSQL <c>"DeklarasiStatus"</c>.</summary>
public enum DeklarasiStatus
{
    [PgName("NORMAL")] Normal,
    [PgName("DARURAT")] Darurat,
    [PgName("PULIH")] Pulih
}

/// <summary>
/// Tabel <c>"DisasterDeclaration"</c> — status tanggap darurat unit. Dibuat saat Pimpinan Satker
/// menyetujui asesmen (API_CONTRACT #28), ditutup lewat #29.
/// </summary>
public sealed class DisasterDeclaration
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string DeclaredById { get; set; } = null!;
    public string JenisBencana { get; set; } = null!;
    public string? KategoriBencana { get; set; }
    public string? Lokasi { get; set; }

    /// <summary>Bawaan database <c>DARURAT</c> — bukan anggota pertama enum, jadi diisi eksplisit.</summary>
    public DeklarasiStatus Status { get; set; } = DeklarasiStatus.Darurat;

    /// <summary>Bawaan database <c>CURRENT_TIMESTAMP</c>.</summary>
    public DateTime DeclaredAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public DateTime? EskalasiPada { get; set; }
    public string? EskalasiOlehId { get; set; }
    public string? AlasanEskalasi { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }
    public string? DibatalkanOlehId { get; set; }

    public Unit Unit { get; set; } = null!;
    public User DeclaredBy { get; set; } = null!;
    public User? EskalasiOleh { get; set; }
}

/// <summary>Tabel <c>"PemulihanLogEntry"</c> — log pemulihan pasca-bencana.</summary>
public sealed class PemulihanLogEntry
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string PetugasId { get; set; } = null!;
    public string Aksi { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? DiperbaruiPada { get; set; }
    public bool Dibatalkan { get; set; }
    public string? AlasanBatal { get; set; }
    public DateTime? DibatalkanPada { get; set; }
    public string? DibatalkanOlehId { get; set; }

    public Unit Unit { get; set; } = null!;
    public User Petugas { get; set; } = null!;
}

internal sealed class KonfigurasiDisasterDeclaration : IEntityTypeConfiguration<DisasterDeclaration>
{
    public void Configure(EntityTypeBuilder<DisasterDeclaration> e)
    {
        e.Property(x => x.DeclaredAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("DisasterDeclaration_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.DeclaredBy).WithMany().HasForeignKey(x => x.DeclaredById)
            .HasConstraintName("DisasterDeclaration_declaredById_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.EskalasiOleh).WithMany().HasForeignKey(x => x.EskalasiOlehId)
            .HasConstraintName("DisasterDeclaration_eskalasiOlehId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class KonfigurasiPemulihanLogEntry : IEntityTypeConfiguration<PemulihanLogEntry>
{
    public void Configure(EntityTypeBuilder<PemulihanLogEntry> e)
    {
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("PemulihanLogEntry_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Petugas).WithMany().HasForeignKey(x => x.PetugasId)
            .HasConstraintName("PemulihanLogEntry_petugasId_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}
