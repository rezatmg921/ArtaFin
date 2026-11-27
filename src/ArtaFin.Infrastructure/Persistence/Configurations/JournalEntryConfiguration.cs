using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries", table =>
            table.HasCheckConstraint(
                "CK_JournalEntries_IdempotencyMetadata_Consistent",
                "([IdempotencyKey] IS NULL AND [IdempotencyUserId] IS NULL AND [RequestHash] IS NULL) OR ([IdempotencyKey] IS NOT NULL AND [IdempotencyUserId] IS NOT NULL AND [RequestHash] IS NOT NULL)"));
        builder.HasKey(entry => entry.Id);
        builder.HasAlternateKey(entry => new { entry.FinancialBookId, entry.Id });
        builder.Property(entry => entry.FinancialBookId).IsRequired();
        builder.Property(entry => entry.PostedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(JournalEntry.MaxDescriptionLength).IsRequired();
        builder.Property(entry => entry.ReversesJournalEntryId);
        builder.Property(entry => entry.IdempotencyUserId);
        builder.Property(entry => entry.IdempotencyKey).HasMaxLength(128);
        builder.Property(entry => entry.RequestHash)
            .HasColumnType("char(64)")
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength();

        builder.HasIndex(entry => new
            {
                entry.FinancialBookId,
                entry.IdempotencyUserId,
                entry.IdempotencyKey
            })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_JournalEntries_Idempotency");

        // A journal entry can be reversed at most once. The filtered unique index
        // allows any number of original entries because NULL values are excluded.
        builder.HasIndex(entry => entry.ReversesJournalEntryId)
            .IsUnique()
            .HasFilter("[ReversesJournalEntryId] IS NOT NULL")
            .HasDatabaseName("UX_JournalEntries_ReversesJournalEntryId");

        // Self-referencing FK keeps reversal references valid and prevents deleting
        // an original entry while its reversal exists.
        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(entry => entry.ReversesJournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FinancialBook>()
            .WithMany()
            .HasForeignKey(entry => entry.FinancialBookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(entry => entry.Lines, lines =>
        {
            lines.ToTable("JournalLines", table =>
            {
                table.HasCheckConstraint("CK_JournalLines_Amount_Positive", "[Amount] > 0");
                table.HasCheckConstraint("CK_JournalLines_Side_Valid", "[Side] IN (1, 2)");
            });

            lines.WithOwner()
                .HasPrincipalKey(entry => new { entry.FinancialBookId, entry.Id })
                .HasForeignKey("FinancialBookId", "JournalEntryId");
            lines.Property<Guid>("JournalEntryId");
            lines.HasKey("JournalEntryId", nameof(JournalLine.LineNumber));
            lines.Property(line => line.LineNumber).ValueGeneratedNever();
            lines.Property(line => line.FinancialBookId).IsRequired();
            lines.Property(line => line.AccountId).IsRequired();

            // Composite FK prevents a journal line from referencing an account
            // belonging to a different FinancialBook.
            lines.HasOne<Account>()
                .WithMany()
                .HasPrincipalKey(account => new { account.FinancialBookId, account.Id })
                .HasForeignKey(line => new { line.FinancialBookId, line.AccountId })
                .OnDelete(DeleteBehavior.Restrict);

            lines.Property(line => line.Side).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
            lines.Property(line => line.EquityMovementType).HasConversion<byte>()
                .HasColumnType("tinyint").HasDefaultValue(EquityMovementType.Unclassified).IsRequired();
            lines.Ignore(line => line.Amount);

            lines.Property<decimal>("_amount")
                .HasField("_amount")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasColumnName("Amount")
                .HasPrecision(19, 4)
                .IsRequired();

            lines.Property<string>("_currency")
                .HasField("_currency")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasColumnName("Currency")
                .HasColumnType("char(3)")
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsFixedLength()
                .IsRequired();
        });

        // OwnsMany introduces the owned collection navigation; configure its backing
        // field only after the ownership relationship has been registered.
        builder.Navigation(entry => entry.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
