using ArtaFin.Domain.Contacts;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Obligations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class FinancialObligationConfiguration : IEntityTypeConfiguration<FinancialObligation>
{
    public void Configure(EntityTypeBuilder<FinancialObligation> builder)
    {
        builder.ToTable("FinancialObligations", table =>
        {
            table.HasCheckConstraint("CK_FinancialObligations_Direction_Valid", "[Direction] IN (1, 2)");
            table.HasCheckConstraint("CK_FinancialObligations_Kind_Valid", "[Kind] IN (1, 2)");
            table.HasCheckConstraint("CK_FinancialObligations_Status_Valid", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_FinancialObligations_Amount_Positive", "[OriginalAmount] > 0");
            table.HasCheckConstraint("CK_FinancialObligations_Dates_Valid", "[DueDate] >= [EffectiveDate]");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.FinancialBookId).IsRequired();
        builder.Property(x => x.ContactId).IsRequired();
        builder.Property(x => x.ControlAccountId).IsRequired();
        builder.Property(x => x.Direction).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.Kind).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.OriginalAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength().HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.EffectiveDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.DueDate).HasColumnType("date").IsRequired();
        builder.Property(x => x.Description).HasMaxLength(FinancialObligation.MaxDescriptionLength).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(FinancialObligation.MaxReferenceLength);
        builder.Property(x => x.Status).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(x => x.CancelledAt).HasColumnType("datetimeoffset(7)");

        builder.HasOne<FinancialBook>().WithMany()
            .HasForeignKey(x => x.FinancialBookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Contact>().WithMany()
            .HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.FinancialBookId, x.ControlAccountId })
            .HasPrincipalKey(x => new { x.FinancialBookId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.FinancialBookId, x.Direction, x.Status, x.DueDate })
            .HasDatabaseName("IX_FinancialObligations_Book_Direction_Status_DueDate");
        builder.HasIndex(x => new { x.FinancialBookId, x.ContactId, x.EffectiveDate })
            .HasDatabaseName("IX_FinancialObligations_Book_Contact_EffectiveDate");
    }
}
