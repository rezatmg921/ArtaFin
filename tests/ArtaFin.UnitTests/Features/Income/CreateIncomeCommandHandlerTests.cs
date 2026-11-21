using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Income.CreateIncome;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Income;

public sealed class CreateIncomeCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenOwnerRecordsIncome_PostsDebitToAssetAndCreditToIncomeAndCommits()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Income test", "USD", DateTimeOffset.UtcNow);
        var income = Account.Create(Guid.NewGuid(), book.Id, "4000", "Sales", AccountType.Income, DateTimeOffset.UtcNow);
        var deposit = Account.Create(Guid.NewGuid(), book.Id, "1000", "Bank", AccountType.Asset, DateTimeOffset.UtcNow);
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, [income, deposit], journal, unit);

        var id = await handler.Handle(new CreateIncomeCommand(book.Id, income.Id, deposit.Id,
            "Service revenue", new Money(250m, "USD")), CancellationToken.None);

        var entry = Assert.IsType<JournalEntry>(journal.Entry);
        Assert.Equal(id, entry.Id);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(deposit.Id, entry.Lines[0].AccountId);
        Assert.Equal(PostingSide.Debit, entry.Lines[0].Side);
        Assert.Equal(income.Id, entry.Lines[1].AccountId);
        Assert.Equal(PostingSide.Credit, entry.Lines[1].Side);
        Assert.Equal(250m, entry.Lines[0].Amount.Amount);
        Assert.Equal(1, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenIncomeAccountHasWrongType_RejectsWithoutWriting()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Income test", "USD", DateTimeOffset.UtcNow);
        var wrong = Account.Create(Guid.NewGuid(), book.Id, "1000", "Cash", AccountType.Asset, DateTimeOffset.UtcNow);
        var deposit = Account.Create(Guid.NewGuid(), book.Id, "1001", "Bank", AccountType.Asset, DateTimeOffset.UtcNow);
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, [wrong, deposit], journal, unit);

        await Assert.ThrowsAsync<IncomeAccountTypeInvalidException>(() =>
            handler.Handle(new CreateIncomeCommand(book.Id, wrong.Id, deposit.Id, "Revenue",
                new Money(10m, "USD")), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenDepositAccountHasWrongType_RejectsWithoutWriting()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Income test", "USD", DateTimeOffset.UtcNow);
        var income = Account.Create(Guid.NewGuid(), book.Id, "4000", "Sales", AccountType.Income, DateTimeOffset.UtcNow);
        var wrong = Account.Create(Guid.NewGuid(), book.Id, "2000", "Payable", AccountType.Liability, DateTimeOffset.UtcNow);
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, [income, wrong], journal, unit);

        await Assert.ThrowsAsync<IncomeDepositAccountTypeInvalidException>(() =>
            handler.Handle(new CreateIncomeCommand(book.Id, income.Id, wrong.Id, "Revenue",
                new Money(10m, "USD")), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenCurrencyDiffersFromBookBaseCurrency_RejectsWithoutWriting()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Income test", "USD", DateTimeOffset.UtcNow);
        var income = Account.Create(Guid.NewGuid(), book.Id, "4000", "Sales", AccountType.Income, DateTimeOffset.UtcNow);
        var deposit = Account.Create(Guid.NewGuid(), book.Id, "1000", "Bank", AccountType.Asset, DateTimeOffset.UtcNow);
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, [income, deposit], journal, unit);

        await Assert.ThrowsAsync<JournalEntryBaseCurrencyMismatchException>(() =>
            handler.Handle(new CreateIncomeCommand(book.Id, income.Id, deposit.Id, "Revenue",
                new Money(10m, "EUR")), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    private static CreateIncomeCommandHandler CreateHandler(
        Guid userId, FinancialBook book, Account[] accounts, FakeJournalRepository journal, FakeUnitOfWork unit) =>
        new(new FakeCurrentUser(userId), new FakeBookRepository(book),
            new FakeAccountRepository(accounts), journal, unit, TimeProvider.System);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class FakeBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<FinancialBook?>(id == book.Id ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid bookId, Guid userId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid bookId, Guid memberId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid bookId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(Account[] accounts) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(Guid bookId, Guid accountId, CancellationToken ct) =>
            Task.FromResult(accounts.SingleOrDefault(x => x.FinancialBookId == bookId && x.Id == accountId));
        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid bookId, Guid accountId, CancellationToken ct) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<AccountReadSnapshot?> GetForUpdateAsync(Guid bookId, Guid accountId, byte[]? version, CancellationToken ct) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<bool> ExistsByCodeAsync(Guid bookId, string code, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(Account account, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeJournalRepository : IJournalEntryRepository
    {
        public JournalEntry? Entry { get; private set; }
        public Task<JournalEntry?> GetByIdAsync(Guid bookId, Guid entryId, CancellationToken ct) =>
            Task.FromResult<JournalEntry?>(null);
        public Task<bool> HasReversalAsync(Guid bookId, Guid entryId, CancellationToken ct) => Task.FromResult(false);
        public Task AddAsync(JournalEntry entry, CancellationToken ct) { Entry = entry; return Task.CompletedTask; }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Calls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Calls++; return Task.FromResult(1); }
    }
}
