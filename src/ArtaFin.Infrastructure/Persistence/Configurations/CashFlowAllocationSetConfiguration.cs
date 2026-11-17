using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class CashFlowAllocationSetConfiguration : IEntityTypeConfiguration<CashFlowAllocationSet>
{
    public void Configure(EntityTypeBuilder<CashFlowAllocationSet> builder)
    {
        builder.ToTable("CashFlowAllocationSets", table =>
        {
            table.HasCheckConstraint("CK_CashFlowAllocationSets_SourceAmount_Positive", "[SourceAmount] > 0");
            table.HasCheckConstraint("CK_CashFlowAllocationSets_SourceLineNumber_Positive", "[SourceLineNumber] > 0");
            table.HasCheckConstraint("CK_CashFlowAllocationSets_Version_Positive", "[Version] > 0");
            table.HasCheckConstraint("CK_CashFlowAllocationSets_Status_Valid", "[Status] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_CashFlowAllocationSets_Finalization_Consistent",
                "([Status] = 1 AND [FinalizedBy] IS NULL AND [FinalizedAt] IS NULL) OR ([Status] IN (2, 3) AND [FinalizedBy] IS NOT NULL AND [FinalizedAt] IS NOT NULL)");
            table.HasCheckConstraint("CK_CashFlowAllocationSets_Supersedes_Consistent",
                "([Version] = 1 AND [SupersedesAllocationSetId] IS NULL) OR ([Version] > 1 AND [SupersedesAllocationSetId] IS NOT NULL)");
        });

        builder.HasKey(set => set.Id);
        builder.Property(set => set.FinancialBookId).IsRequired();
        builder.Property(set => set.SourceJournalEntryId).IsRequired();
        builder.Property(set => set.SourceLineNumber).IsRequired();
        builder.Property(set => set.SourceAmount).HasPrecision(19, 4).IsRequired();
        builder.Property(set => set.Currency).HasColumnType("char(3)").HasMaxLength(3).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(set => set.Version).IsRequired();
        builder.Property(set => set.Status).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(set => set.CreatedBy).IsRequired();
        builder.Property(set => set.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(set => set.FinalizedBy);
        builder.Property(set => set.FinalizedAt).HasColumnType("datetimeoffset(7)");
        builder.Property<byte[]>("RowVersion").IsRowVersion().IsRequired().HasColumnName("RowVersion");

        builder.HasOne<FinancialBook>().WithMany()
            .HasForeignKey(set => set.FinancialBookId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JournalEntry>().WithMany()
            .HasForeignKey(set => set.SourceJournalEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CashFlowAllocationSet>().WithMany()
            .HasForeignKey(set => set.SupersedesAllocationSetId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(set => new { set.SourceJournalEntryId, set.SourceLineNumber, set.Version })
            .IsUnique().HasDatabaseName("UX_CashFlowAllocationSets_Source_Version");
        builder.HasIndex(set => new { set.SourceJournalEntryId, set.SourceLineNumber })
            .IsUnique().HasFilter("[Status] = 2")
            .HasDatabaseName("UX_CashFlowAllocationSets_CurrentFinalized");

        builder.Ignore(set => set.AllocatedAmount);
        builder.Ignore(set => set.RemainingAmount);

        builder.HasMany(set => set.Allocations)
            .WithOne()
            .HasForeignKey(allocation => allocation.AllocationSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(set => set.Allocations)
            .HasField("_allocations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
