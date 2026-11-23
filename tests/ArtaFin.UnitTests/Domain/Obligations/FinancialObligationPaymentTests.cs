using ArtaFin.Domain.Obligations;
using Xunit;

namespace ArtaFin.UnitTests.Domain.Obligations;

public sealed class FinancialObligationPaymentTests
{
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid ObligationId = Guid.NewGuid();
    private static readonly Guid CashAccountId = Guid.NewGuid();
    private static readonly Guid JournalEntryId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FirstInstallmentId = Guid.NewGuid();
    private static readonly Guid SecondInstallmentId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static FinancialObligationPayment Create(
        IReadOnlyList<FinancialObligationPaymentAllocationTerm>? allocations = null) =>
        FinancialObligationPayment.Create(
            Guid.NewGuid(), BookId, ObligationId, CashAccountId, JournalEntryId,
            100m, "USD", new DateOnly(2026, 11, 23), "Installment payment",
            "RCPT-001", UserId, Now, "payment-001", new string('A', 64),
            allocations ?? [
                new FinancialObligationPaymentAllocationTerm(FirstInstallmentId, 40m),
                new FinancialObligationPaymentAllocationTerm(SecondInstallmentId, 60m)
            ]);

    [Fact]
    public void Create_PreservesPaymentAndInstallmentAllocations()
    {
        var payment = Create();

        Assert.Equal(BookId, payment.FinancialBookId);
        Assert.Equal(ObligationId, payment.FinancialObligationId);
        Assert.Equal(CashAccountId, payment.CashAccountId);
        Assert.Equal(JournalEntryId, payment.JournalEntryId);
        Assert.Equal(100m, payment.PaymentMoney.Amount);
        Assert.Equal("USD", payment.Currency);
        Assert.Equal("Installment payment", payment.Description);
        Assert.Equal("RCPT-001", payment.Reference);
        Assert.Equal(UserId, payment.CreatedBy);
        Assert.Equal("payment-001", payment.IdempotencyKey);
        Assert.Equal(new string('A', 64), payment.RequestHash);
        Assert.Collection(payment.Allocations,
            first =>
            {
                Assert.Equal(FirstInstallmentId, first.InstallmentId);
                Assert.Equal(40m, first.Amount);
            },
            second =>
            {
                Assert.Equal(SecondInstallmentId, second.InstallmentId);
                Assert.Equal(60m, second.Amount);
            });
    }

    [Fact]
    public void Create_RequiresAllocationsToEqualPaymentAmount()
    {
        Assert.Throws<ArgumentException>(() => Create([
            new FinancialObligationPaymentAllocationTerm(FirstInstallmentId, 99m)
        ]));
    }

    [Fact]
    public void Create_RejectsDuplicateInstallmentAllocations()
    {
        Assert.Throws<ArgumentException>(() => Create([
            new FinancialObligationPaymentAllocationTerm(FirstInstallmentId, 40m),
            new FinancialObligationPaymentAllocationTerm(FirstInstallmentId, 60m)
        ]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositiveAllocation(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create([
            new FinancialObligationPaymentAllocationTerm(FirstInstallmentId, amount),
            new FinancialObligationPaymentAllocationTerm(SecondInstallmentId, 100m - amount)
        ]));
    }

    [Fact]
    public void Create_RequiresValidIdempotencyMetadata()
    {
        Assert.Throws<ArgumentException>(() => FinancialObligationPayment.Create(
            Guid.NewGuid(), BookId, ObligationId, CashAccountId, JournalEntryId,
            100m, "USD", new DateOnly(2026, 11, 23), "Payment", null,
            UserId, Now, "", new string('A', 64),
            [new FinancialObligationPaymentAllocationTerm(FirstInstallmentId, 100m)]));
    }
}
