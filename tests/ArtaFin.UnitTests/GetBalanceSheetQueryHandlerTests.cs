using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Reports.GetBalanceSheet;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class GetBalanceSheetQueryHandlerTests
{
    [Fact]
    public async Task Handle_CalculatesAssetsLiabilitiesEquityAndUnclosedNetIncome()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var asOf = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var handler = CreateHandler(book, ownerId,
        [
            Snapshot("1000", AccountType.Asset, 200m, 0m),
            Snapshot("2000", AccountType.Liability, 0m, 50m),
            Snapshot("3000", AccountType.Equity, 0m, 30m),
            Snapshot("4000", AccountType.Income, 0m, 150m),
            Snapshot("6000", AccountType.Expense, 30m, 0m)
        ], asOf);

        var result = await handler.Handle(
            new GetBalanceSheetQuery(book.Id, asOf), CancellationToken.None);

        Assert.Equal(asOf, result.AsOf);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(200m, result.TotalAssets);
        Assert.Equal(50m, result.TotalLiabilities);
        Assert.Equal(30m, result.Equity.Sum(x => x.Amount));
        Assert.Equal(120m, result.UnclosedNetIncome);
        Assert.Equal(150m, result.TotalEquity);
        Assert.Equal(200m, result.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, result.Difference);
    }

    [Fact]
    public async Task Handle_UsesOnlyAssetLiabilityAndEquityForTheirSections()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var handler = CreateHandler(book, ownerId,
        [
            Snapshot("1000", AccountType.Asset, 100m, 0m),
            Snapshot("2000", AccountType.Liability, 0m, 40m),
            Snapshot("3000", AccountType.Equity, 0m, 60m),
            Snapshot("4000", AccountType.Income, 0m, 20m),
            Snapshot("6000", AccountType.Expense, 20m, 0m)
        ], TimeProvider.System.GetUtcNow());

        var result = await handler.Handle(
            new GetBalanceSheetQuery(book.Id, null), CancellationToken.None);

        Assert.Single(result.Assets);
        Assert.Single(result.Liabilities);
        Assert.Single(result.Equity);
        Assert.Equal(0m, result.UnclosedNetIncome);
        Assert.Equal(100m, result.TotalAssets);
        Assert.Equal(100m, result.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, result.Difference);
    }

    [Fact]
    public async Task Handle_OffsetsContraAssetAndIncludesUnclosedEarningsWithoutDoubleCountingEquity()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var asOf = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        var handler = CreateHandler(book, ownerId,
        [
            // Debit-normal asset with a credit balance (for example, accumulated depreciation).
            Snapshot("1000", AccountType.Asset, 175m, 20m),
            Snapshot("2000", AccountType.Liability, 5m, 40m),
            // Equity already contains previously closed/retained earnings.
            Snapshot("3000", AccountType.Equity, 10m, 55m),
            // Current-period earnings have not yet been closed.
            Snapshot("4000", AccountType.Income, 0m, 100m),
            Snapshot("6000", AccountType.Expense, 25m, 0m)
        ], asOf);

        var result = await handler.Handle(
            new GetBalanceSheetQuery(book.Id, asOf), CancellationToken.None);

        Assert.Equal(155m, result.TotalAssets);
        Assert.Equal(35m, result.TotalLiabilities);
        Assert.Equal(45m, result.Equity.Sum(account => account.Amount));
        Assert.Equal(75m, result.UnclosedNetIncome);
        Assert.Equal(120m, result.TotalEquity);
        Assert.Equal(155m, result.TotalLiabilitiesAndEquity);
        Assert.Equal(0m, result.Difference);
    }

    [Fact]
    public async Task Handle_UsesCurrentTimeWhenAsOfIsOmitted()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var now = new DateTimeOffset(2026, 11, 27, 9, 30, 0, TimeSpan.Zero);
        var repository = new FakeBalanceSheetReadRepository([]);
        var handler = new GetBalanceSheetQueryHandler(
            new TestCurrentUser(ownerId),
            new FakeFinancialBookRepository(book),
            repository,
            new FixedTimeProvider(now));

        var result = await handler.Handle(
            new GetBalanceSheetQuery(book.Id, null), CancellationToken.None);

        Assert.Equal(now, result.AsOf);
        Assert.Equal(now, repository.LastAsOf);
    }

    [Fact]
    public async Task Handle_RejectsUserWithoutBookAccess()
    {
        var book = CreateBook(Guid.NewGuid());
        var handler = CreateHandler(book, Guid.NewGuid(), [], TimeProvider.System.GetUtcNow());

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(new GetBalanceSheetQuery(book.Id, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RejectsEmptyFinancialBookId()
    {
        var handler = CreateHandler(
            CreateBook(Guid.NewGuid()), Guid.NewGuid(), [], TimeProvider.System.GetUtcNow());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetBalanceSheetQuery(Guid.Empty, null), CancellationToken.None));
    }

    private static BalanceSheetAccountSnapshot Snapshot(
        string code, AccountType type, decimal debits, decimal credits) =>
        new(Guid.NewGuid(), code, code, type, debits, credits);

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Balance sheet unit test", "USD",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static GetBalanceSheetQueryHandler CreateHandler(
        FinancialBook book,
        Guid currentUserId,
        IReadOnlyList<BalanceSheetAccountSnapshot> snapshots,
        DateTimeOffset now) =>
        new(new TestCurrentUser(currentUserId),
            new FakeFinancialBookRepository(book),
            new FakeBalanceSheetReadRepository(snapshots),
            new FixedTimeProvider(now));

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(id == book.Id ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(Guid bookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid bookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid bookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);

        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeBalanceSheetReadRepository(
        IReadOnlyList<BalanceSheetAccountSnapshot> snapshots) : IBalanceSheetReadRepository
    {
        public DateTimeOffset? LastAsOf { get; private set; }

        public Task<IReadOnlyList<BalanceSheetAccountSnapshot>> GetAsOfAsync(
            Guid financialBookId, DateTimeOffset asOf, CancellationToken cancellationToken)
        {
            LastAsOf = asOf;
            return Task.FromResult(snapshots);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
