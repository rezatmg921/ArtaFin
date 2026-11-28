using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");

        builder.HasKey(userRole => userRole.Id);

        builder.Property(userRole => userRole.UserId).IsRequired();
        builder.Property(userRole => userRole.RoleId).IsRequired();
        builder.Property(userRole => userRole.AssignedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId })
            .IsUnique()
            .HasDatabaseName("UX_UserRoles_UserId_RoleId");

        builder.HasIndex(userRole => userRole.RoleId)
            .HasDatabaseName("IX_UserRoles_RoleId");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(userRole => userRole.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppRole>()
            .WithMany()
            .HasForeignKey(userRole => userRole.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
