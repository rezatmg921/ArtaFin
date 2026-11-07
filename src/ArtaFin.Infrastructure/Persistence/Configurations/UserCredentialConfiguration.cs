using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("UserCredentials", table =>
        {
            table.HasCheckConstraint("CK_UserCredentials_FailedLoginCount_NonNegative",
                "[FailedLoginCount] >= 0");
        });

        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.UserId)
            .IsRequired();

        builder.Property(credential => credential.PasswordHash)
            .HasMaxLength(1024)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(credential => credential.HashAlgorithm)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(credential => credential.PasswordChangedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(credential => credential.FailedLoginCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(credential => credential.LockedUntil)
            .HasColumnType("datetimeoffset(7)");

        builder.HasIndex(credential => credential.UserId)
            .IsUnique()
            .HasDatabaseName("UX_UserCredentials_UserId");

        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<UserCredential>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
