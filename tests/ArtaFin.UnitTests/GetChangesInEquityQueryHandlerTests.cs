using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Reports.GetChangesInEquity;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class GetChangesInEquityQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOpeningMovementsClosingAndUnclosedIncome()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Equity report test", "USD",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var accountId = Guid.NewGuid();
        var repository = new FakeChangesInEquityReadRepository(new ChangesInEquitySnapshot(
        [
            new ChangesInEquityAccountSnapshot(
                accountId, "3000", "Capital", AccountType.Equity,
                0m, 100m, 0m, 30m, 0m, 130m,
                30m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
            new ChangesInEquityAccountSnapshot(
                Guid.NewGuid(), "4000", "Sales", AccountType.Income,
                0m, 20m, 0m, 50m, 0m, 70m,
                0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
            new ChangesInEquityAccountSnapshot(
                Guid.NewGuid(), "6000", "Rent", AccountType.Expense,
                5m, 0m, 15m, 0m, 20m, 0m,
                0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m)
        ], 15m, 50m));
        var from = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var handler = new GetChangesInEquityQueryHandler(
            new TestCurrentUser(ownerId),
            new FakeFinancialBookRepository(book),
            repository,
            TimeProvider.System);

        var result = await handler.Handle(
            new GetChangesInEquityQuery(book.Id, from, to),
            CancellationToken.None);

        var component = Assert.Single(result.Components);
        Assert.Equal(accountId, component.AccountId);
        Assert.Equal(100m, component.OpeningBalance);
        Assert.Equal(30m, component.Increases);
        Assert.Equal(0m, component.Decreases);
        Assert.Equal(130m, component.ClosingBalance);
        Assert.Equal(100m, result.OpeningEquity);
        Assert.Equal(30m, result.TotalIncreases);
        Assert.Equal(0m, result.TotalDecreases);
        Assert.Equal(130m, result.ClosingEquity);
        Assert.Equal(15m, result.OpeningUnclosedNetIncome);
        Assert.Equal(50m, result.ClosingUnclosedNetIncome);
        Assert.Equal(115m, result.OpeningTotalEquity);
        Assert.Equal(180m, result.ClosingTotalEquity);
        Assert.Equal(from, repository.LastFrom);
        Assert.Equal(to, repository.LastTo);
    }

    [Fact]
    public async Task Handle_RejectsInvalidPeriod()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Equity report test", "USD",
            DateTimeOffset.UtcNow);
        var repository = new FakeChangesInEquityReadRepository(
            new ChangesInEquitySnapshot([], 0m, 0m));
        var handler = new GetChangesInEquityQueryHandler(
            new TestCurrentUser(ownerId),
            new FakeFinancialBookRepository(book),
            repository,
            TimeProvider.System);
        var date = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetChangesInEquityQuery(book.Id, date, date), CancellationToken.None));

        Assert.Null(repository.LastFrom);
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<FinancialBook?>(id == book.Id ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid bookId, Guid userId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid bookId, Guid memberId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid bookId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeChangesInEquityReadRepository(ChangesInEquitySnapshot snapshot)
        : IChangesInEquityReadRepository
    {
        public DateTimeOffset? LastFrom { get; private set; }
        public DateTimeOffset? LastTo { get; private set; }

        public Task<ChangesInEquitySnapshot> GetPeriodAsync(
            Guid financialBookId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            LastFrom = from;
            LastTo = to;
            return Task.FromResult(snapshot);
        }
    }
}
