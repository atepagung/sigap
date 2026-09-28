using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sigap.Infrastructure.Persistensi.Laporan;

internal sealed class KonfigurasiDisasterAlert : IEntityTypeConfiguration<DisasterAlert>
{
    public void Configure(EntityTypeBuilder<DisasterAlert> e)
    {
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("DisasterAlert_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pelapor).WithMany().HasForeignKey(x => x.PelaporId)
            .HasConstraintName("DisasterAlert_pelaporId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Verifikator).WithMany().HasForeignKey(x => x.VerifikatorId)
            .HasConstraintName("DisasterAlert_verifikatorId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}
