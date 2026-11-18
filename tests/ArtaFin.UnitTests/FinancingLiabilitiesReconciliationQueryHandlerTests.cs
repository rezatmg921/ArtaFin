using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Reports.GetFinancingLiabilitiesReconciliation;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class FinancingLiabilitiesReconciliationQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReconcilesLeaseRecognitionAndCashRepaymentSeparately()
    {
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var book = FinancialBook.Create(bookId, ownerId, "Financing liabilities", "IRR", from.AddDays(-1));
        var liabilityAccountId = Guid.NewGuid();

        var leaseRecognitionId = Guid.NewGuid();
        var leaseRecognition = new[]
        {
            Line(leaseRecognitionId, from.AddDays(1), 1, Guid.NewGuid(), "1501", "Equipment",
                FinancingLiabilityType.None, CashFlowAccountType.None, AccountType.Asset,
                PostingSide.Debit, 80_000_000m, "IRR"),
            Line(leaseRecognitionId, from.AddDays(1), 2, Guid.NewGuid(), "2301", "Lease liability",
                FinancingLiabilityType.LeaseLiabilities, CashFlowAccountType.None, AccountType.Liability,
                PostingSide.Credit, 80_000_000m, "IRR")
        };

        var repaymentId = Guid.NewGuid();
        var repayment = new[]
        {
            Line(repaymentId, from.AddDays(2), 1, liabilityAccountId, "2301", "Lease liability",
                FinancingLiabilityType.LeaseLiabilities, CashFlowAccountType.None, AccountType.Liability,
                PostingSide.Debit, 10_000_000m, "IRR"),
            Line(repaymentId, from.AddDays(2), 2, Guid.NewGuid(), "1001", "Bank",
                FinancingLiabilityType.None, CashFlowAccountType.Cash, AccountType.Asset,
                PostingSide.Credit, 10_000_000m, "IRR")
        };

        // A non-cash lease recognition is disclosed on the liability line.
        // The later bank payment is allocated to the liability counterpart line.
        var snapshot = new FinancingLiabilitiesReconciliationReadSnapshot(
            leaseRecognition.Concat(repayment).ToArray(),
            [new FinancingLiabilityCashAllocationSnapshot(repaymentId, 2, 1, 10_000_000m)],
            [new FinancingLiabilityNonCashDisclosureSnapshot(
                leaseRecognitionId, 2, 80_000_000m, "IRR", "ASSET_ACQUIRED_THROUGH_LEASE")]);

        var handler = new GetFinancingLiabilitiesReconciliationQueryHandler(
            new TestCurrentUser(ownerId),
            new TestFinancialBookRepository(book),
            new TestReadRepository(snapshot),
            TimeProvider.System);

        var result = await handler.Handle(
            new GetFinancingLiabilitiesReconciliationQuery(bookId, from, to),
            CancellationToken.None);

        var account = Assert.Single(result.Accounts);
        Assert.Equal(0m, account.OpeningBalance);
        Assert.Equal(-10_000_000m, account.CashChanges);
        Assert.Equal(80_000_000m, account.NonCashChanges);
        Assert.Equal(0m, account.UnclassifiedChanges);
        Assert.Equal(70_000_000m, account.ClosingBalance);
        Assert.Equal(0m, account.ReconciliationDifference);
        Assert.Equal(70_000_000m, result.TotalClosingBalance);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Exceptions);
    }

    [Fact]
    public void Account_RejectsFinancingClassificationForNonLiability()
    {
        var asset = Account.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1501", "Equipment", AccountType.Asset,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() =>
            asset.ConfigureFinancingLiabilityClassification(FinancingLiabilityType.LeaseLiabilities));
    }

    private static FinancingLiabilityLineSnapshot Line(
        Guid entryId,
        DateTimeOffset postedAt,
        int lineNumber,
        Guid accountId,
        string code,
        string name,
        FinancingLiabilityType financingType,
        CashFlowAccountType cashType,
        AccountType accountType,
        PostingSide side,
        decimal amount,
        string currency) =>
        new(entryId, postedAt, "Lease transaction", lineNumber, accountId, code, name,
            financingType, cashType, side, amount, currency);

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class TestReadRepository(
        FinancingLiabilitiesReconciliationReadSnapshot snapshot)
        : IFinancingLiabilitiesReconciliationReadRepository
    {
        public Task<FinancingLiabilitiesReconciliationReadSnapshot> GetBeforeAsync(
            Guid financialBookId,
            DateTimeOffset to,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class TestFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(financialBookId == book.Id ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid financialBookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);

        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
