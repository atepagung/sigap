using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sigap.Infrastructure.Persistensi.Broadcast;

internal sealed class KonfigurasiActiveBroadcast : IEntityTypeConfiguration<ActiveBroadcast>
{
    public void Configure(EntityTypeBuilder<ActiveBroadcast> e) =>
        e.HasOne(x => x.DikirimOleh).WithMany().HasForeignKey(x => x.DikirimOlehId)
            .HasConstraintName("ActiveBroadcast_dikirimOlehId_fkey").OnDelete(DeleteBehavior.Restrict);
}

internal sealed class KonfigurasiBroadcastRequest : IEntityTypeConfiguration<BroadcastRequest>
{
    public void Configure(EntityTypeBuilder<BroadcastRequest> e)
    {
        e.HasOne(x => x.RequestedByUnit).WithMany().HasForeignKey(x => x.RequestedByUnitId)
            .HasConstraintName("BroadcastRequest_requestedByUnitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById)
            .HasConstraintName("BroadcastRequest_requestedById_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById)
            .HasConstraintName("BroadcastRequest_approvedById_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class KonfigurasiBroadcastSasaranUnit : IEntityTypeConfiguration<BroadcastSasaranUnit>
{
    public void Configure(EntityTypeBuilder<BroadcastSasaranUnit> e)
    {
        // DDL tabel ke-33 memakai REFERENCES tanpa nama; nama di bawah adalah yang dibuat
        // PostgreSQL sendiri, dan aksinya NO ACTION (bawaan) — bukan RESTRICT seperti Prisma.
        e.HasOne(x => x.Broadcast).WithMany().HasForeignKey(x => x.BroadcastId)
            .HasConstraintName("BroadcastSasaranUnit_broadcastId_fkey").OnDelete(DeleteBehavior.NoAction);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("BroadcastSasaranUnit_unitId_fkey").OnDelete(DeleteBehavior.NoAction);
        e.HasOne(x => x.DilewatiKarenaBroadcast).WithMany().HasForeignKey(x => x.DilewatiKarenaBroadcastId)
            .HasConstraintName("BroadcastSasaranUnit_dilewatiKarenaBroadcastId_fkey")
            .OnDelete(DeleteBehavior.NoAction);

        e.HasIndex(x => new { x.BroadcastId, x.UnitId }).IsUnique()
            .HasDatabaseName("sasaran_unik_per_broadcast");
        e.HasIndex(x => new { x.UnitId, x.JenisBencana }).IsUnique()
            .HasDatabaseName("sasaran_satu_pemegang_aktif")
            .HasFilter("\"status\" = 'DISASAR' AND \"aktif\"");
        e.HasIndex(x => x.UnitId).HasDatabaseName("sasaran_unit_aktif").HasFilter("\"aktif\"");

        e.ToTable(t => t.HasCheckConstraint(
            "sasaran_dilewati_wajib_pemegang",
            "((\"status\" = 'DILEWATI') = (\"dilewatiKarenaBroadcastId\" IS NOT NULL))"));
    }
}
