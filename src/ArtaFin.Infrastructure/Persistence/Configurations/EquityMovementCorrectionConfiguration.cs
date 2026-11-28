using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class EquityMovementCorrectionConfiguration
    : IEntityTypeConfiguration<EquityMovementCorrection>
{
    public void Configure(EntityTypeBuilder<EquityMovementCorrection> builder)
    {
        builder.ToTable("EquityMovementCorrections", table =>
        {
            table.HasCheckConstraint(
                "CK_EquityMovementCorrections_LineNumber_Positive",
                "[LineNumber] > 0");

            table.HasCheckConstraint(
                "CK_EquityMovementCorrections_OriginalType_Valid",
                "[OriginalMovementType] IN (0, 1, 2, 3, 4)");

            table.HasCheckConstraint(
                "CK_EquityMovementCorrections_CorrectedType_Valid",
                "[CorrectedMovementType] IN (1, 2, 3, 4)");

            table.HasCheckConstraint(
                "CK_EquityMovementCorrections_Types_Different",
                "[OriginalMovementType] <> [CorrectedMovementType]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FinancialBookId).IsRequired();
        builder.Property(x => x.JournalEntryId).IsRequired();
        builder.Property(x => x.LineNumber).IsRequired();

        builder.Property(x => x.OriginalMovementType)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(x => x.CorrectedMovementType)
            .HasConversion<byte>()
            .HasColumnType("tinyint")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(EquityMovementCorrection.MaxReasonLength)
            .IsRequired();

        builder.Property(x => x.CreatedBy).IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.HasOne<FinancialBook>()
            .WithMany()
            .HasForeignKey(x => x.FinancialBookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(x => new { x.FinancialBookId, x.JournalEntryId })
            .HasPrincipalKey(x => new { x.FinancialBookId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
            {
                x.FinancialBookId,
                x.JournalEntryId,
                x.LineNumber,
                x.CreatedAt
            })
            .HasDatabaseName("IX_EquityMovementCorrections_Line_History");
    }
}
