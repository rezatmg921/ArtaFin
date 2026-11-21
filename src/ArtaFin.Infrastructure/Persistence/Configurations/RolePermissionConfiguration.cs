using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(rolePermission => rolePermission.Id);

        builder.Property(rolePermission => rolePermission.RoleId).IsRequired();
        builder.Property(rolePermission => rolePermission.PermissionId).IsRequired();
        builder.Property(rolePermission => rolePermission.AssignedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasIndex(rolePermission => new
            {
                rolePermission.RoleId,
                rolePermission.PermissionId
            })
            .IsUnique()
            .HasDatabaseName("UX_RolePermissions_RoleId_PermissionId");

        builder.HasIndex(rolePermission => rolePermission.PermissionId)
            .HasDatabaseName("IX_RolePermissions_PermissionId");

        builder.HasOne<AppRole>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppPermission>()
            .WithMany()
            .HasForeignKey(rolePermission => rolePermission.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
