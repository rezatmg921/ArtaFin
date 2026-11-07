using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.FinancialBookId)
            .IsRequired();

        builder.Property(entry => entry.PostedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(entry => entry.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Navigation(entry => entry.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(entry => entry.Lines, lines =>
        {
            lines.ToTable("JournalLines", table =>
            {
                table.HasCheckConstraint(
                    "CK_JournalLines_Amount_Positive",
                    "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_JournalLines_Side_Valid",
                    "[Side] IN (1, 2)");
            });

            lines.WithOwner()
                .HasForeignKey("JournalEntryId");

            lines.Property<Guid>("JournalEntryId");

            lines.HasKey("JournalEntryId", nameof(JournalLine.LineNumber));

            lines.Property(line => line.LineNumber)
                .ValueGeneratedNever();

            lines.Property(line => line.AccountId)
                .IsRequired();

            lines.Property(line => line.Side)
                .HasConversion<byte>()
                .HasColumnType("tinyint")
                .IsRequired();

            // Money remains a Domain value object. Its two private scalar fields are
            // mapped separately so SQL can store and query amount and currency efficiently.
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
    }
}
