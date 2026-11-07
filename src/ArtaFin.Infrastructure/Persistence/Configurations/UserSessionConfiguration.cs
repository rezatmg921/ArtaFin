using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions", table =>
            table.HasCheckConstraint("CK_UserSessions_Expiry_After_Creation",
                "[ExpiresAt] > [CreatedAt]"));

        builder.HasKey(session => session.Id);

        builder.Property(session => session.UserId)
            .IsRequired();

        builder.Property(session => session.RefreshTokenHash)
            .HasMaxLength(128)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(session => session.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(session => session.ExpiresAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(session => session.RevokedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.Property(session => session.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(session => session.RefreshTokenHash)
            .IsUnique()
            .HasDatabaseName("UX_UserSessions_RefreshTokenHash");

        builder.HasIndex(session => session.UserId)
            .HasDatabaseName("IX_UserSessions_UserId");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserSession>()
            .WithMany()
            .HasForeignKey(session => session.ReplacedBySessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
