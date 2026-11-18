using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Finance;

public sealed class LeaseContractTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid LiabilityAccountId = Guid.NewGuid();
    private static readonly Guid InterestAccountId = Guid.NewGuid();
    private static readonly Guid PaymentAccountId = Guid.NewGuid();

    private static LeaseContract CreateContract(
        DateOnly? firstPaymentDate = null,
        DateOnly? contractEndDate = null,
        decimal effectivePeriodicRate = 0.01m,
        LeasePaymentTiming paymentTiming = LeasePaymentTiming.InArrears,
        Guid? liabilityAccountId = null,
        Guid? interestAccountId = null,
        Guid? paymentAccountId = null) =>
        LeaseContract.Create(
            Guid.NewGuid(),
            BookId,
            "LEASE-001",
            "Office equipment lease",
            new DateOnly(2026, 1, 1),
            firstPaymentDate ?? new DateOnly(2026, 2, 1),
            contractEndDate ?? new DateOnly(2028, 1, 1),
            new Money(80_000_000m, "IRR"),
            effectivePeriodicRate,
            LeasePaymentFrequency.Monthly,
            paymentTiming,
            liabilityAccountId ?? LiabilityAccountId,
            interestAccountId ?? InterestAccountId,
            paymentAccountId ?? PaymentAccountId);

    [Fact]
    public void Create_PersistsExplicitPeriodRateAndContractTerms()
    {
        var contract = CreateContract();

        Assert.Equal(BookId, contract.FinancialBookId);
        Assert.Equal("LEASE-001", contract.ContractNumber);
        Assert.Equal(80_000_000m, contract.InitialLiability.Amount);
        Assert.Equal("IRR", contract.InitialLiability.Currency);
        Assert.Equal(0.01m, contract.EffectivePeriodicRate);
        Assert.Equal(LeasePaymentFrequency.Monthly, contract.PaymentFrequency);
        Assert.Equal(LeasePaymentTiming.InArrears, contract.PaymentTiming);
        Assert.Equal(LeaseContractStatus.Active, contract.Status);
    }

    [Fact]
    public void Create_TrimsContractNumberAndDescription()
    {
        var contract = LeaseContract.Create(
            Guid.NewGuid(), BookId, " LEASE-002 ", " Equipment lease ",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
            new Money(1_000m, "IRR"), 0m, LeasePaymentFrequency.Monthly,
            LeasePaymentTiming.InAdvance, LiabilityAccountId, InterestAccountId, PaymentAccountId);

        Assert.Equal("LEASE-002", contract.ContractNumber);
        Assert.Equal("Equipment lease", contract.Description);
    }

    [Fact]
    public void Create_RejectsNegativeEffectivePeriodicRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateContract(effectivePeriodicRate: -0.001m));
    }

    [Fact]
    public void Create_RejectsSameAccountForDifferentLeaseRoles()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateContract(interestAccountId: LiabilityAccountId));
    }

    [Fact]
    public void Create_RejectsArrearsPaymentOnCommencementDate()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateContract(firstPaymentDate: new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Create_AllowsAdvancePaymentOnCommencementDate()
    {
        var contract = CreateContract(
            firstPaymentDate: new DateOnly(2026, 1, 1),
            paymentTiming: LeasePaymentTiming.InAdvance);

        Assert.Equal(new DateOnly(2026, 1, 1), contract.FirstPaymentDate);
    }

    [Fact]
    public void Complete_ChangesOnlyAnActiveContract()
    {
        var contract = CreateContract();

        contract.Complete(new DateOnly(2027, 1, 1));

        Assert.Equal(LeaseContractStatus.Completed, contract.Status);
        Assert.Throws<ArtaFin.Domain.Common.DomainException>(() =>
            contract.Terminate(new DateOnly(2027, 1, 2)));
    }

    [Fact]
    public void Terminate_RejectsDateBeforeCommencement()
    {
        var contract = CreateContract();

        Assert.Throws<ArtaFin.Domain.Common.DomainException>(() =>
            contract.Terminate(new DateOnly(2025, 12, 31)));
    }
}
