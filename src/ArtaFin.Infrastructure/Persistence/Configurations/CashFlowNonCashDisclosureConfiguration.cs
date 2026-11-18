using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class CashFlowNonCashDisclosureConfiguration : IEntityTypeConfiguration<CashFlowNonCashDisclosure>
{
    public void Configure(EntityTypeBuilder<CashFlowNonCashDisclosure> builder)
    {
        builder.ToTable("CashFlowNonCashDisclosures", table =>
        {
            table.HasCheckConstraint("CK_CashFlowNonCashDisclosures_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint("CK_CashFlowNonCashDisclosures_SourceLineNumber_Positive", "[SourceLineNumber] > 0");
            table.HasCheckConstraint("CK_CashFlowNonCashDisclosures_Activity_Valid", "[Activity] IN (2, 3)");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.FinancialBookId).IsRequired();
        builder.Property(item => item.JournalEntryId).IsRequired();
        builder.Property(item => item.SourceLineNumber).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(19, 4).IsRequired();
        builder.Property(item => item.Currency).HasColumnType("char(3)").HasMaxLength(3)
            .IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(item => item.Activity).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(item => item.Category).HasMaxLength(CashFlowNonCashDisclosure.MaxCategoryLength).IsRequired();
        builder.Property(item => item.Memo).HasMaxLength(CashFlowNonCashDisclosure.MaxMemoLength);
        builder.Property(item => item.CreatedBy).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();

        builder.HasOne<FinancialBook>().WithMany()
            .HasForeignKey(item => item.FinancialBookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany()
            .HasForeignKey(item => item.JournalEntryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.JournalEntryId, item.SourceLineNumber })
            .IsUnique().HasDatabaseName("UX_CashFlowNonCashDisclosures_SourceLine");
        builder.HasIndex(item => new { item.FinancialBookId, item.CreatedAt })
            .HasDatabaseName("IX_CashFlowNonCashDisclosures_Book_CreatedAt");
    }
}
