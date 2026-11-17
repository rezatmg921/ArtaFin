using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Reports.GetCashFlowStatement;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class CashFlowStatementQueryHandlerTests
{
    [Fact]
    public async Task Handle_ComputesDirectMethodAndReconcilesCashIncludingInternalTransfers()
    {
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var book = FinancialBook.Create(bookId, ownerId, "Cash flow report", "USD", from.AddDays(-10));

        var opening = Entry(Guid.NewGuid(), from.AddDays(-1), "Opening contribution",
            Line(1, "1000", "Cash", CashFlowAccountType.Cash, PostingSide.Debit, 100m),
            Line(2, "3000", "Capital", CashFlowAccountType.None, PostingSide.Credit, 100m));
        var receipt = Entry(Guid.NewGuid(), from.AddDays(1), "Customer receipt",
            Line(1, "1000", "Cash", CashFlowAccountType.Cash, PostingSide.Debit, 50m),
            Line(2, "4000", "Revenue", CashFlowAccountType.None, PostingSide.Credit, 50m));
        var payment = Entry(Guid.NewGuid(), from.AddDays(2), "Supplier payment",
            Line(1, "6000", "Supplies", CashFlowAccountType.None, PostingSide.Debit, 30m),
            Line(2, "1000", "Cash", CashFlowAccountType.Cash, PostingSide.Credit, 30m));
        var financing = Entry(Guid.NewGuid(), from.AddDays(3), "Owner contribution",
            Line(1, "1000", "Cash", CashFlowAccountType.Cash, PostingSide.Debit, 20m),
            Line(2, "3000", "Capital", CashFlowAccountType.None, PostingSide.Credit, 20m));
        var transfer = Entry(Guid.NewGuid(), from.AddDays(4), "Cash to bank",
            Line(1, "1010", "Bank", CashFlowAccountType.Cash, PostingSide.Debit, 10m),
            Line(2, "1000", "Cash", CashFlowAccountType.Cash, PostingSide.Credit, 10m));

        var allocations = new[]
        {
            Allocation(opening, 1, 100m, CashFlowActivity.Financing, "OWNER_CONTRIBUTION", 2),
            Allocation(receipt, 1, 50m, CashFlowActivity.Operating, "CUSTOMER_RECEIPTS", 2),
            Allocation(payment, 2, 30m, CashFlowActivity.Operating, "OPERATING_SUPPLIER_PAYMENT", 1),
            Allocation(financing, 1, 20m, CashFlowActivity.Financing, "OWNER_CONTRIBUTION", 2)
        };

        var snapshot = new CashFlowStatementReadSnapshot(
            new[] { opening, receipt, payment, financing, transfer }.SelectMany(entry => entry).ToArray(),
            allocations,
            []);
        var handler = CreateHandler(ownerId, book, snapshot);

        var result = await handler.Handle(
            new GetCashFlowStatementQuery(bookId, from, to), CancellationToken.None);

        Assert.Equal(100m, result.OpeningCashAndCashEquivalents);
        Assert.Equal(140m, result.ClosingCashAndCashEquivalents);
        Assert.Equal(40m, result.NetIncreaseDecrease);
        Assert.Equal(0m, result.ReconciliationDifference);
        Assert.True(result.IsReconciled);
        Assert.Empty(result.Exceptions);

        var operating = Assert.Single(result.Activities, item => item.Activity == CashFlowActivity.Operating);
        Assert.Equal(50m, operating.CashReceipts);
        Assert.Equal(30m, operating.CashPayments);
        Assert.Equal(20m, operating.NetCashFlow);

        var financingResult = Assert.Single(result.Activities, item => item.Activity == CashFlowActivity.Financing);
        Assert.Equal(20m, financingResult.CashReceipts);
        Assert.Equal(0m, financingResult.CashPayments);
        Assert.Equal(20m, financingResult.NetCashFlow);
    }

    [Fact]
    public async Task Handle_ReportsDraftAllocationAsExceptionAndDoesNotIncludeItInCashFlows()
    {
        var ownerId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);
        var book = FinancialBook.Create(bookId, ownerId, "Unclassified cash flow", "USD", from.AddDays(-1));
        var entry = Entry(Guid.NewGuid(), from.AddDays(1), "Unclassified receipt",
            Line(1, "1000", "Cash", CashFlowAccountType.Cash, PostingSide.Debit, 15m),
            Line(2, "4000", "Revenue", CashFlowAccountType.None, PostingSide.Credit, 15m));

        var snapshot = new CashFlowStatementReadSnapshot(
            entry,
            [],
            [new CashFlowStatementDraftSnapshot(entry[0].JournalEntryId, 1)]);
        var handler = CreateHandler(ownerId, book, snapshot);

        var result = await handler.Handle(
            new GetCashFlowStatementQuery(bookId, from, to), CancellationToken.None);

        Assert.Equal(0m, result.NetIncreaseDecrease);
        Assert.Equal(15m, result.ClosingCashAndCashEquivalents);
        Assert.Equal(15m, result.ReconciliationDifference);
        Assert.False(result.IsReconciled);
        var exception = Assert.Single(result.Exceptions);
        Assert.Equal("ALLOCATION_DRAFT_NOT_FINALIZED", exception.Code);
    }

    private static GetCashFlowStatementQueryHandler CreateHandler(
        Guid ownerId,
        FinancialBook book,
        CashFlowStatementReadSnapshot snapshot) =>
        new(
            new TestCurrentUser(ownerId),
            new TestFinancialBookRepository(book),
            new TestCashFlowStatementReadRepository(snapshot),
            TimeProvider.System);

    private static CashFlowStatementLineSnapshot[] Entry(
        Guid entryId,
        DateTimeOffset postedAt,
        string description,
        params CashFlowStatementLineSnapshot[] lines) =>
        lines.Select(line => line with
        {
            JournalEntryId = entryId,
            PostedAt = postedAt,
            Description = description
        }).ToArray();

    private static CashFlowStatementLineSnapshot Line(
        int lineNumber,
        string accountCode,
        string accountName,
        CashFlowAccountType cashFlowAccountType,
        PostingSide side,
        decimal amount) =>
        new(
            Guid.Empty,
            default,
            string.Empty,
            lineNumber,
            Guid.NewGuid(),
            accountCode,
            accountName,
            cashFlowAccountType == CashFlowAccountType.None ? AccountType.Equity : AccountType.Asset,
            cashFlowAccountType,
            side,
            amount,
            "USD");

    private static CashFlowStatementAllocationSnapshot Allocation(
        CashFlowStatementLineSnapshot[] entry,
        int sourceLineNumber,
        decimal amount,
        CashFlowActivity activity,
        string category,
        int counterpartLineNumber) =>
        new(entry[0].JournalEntryId, sourceLineNumber, amount, activity, category, counterpartLineNumber);

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class TestCashFlowStatementReadRepository(
        CashFlowStatementReadSnapshot snapshot) : ICashFlowStatementReadRepository
    {
        public Task<CashFlowStatementReadSnapshot> GetBeforeAsync(
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
