using ArtaFin.Domain.Common;
using ArtaFin.Domain.Obligations;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Obligations;

public sealed class FinancialObligationTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid ContactId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateOnly EffectiveDate = new(2026, 11, 1);
    private static readonly DateOnly DueDate = new(2026, 12, 1);
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    private static FinancialObligation Create(
        FinancialObligationDirection direction = FinancialObligationDirection.Receivable,
        decimal amount = 1250m,
        string currency = "USD",
        DateOnly? effectiveDate = null,
        DateOnly? dueDate = null) =>
        FinancialObligation.Create(
            Guid.NewGuid(), BookId, ContactId, AccountId, direction,
            FinancialObligationKind.GeneralDebt, amount, currency,
            effectiveDate ?? EffectiveDate, dueDate ?? DueDate,
            "  Invoice 2026-001  ", "  INV-001  ", Now);

    [Fact]
    public void Create_StoresNormalizedContractualTerms()
    {
        var obligation = Create();

        Assert.Equal(BookId, obligation.FinancialBookId);
        Assert.Equal(ContactId, obligation.ContactId);
        Assert.Equal(AccountId, obligation.ControlAccountId);
        Assert.Equal(FinancialObligationDirection.Receivable, obligation.Direction);
        Assert.Equal(FinancialObligationKind.GeneralDebt, obligation.Kind);
        Assert.Equal(1250m, obligation.OriginalAmount);
        Assert.Equal("USD", obligation.Currency);
        Assert.Equal(1250m, obligation.OriginalMoney.Amount);
        Assert.Equal(EffectiveDate, obligation.EffectiveDate);
        Assert.Equal(DueDate, obligation.DueDate);
        Assert.Equal("Invoice 2026-001", obligation.Description);
        Assert.Equal("INV-001", obligation.Reference);
        Assert.Equal(FinancialObligationStatus.Open, obligation.Status);
        Assert.Equal(Now, obligation.CreatedAt);
        Assert.Equal(Now, obligation.UpdatedAt);
    }

    [Fact]
    public void Create_SupportsPayableDirection()
    {
        var obligation = Create(FinancialObligationDirection.Payable);

        Assert.Equal(FinancialObligationDirection.Payable, obligation.Direction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositiveAmount(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(amount: amount));

    [Fact]
    public void Create_RejectsUnsupportedCurrencyPrecision() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(amount: 1.001m, currency: "USD"));

    [Fact]
    public void Create_RejectsDueDateBeforeEffectiveDate() =>
        Assert.Throws<ArgumentException>(() => Create(
            effectiveDate: new DateOnly(2026, 12, 2),
            dueDate: new DateOnly(2026, 12, 1)));

    [Fact]
    public void Create_RejectsUndefinedDirection() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FinancialObligation.Create(
            Guid.NewGuid(), BookId, ContactId, AccountId, (FinancialObligationDirection)99,
            FinancialObligationKind.GeneralDebt, 10m, "USD",
            EffectiveDate, DueDate, "Debt", null, Now));

    [Fact]
    public void IsOverdueOn_IsDerivedFromDueDateAndSettlementState()
    {
        var obligation = Create();

        Assert.False(obligation.IsOverdueOn(DueDate));
        Assert.True(obligation.IsOverdueOn(DueDate.AddDays(1)));
        obligation.MarkSettled(Now.AddDays(1));
        Assert.False(obligation.IsOverdueOn(DueDate.AddDays(2)));
    }

    [Fact]
    public void MarkPartiallySettled_TransitionsOnlyFromOpen()
    {
        var obligation = Create();

        obligation.MarkPartiallySettled(Now.AddHours(1));

        Assert.Equal(FinancialObligationStatus.PartiallySettled, obligation.Status);
        Assert.Throws<DomainException>(() => obligation.MarkPartiallySettled(Now.AddHours(2)));
    }

    [Fact]
    public void ApplyPayment_UpdatesStatusAcrossRepeatedPartialPayments()
    {
        var obligation = Create();

        obligation.ApplyPayment(300m, Now.AddHours(1));
        Assert.Equal(FinancialObligationStatus.PartiallySettled, obligation.Status);
        Assert.Equal(Now.AddHours(1), obligation.UpdatedAt);

        obligation.ApplyPayment(900m, Now.AddHours(2));
        Assert.Equal(FinancialObligationStatus.PartiallySettled, obligation.Status);
        Assert.Equal(Now.AddHours(2), obligation.UpdatedAt);

        obligation.ApplyPayment(1250m, Now.AddHours(3));
        Assert.Equal(FinancialObligationStatus.Settled, obligation.Status);
        Assert.Equal(Now.AddHours(3), obligation.UpdatedAt);
    }

    [Fact]
    public void ApplyPayment_RejectsOverpaymentAndCancelledObligation()
    {
        var obligation = Create();
        Assert.Throws<DomainException>(() => obligation.ApplyPayment(1251m, Now.AddHours(1)));

        obligation.Cancel(Now.AddHours(1));
        Assert.Throws<DomainException>(() => obligation.ApplyPayment(1m, Now.AddHours(2)));
    }

    [Fact]
    public void RecalculateSettlementAfterReversal_ReflectsRemainingValidPayments()
    {
        var obligation = Create();

        obligation.ApplyPayment(1250m, Now.AddHours(1));
        obligation.RecalculateSettlementAfterReversal(400m, Now.AddHours(2));
        Assert.Equal(FinancialObligationStatus.PartiallySettled, obligation.Status);

        obligation.RecalculateSettlementAfterReversal(0m, Now.AddHours(3));
        Assert.Equal(FinancialObligationStatus.Open, obligation.Status);
        Assert.Equal(Now.AddHours(3), obligation.UpdatedAt);
    }

    [Fact]
    public void RecalculateSettlementAfterReversal_RejectsCancelledObligation()
    {
        var obligation = Create();
        obligation.Cancel(Now.AddHours(1));

        Assert.Throws<DomainException>(() =>
            obligation.RecalculateSettlementAfterReversal(0m, Now.AddHours(2)));
    }

    [Fact]
    public void MarkSettled_AllowsOpenOrPartiallySettled()
    {
        var open = Create();
        open.MarkSettled(Now.AddHours(1));
        Assert.Equal(FinancialObligationStatus.Settled, open.Status);

        var partial = Create();
        partial.MarkPartiallySettled(Now.AddHours(1));
        partial.MarkSettled(Now.AddHours(2));
        Assert.Equal(FinancialObligationStatus.Settled, partial.Status);
    }

    [Fact]
    public void Cancel_OnlyAllowsOpenObligation()
    {
        var obligation = Create();

        obligation.Cancel(Now.AddHours(1));

        Assert.Equal(FinancialObligationStatus.Cancelled, obligation.Status);
        Assert.Equal(Now.AddHours(1), obligation.CancelledAt);
        Assert.Throws<DomainException>(() => obligation.Cancel(Now.AddHours(2)));
    }

    [Fact]
    public void Create_WithoutInstallmentTerms_CreatesOneFullAmountInstallment()
    {
        var obligation = Create();

        var installment = Assert.Single(obligation.Installments);
        Assert.Equal(1, installment.InstallmentNumber);
        Assert.Equal(DueDate, installment.DueDate);
        Assert.Equal(obligation.OriginalAmount, installment.Amount);
    }

    [Fact]
    public void Create_WithInstallmentTerms_PreservesOrderedSchedule()
    {
        var terms = new[]
        {
            new FinancialObligationInstallmentTerm(500m, new DateOnly(2026, 11, 15)),
            new FinancialObligationInstallmentTerm(750m, DueDate)
        };

        var obligation = FinancialObligation.Create(
            Guid.NewGuid(), BookId, ContactId, AccountId,
            FinancialObligationDirection.Receivable, FinancialObligationKind.GeneralDebt,
            1250m, "USD", EffectiveDate, DueDate, "Invoice", null, Now, terms);

        Assert.Collection(obligation.Installments,
            first =>
            {
                Assert.Equal(1, first.InstallmentNumber);
                Assert.Equal(new DateOnly(2026, 11, 15), first.DueDate);
                Assert.Equal(500m, first.Amount);
            },
            second =>
            {
                Assert.Equal(2, second.InstallmentNumber);
                Assert.Equal(DueDate, second.DueDate);
                Assert.Equal(750m, second.Amount);
            });
        Assert.Equal(obligation.OriginalAmount, obligation.Installments.Sum(x => x.Amount));
    }

    [Fact]
    public void Create_RejectsInstallmentsWhoseTotalDoesNotMatchObligation()
    {
        var terms = new[]
        {
            new FinancialObligationInstallmentTerm(500m, new DateOnly(2026, 11, 15)),
            new FinancialObligationInstallmentTerm(700m, DueDate)
        };

        Assert.Throws<ArgumentException>(() => FinancialObligation.Create(
            Guid.NewGuid(), BookId, ContactId, AccountId,
            FinancialObligationDirection.Receivable, FinancialObligationKind.GeneralDebt,
            1250m, "USD", EffectiveDate, DueDate, "Invoice", null, Now, terms));
    }

    [Fact]
    public void Create_RejectsInstallmentsNotInStrictDueDateOrder()
    {
        var terms = new[]
        {
            new FinancialObligationInstallmentTerm(500m, DueDate),
            new FinancialObligationInstallmentTerm(750m, DueDate)
        };

        Assert.Throws<ArgumentException>(() => FinancialObligation.Create(
            Guid.NewGuid(), BookId, ContactId, AccountId,
            FinancialObligationDirection.Receivable, FinancialObligationKind.GeneralDebt,
            1250m, "USD", EffectiveDate, DueDate, "Invoice", null, Now, terms));
    }

}
