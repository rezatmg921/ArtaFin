using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users", table =>
            table.HasCheckConstraint("CK_Users_DisabledState_Consistent",
                "([IsActive] = 1 AND [DisabledAt] IS NULL) OR ([IsActive] = 0 AND [DisabledAt] IS NOT NULL)"));

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .HasMaxLength(AppUser.MaxEmailLength)
            .IsRequired();

        builder.Property(user => user.NormalizedEmail)
            .HasMaxLength(AppUser.MaxEmailLength)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(user => user.DisabledAt)
            .HasColumnType("datetimeoffset(7)");

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("UX_Users_NormalizedEmail");
    }
}
