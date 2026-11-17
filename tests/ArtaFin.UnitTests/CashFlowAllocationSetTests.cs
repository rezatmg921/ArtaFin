using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class CashFlowAllocationSetTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid EntryId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    private static CashFlowAllocationSet CreateSet() =>
        CashFlowAllocationSet.Create(
            Guid.NewGuid(), BookId, EntryId, 1, 100m, "irr", ActorId, Now);

    [Fact]
    public void Create_NormalizesCurrency_AndStartsAsDraft()
    {
        var set = CreateSet();

        Assert.Equal("IRR", set.Currency);
        Assert.Equal(CashFlowAllocationSetStatus.Draft, set.Status);
        Assert.Equal(100m, set.RemainingAmount);
        Assert.Empty(set.Allocations);
    }

    [Fact]
    public void AddAllocation_AllowsSplitAcrossActivities()
    {
        var set = CreateSet();

        set.AddAllocation(Guid.NewGuid(), 80m, CashFlowActivity.Financing, "LOAN_PRINCIPAL", 2);
        set.AddAllocation(Guid.NewGuid(), 20m, CashFlowActivity.Operating, "INTEREST_PAID", 2);

        Assert.Equal(100m, set.AllocatedAmount);
        Assert.Equal(0m, set.RemainingAmount);
        Assert.Equal(2, set.Allocations.Count);
    }

    [Fact]
    public void AddAllocation_RequiresCounterpartAndSupportedPrecision()
    {
        var set = CreateSet();

        Assert.Throws<DomainException>(() =>
            set.AddAllocation(Guid.NewGuid(), 10m, CashFlowActivity.Operating, "CUSTOMER_RECEIPT"));
        Assert.Throws<DomainException>(() =>
            set.AddAllocation(Guid.NewGuid(), 10.12345m, CashFlowActivity.Operating, "CUSTOMER_RECEIPT", 2));
    }

    [Fact]
    public void AddAllocation_RejectsOverAllocation()
    {
        var set = CreateSet();
        set.AddAllocation(Guid.NewGuid(), 70m, CashFlowActivity.Financing, "LOAN_PRINCIPAL", 2);

        Assert.Throws<DomainException>(() =>
            set.AddAllocation(Guid.NewGuid(), 31m, CashFlowActivity.Operating, "INTEREST_PAID", 2));
    }

    [Fact]
    public void Finalize_RequiresExactAllocation()
    {
        var set = CreateSet();
        set.AddAllocation(Guid.NewGuid(), 99m, CashFlowActivity.Operating, "CUSTOMER_RECEIPT", 2);

        Assert.Throws<DomainException>(() => set.Finalize(ActorId, Now.AddMinutes(1)));
    }

    [Fact]
    public void Finalize_RequiresExactAmount_AndMakesSetImmutable()
    {
        var set = CreateSet();
        set.AddAllocation(Guid.NewGuid(), 80m, CashFlowActivity.Financing, "LOAN_PRINCIPAL", 2);
        set.AddAllocation(Guid.NewGuid(), 20m, CashFlowActivity.Operating, "INTEREST_PAID", 2);

        var finalizedAt = Now.AddMinutes(1);
        set.Finalize(ActorId, finalizedAt);

        Assert.Equal(CashFlowAllocationSetStatus.Finalized, set.Status);
        Assert.Equal(ActorId, set.FinalizedBy);
        Assert.Equal(finalizedAt, set.FinalizedAt);
        Assert.Throws<DomainException>(() =>
            set.AddAllocation(Guid.NewGuid(), 1m, CashFlowActivity.Operating, "OTHER", 2));
        Assert.Throws<DomainException>(() => set.RemoveAllocation(set.Allocations[0].Id));
    }

    [Fact]
    public void FinalizedSet_CanBeMarkedSuperseded_WithoutChangingItsAllocations()
    {
        var set = CreateSet();
        set.AddAllocation(Guid.NewGuid(), 100m, CashFlowActivity.Operating, "CUSTOMER_RECEIPT", 2);
        set.Finalize(ActorId, Now.AddMinutes(1));

        set.MarkSuperseded();

        Assert.Equal(CashFlowAllocationSetStatus.Superseded, set.Status);
        Assert.Equal(100m, set.AllocatedAmount);
        Assert.Equal(ActorId, set.FinalizedBy);
        Assert.Throws<DomainException>(() =>
            set.AddAllocation(Guid.NewGuid(), 1m, CashFlowActivity.Operating, "OTHER", 2));
    }

    [Fact]
    public void RemoveAllocation_IsAllowedOnlyWhileDraft()
    {
        var set = CreateSet();
        var allocationId = Guid.NewGuid();
        set.AddAllocation(allocationId, 100m, CashFlowActivity.Operating, "CUSTOMER_RECEIPT", 2);

        set.RemoveAllocation(allocationId);

        Assert.Equal(0m, set.AllocatedAmount);
    }

    [Fact]
    public void Create_RejectsInvalidVersionAndSupersedesCombination()
    {
        Assert.Throws<ArgumentException>(() => CashFlowAllocationSet.Create(
            Guid.NewGuid(), BookId, EntryId, 1, 100m, "IRR", ActorId, Now,
            version: 2, supersedesAllocationSetId: null));
    }
}
