using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Sigap.Infrastructure.Persistensi.Asesmen;
using Sigap.Infrastructure.Persistensi.Organisasi;

// ═════════════════════════════════════════════════════════════════════════════════════════
// FASE 2 — pasca-bencana: LPKB, rilis komunikasi, status aset, eksekusi RKB.
// DI LUAR CAKUPAN FASE 1: tidak ada endpoint, permission, maupun use case yang memakainya.
// "StatusAset" khususnya: menunya dihapus koreksi stakeholder 4 (sudah tercakup aspek Aset).
// ═════════════════════════════════════════════════════════════════════════════════════════

namespace Sigap.Infrastructure.Persistensi.PascaBencana;

/// <summary>Enum PostgreSQL <c>"LpkbStatus"</c>.</summary>
public enum LpkbStatus
{
    [PgName("DRAFT")] Draft,
    [PgName("MENUNGGU_REVIEW")] MenungguReview,
    [PgName("DIREVIEW")] Direview,
    [PgName("FINAL")] Final
}

/// <summary>Tabel <c>"LpkbReport"</c>.</summary>
public sealed class LpkbReport
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;

    /// <summary>JSONB, wajib.</summary>
    public string Isi { get; set; } = null!;

    public LpkbStatus Status { get; set; }
    public string SubmittedById { get; set; } = null!;
    public string? ReviewedById { get; set; }
    public string? CatatanReview { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
    public User SubmittedBy { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}

/// <summary>Tabel <c>"RilisKomunikasi"</c>.</summary>
public sealed class RilisKomunikasi
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Judul { get; set; } = null!;
    public string Isi { get; set; } = null!;
    public string? Saluran { get; set; }
    public string? Fase { get; set; }

    /// <summary>Bawaan database <c>'MENUNGGU'</c>.</summary>
    public string Status { get; set; } = "MENUNGGU";

    public string? CatatanPimpinan { get; set; }

    /// <summary>Tanpa foreign key di database — sesuai skema prototipe.</summary>
    public string DisusunOlehId { get; set; } = null!;

    public string? DisetujuiOlehId { get; set; }
    public DateTime? DisetujuiPada { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
}

/// <summary>Tabel <c>"StatusAset"</c>.</summary>
public sealed class StatusAset
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string Kategori { get; set; } = null!;
    public string Kondisi { get; set; } = null!;
    public string? Keterangan { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
}

/// <summary>Tabel <c>"LangkahEksekusi"</c> (eksekusi RKB).</summary>
public sealed class LangkahEksekusi
{
    public bool IsDemo { get; set; }
    public string Id { get; set; } = null!;
    public string UnitId { get; set; } = null!;
    public string LayananId { get; set; } = null!;
    public string Langkah { get; set; } = null!;

    /// <summary>RKBU atau MANUAL. Bawaan database <c>'MANUAL'</c>.</summary>
    public string Sumber { get; set; } = "MANUAL";

    public bool Selesai { get; set; }
    public DateTime? SelesaiPada { get; set; }
    public string? Keterangan { get; set; }
    public string? OlehId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Unit Unit { get; set; } = null!;
    public LayananKritis Layanan { get; set; } = null!;
}

internal sealed class KonfigurasiLpkbReport : IEntityTypeConfiguration<LpkbReport>
{
    public void Configure(EntityTypeBuilder<LpkbReport> e)
    {
        e.Property(x => x.Isi).HasColumnType("jsonb");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("LpkbReport_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById)
            .HasConstraintName("LpkbReport_submittedById_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById)
            .HasConstraintName("LpkbReport_reviewedById_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class KonfigurasiRilisKomunikasi : IEntityTypeConfiguration<RilisKomunikasi>
{
    public void Configure(EntityTypeBuilder<RilisKomunikasi> e) =>
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("RilisKomunikasi_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
}

internal sealed class KonfigurasiStatusAset : IEntityTypeConfiguration<StatusAset>
{
    public void Configure(EntityTypeBuilder<StatusAset> e) =>
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("StatusAset_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
}

internal sealed class KonfigurasiLangkahEksekusi : IEntityTypeConfiguration<LangkahEksekusi>
{
    public void Configure(EntityTypeBuilder<LangkahEksekusi> e)
    {
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("LangkahEksekusi_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Layanan).WithMany(x => x.Langkah).HasForeignKey(x => x.LayananId)
            .HasConstraintName("LangkahEksekusi_layananId_fkey").OnDelete(DeleteBehavior.Cascade);
    }
}
