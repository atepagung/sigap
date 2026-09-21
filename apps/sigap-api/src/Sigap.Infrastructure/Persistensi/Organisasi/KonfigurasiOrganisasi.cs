using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Sigap.Infrastructure.Persistensi.Organisasi;

internal sealed class KonfigurasiUnit : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> e)
    {
        e.HasIndex(x => x.Kode).IsUnique().HasDatabaseName("Unit_kode_key");
        e.HasOne(x => x.ParentUnit).WithMany(x => x.SubUnit).HasForeignKey(x => x.ParentUnitId)
            .HasConstraintName("Unit_parentUnitId_fkey").OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class KonfigurasiUser : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.HasIndex(x => x.Nip).IsUnique().HasDatabaseName("User_nip_key");
        e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("User_email_key");
        e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("User_unitId_fkey").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class KonfigurasiUserRole : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> e)
    {
        e.HasIndex(x => new { x.UserId, x.Role }).IsUnique().HasDatabaseName("UserRole_userId_role_key");
        e.HasOne(x => x.User).WithMany(x => x.Roles).HasForeignKey(x => x.UserId)
            .HasConstraintName("UserRole_userId_fkey").OnDelete(DeleteBehavior.Cascade);
    }
}
