using ArtaFin.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtaFin.Infrastructure.Persistence.Configurations;

public sealed class CashFlowAllocationConfiguration : IEntityTypeConfiguration<CashFlowAllocation>
{
    public void Configure(EntityTypeBuilder<CashFlowAllocation> builder)
    {
        builder.ToTable("CashFlowAllocations", table =>
        {
            table.HasCheckConstraint("CK_CashFlowAllocations_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint("CK_CashFlowAllocations_Activity_Valid", "[Activity] IN (1, 2, 3)");
        });

        builder.HasKey(allocation => allocation.Id);
        builder.Property(allocation => allocation.AllocationSetId).IsRequired();
        builder.Property(allocation => allocation.Amount).HasPrecision(19, 4).IsRequired();
        builder.Property(allocation => allocation.Activity).HasConversion<byte>().HasColumnType("tinyint").IsRequired();
        builder.Property(allocation => allocation.Category).HasMaxLength(CashFlowAllocationSet.MaxCategoryLength).IsRequired();
        builder.Property(allocation => allocation.CounterpartLineNumber);
        builder.Property(allocation => allocation.Memo).HasMaxLength(CashFlowAllocationSet.MaxMemoLength);

        builder.HasIndex(allocation => allocation.AllocationSetId);
    }
}
