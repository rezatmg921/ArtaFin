using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .HasMaxLength(AppRole.MaxNameLength)
            .IsRequired();

        builder.Property(role => role.NormalizedName)
            .HasMaxLength(AppRole.MaxNameLength)
            .IsRequired();

        builder.Property(role => role.Description)
            .HasMaxLength(500);

        builder.Property(role => role.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasIndex(role => role.NormalizedName)
            .IsUnique()
            .HasDatabaseName("UX_Roles_NormalizedName");
    }
}
