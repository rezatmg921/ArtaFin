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
    public void MarkReversed_RecordsAuditMetadataWithoutChangingOriginalPayment()
    {
        var payment = Create();
        var reversalId = Guid.NewGuid();
        var reversedAt = Now.AddHours(1);

        payment.MarkReversed(reversalId, UserId, reversedAt, "Duplicate receipt", "REVERSAL-001", new string('D', 64));

        Assert.Equal(100m, payment.Amount);
        Assert.Equal(JournalEntryId, payment.JournalEntryId);
        Assert.Equal(reversalId, payment.ReversalJournalEntryId);
        Assert.Equal(UserId, payment.ReversedBy);
        Assert.Equal(reversedAt, payment.ReversedAt);
        Assert.Equal("Duplicate receipt", payment.ReversalReason);
        Assert.Equal("REVERSAL-001", payment.ReversalIdempotencyKey);
        Assert.Equal(new string('D', 64), payment.ReversalRequestHash);
        Assert.Equal(2, payment.Allocations.Count);
    }

    [Fact]
    public void MarkReversed_RejectsSecondReversal()
    {
        var payment = Create();
        payment.MarkReversed(Guid.NewGuid(), UserId, Now.AddHours(1), "Duplicate", "REVERSAL-001", new string('D', 64));

        Assert.Throws<ArtaFin.Domain.Common.DomainException>(() =>
            payment.MarkReversed(Guid.NewGuid(), UserId, Now.AddHours(2), "Second", "REVERSAL-002", new string('E', 64)));
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
