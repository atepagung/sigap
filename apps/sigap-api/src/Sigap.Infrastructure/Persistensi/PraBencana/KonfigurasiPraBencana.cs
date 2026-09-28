using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sigap.Infrastructure.Persistensi.PraBencana;

internal sealed class KonfigurasiMkbDocument : IEntityTypeConfiguration<MkbDocument>
{
    public void Configure(EntityTypeBuilder<MkbDocument> e)
    {
        e.Property(x => x.Isi).HasColumnType("jsonb");
        e.HasIndex(x => new { x.UnitId, x.Kode }).IsUnique().HasDatabaseName("MkbDocument_unitId_kode_key");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("MkbDocument_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById)
            .HasConstraintName("MkbDocument_submittedById_fkey").OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById)
            .HasConstraintName("MkbDocument_approvedById_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class KonfigurasiRisikoBencana : IEntityTypeConfiguration<RisikoBencana>
{
    public void Configure(EntityTypeBuilder<RisikoBencana> e)
    {
        e.HasIndex(x => new { x.UnitId, x.JenisAncaman }).IsUnique()
            .HasDatabaseName("RisikoBencana_unitId_jenisAncaman_key");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("RisikoBencana_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KonfigurasiAsetKritis : IEntityTypeConfiguration<AsetKritis>
{
    public void Configure(EntityTypeBuilder<AsetKritis> e) =>
        e.HasOne(x => x.Risiko).WithMany(x => x.Aset).HasForeignKey(x => x.RisikoId)
            .HasConstraintName("AsetKritis_risikoId_fkey").OnDelete(DeleteBehavior.Cascade);
}

internal sealed class KonfigurasiGrabListItem : IEntityTypeConfiguration<GrabListItem>
{
    public void Configure(EntityTypeBuilder<GrabListItem> e) =>
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("GrabListItem_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
}

internal sealed class KonfigurasiNomorDarurat : IEntityTypeConfiguration<NomorDarurat>
{
    public void Configure(EntityTypeBuilder<NomorDarurat> e) =>
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("NomorDarurat_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
}

internal sealed class KonfigurasiStandarPengendalian : IEntityTypeConfiguration<StandarPengendalian>
{
    public void Configure(EntityTypeBuilder<StandarPengendalian> e) =>
        e.HasIndex(x => new { x.EselonIKey, x.Versi }).IsUnique()
            .HasDatabaseName("StandarPengendalian_eselonIKey_versi_key");
}

internal sealed class KonfigurasiTemplatePesanKunci : IEntityTypeConfiguration<TemplatePesanKunci>
{
    public void Configure(EntityTypeBuilder<TemplatePesanKunci> e)
    {
        e.HasIndex(x => x.RisikoId).IsUnique().HasDatabaseName("TemplatePesanKunci_risikoId_key");
        e.HasOne(x => x.Risiko).WithOne(x => x.Template).HasForeignKey<TemplatePesanKunci>(x => x.RisikoId)
            .HasConstraintName("TemplatePesanKunci_risikoId_fkey").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class KonfigurasiAnggotaCallTree : IEntityTypeConfiguration<AnggotaCallTree>
{
    public void Configure(EntityTypeBuilder<AnggotaCallTree> e) =>
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("AnggotaCallTree_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
}

internal sealed class KonfigurasiSimulasiDrill : IEntityTypeConfiguration<SimulasiDrill>
{
    public void Configure(EntityTypeBuilder<SimulasiDrill> e) =>
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("SimulasiDrill_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
}
