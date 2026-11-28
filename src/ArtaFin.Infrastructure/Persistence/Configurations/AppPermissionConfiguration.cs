using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class AppPermissionConfiguration : IEntityTypeConfiguration<AppPermission>
{
    public void Configure(EntityTypeBuilder<AppPermission> builder)
    {
        builder.ToTable("Permissions");

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Code)
            .HasMaxLength(AppPermission.MaxCodeLength)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(permission => permission.Name)
            .HasMaxLength(AppPermission.MaxNameLength)
            .IsRequired();

        builder.Property(permission => permission.Description)
            .HasMaxLength(500);

        builder.Property(permission => permission.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasIndex(permission => permission.Code)
            .IsUnique()
            .HasDatabaseName("UX_Permissions_Code");
    }
}
