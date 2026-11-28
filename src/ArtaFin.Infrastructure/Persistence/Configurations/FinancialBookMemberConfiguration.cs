using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class FinancialBookMemberConfiguration : IEntityTypeConfiguration<FinancialBookMember>
{
    public void Configure(EntityTypeBuilder<FinancialBookMember> builder)
    {
        builder.ToTable("FinancialBookMembers", table =>
            table.HasCheckConstraint(
                "CK_FinancialBookMembers_Role_Valid",
                "[Role] IN (1, 2, 3)"));

        builder.HasKey(member => member.Id);

        builder.Property(member => member.FinancialBookId).IsRequired();
        builder.Property(member => member.UserId).IsRequired();
        builder.Property(member => member.Role)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();
        builder.Property(member => member.GrantedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();
        builder.Property(member => member.RevokedAt)
            .HasColumnType("datetimeoffset(7)");

        builder.HasIndex(member => new { member.FinancialBookId, member.UserId })
            .IsUnique()
            .HasDatabaseName("UX_FinancialBookMembers_BookId_UserId");

        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_FinancialBookMembers_UserId");

        builder.HasOne<FinancialBook>()
            .WithMany()
            .HasForeignKey(member => member.FinancialBookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
