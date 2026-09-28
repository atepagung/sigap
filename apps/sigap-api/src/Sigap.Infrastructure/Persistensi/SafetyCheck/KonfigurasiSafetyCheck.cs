using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sigap.Infrastructure.Persistensi.SafetyCheck;

internal sealed class KonfigurasiSafetyCheckResponse : IEntityTypeConfiguration<SafetyCheckResponse>
{
    public void Configure(EntityTypeBuilder<SafetyCheckResponse> e)
    {
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .HasConstraintName("SafetyCheckResponse_userId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("SafetyCheckResponse_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Broadcast).WithMany(x => x.SafetyChecks).HasForeignKey(x => x.BroadcastId)
            .HasConstraintName("SafetyCheckResponse_broadcastId_fkey").OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.DicatatOleh).WithMany().HasForeignKey(x => x.DicatatOlehId)
            .HasConstraintName("SafetyCheckResponse_dicatatOlehId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}
