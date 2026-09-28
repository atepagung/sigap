using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sigap.Infrastructure.Persistensi.Asesmen;

internal sealed class KonfigurasiDamageAssessment : IEntityTypeConfiguration<DamageAssessment>
{
    public void Configure(EntityTypeBuilder<DamageAssessment> e)
    {
        e.Property(x => x.LayananTerdampak).HasColumnType("jsonb");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("DamageAssessment_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById)
            .HasConstraintName("DamageAssessment_submittedById_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KonfigurasiChecklistKondisiLapangan : IEntityTypeConfiguration<ChecklistKondisiLapangan>
{
    public void Configure(EntityTypeBuilder<ChecklistKondisiLapangan> e)
    {
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("ChecklistKondisiLapangan_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById)
            .HasConstraintName("ChecklistKondisiLapangan_submittedById_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KonfigurasiLayananKritis : IEntityTypeConfiguration<LayananKritis>
{
    public void Configure(EntityTypeBuilder<LayananKritis> e)
    {
        e.Property(x => x.SkorDampak).HasColumnType("jsonb");
        e.HasIndex(x => new { x.UnitId, x.Nama }).IsUnique().HasDatabaseName("LayananKritis_unitId_nama_key");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("LayananKritis_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KonfigurasiGangguanLayanan : IEntityTypeConfiguration<GangguanLayanan>
{
    public void Configure(EntityTypeBuilder<GangguanLayanan> e)
    {
        e.Property(x => x.Mulai).HasDefaultValueSql("CURRENT_TIMESTAMP");
        e.HasOne(x => x.Layanan).WithMany(x => x.Gangguan).HasForeignKey(x => x.LayananId)
            .HasConstraintName("GangguanLayanan_layananId_fkey").OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.DilaporkanOleh).WithMany().HasForeignKey(x => x.DilaporkanOlehId)
            .HasConstraintName("GangguanLayanan_dilaporkanOlehId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}
