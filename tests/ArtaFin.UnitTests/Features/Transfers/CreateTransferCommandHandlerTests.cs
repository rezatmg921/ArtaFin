using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Application.Features.Transfers.CreateTransfer;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Transfers;

public sealed class CreateTransferCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid SourceId = Guid.NewGuid();
    private static readonly Guid DestinationId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerTransfersBetweenAssetAccounts_PostsBalancedEntryAndCommits()
    {
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [CreateAccount(SourceId, AccountType.Asset), CreateAccount(DestinationId, AccountType.Asset)],
            journal, unit);

        var id = await handler.Handle(CreateCommand(), CancellationToken.None);

        var entry = Assert.IsType<JournalEntry>(journal.Entry);
        Assert.Equal(id, entry.Id);
        Assert.Equal(BookId, entry.FinancialBookId);
        Assert.Equal(Now, entry.PostedAt);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(DestinationId, entry.Lines[0].AccountId);
        Assert.Equal(PostingSide.Debit, entry.Lines[0].Side);
        Assert.Equal(SourceId, entry.Lines[1].AccountId);
        Assert.Equal(PostingSide.Credit, entry.Lines[1].Side);
        Assert.Equal(125m, entry.Lines[0].Amount.Amount);
        Assert.Equal("USD", entry.Lines[0].Amount.Currency);
        Assert.Equal(1, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenUserCannotPost_RejectsWithoutWriting()
    {
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(Guid.NewGuid(),
            [CreateAccount(SourceId, AccountType.Asset), CreateAccount(DestinationId, AccountType.Asset)],
            journal, unit);

        await Assert.ThrowsAsync<JournalEntryAccessDeniedException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenAnAccountIsNotAsset_RejectsWithoutWriting()
    {
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [CreateAccount(SourceId, AccountType.Asset), CreateAccount(DestinationId, AccountType.Expense)],
            journal, unit);

        await Assert.ThrowsAsync<TransferAccountTypeInvalidException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenCurrencyDiffersFromBookBaseCurrency_RejectsWithoutWriting()
    {
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [CreateAccount(SourceId, AccountType.Asset), CreateAccount(DestinationId, AccountType.Asset)],
            journal, unit);

        await Assert.ThrowsAsync<JournalEntryBaseCurrencyMismatchException>(() =>
            handler.Handle(CreateCommand(new Money(125m, "EUR")), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenAccountIsInactive_RejectsWithoutWriting()
    {
        var source = CreateAccount(SourceId, AccountType.Asset);
        source.Deactivate(Now.AddMinutes(-1));
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [source, CreateAccount(DestinationId, AccountType.Asset)], journal, unit);

        await Assert.ThrowsAsync<JournalEntryAccountUnavailableException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    [Fact]
    public async Task Handle_WhenAccountIdsAreTheSame_RejectsWithoutWriting()
    {
        var journal = new FakeJournalRepository();
        var unit = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId, [CreateAccount(SourceId, AccountType.Asset)], journal, unit);

        await Assert.ThrowsAsync<TransferAccountsMustBeDifferentException>(() =>
            handler.Handle(new CreateTransferCommand(BookId, SourceId, SourceId, "Internal transfer",
                new Money(125m, "USD")), CancellationToken.None));
        Assert.Null(journal.Entry);
        Assert.Equal(0, unit.Calls);
    }

    private static CreateTransferCommand CreateCommand(Money? amount = null) =>
        new(BookId, SourceId, DestinationId, "Internal transfer", amount ?? new Money(125m, "USD"));

    private static Account CreateAccount(Guid id, AccountType type) =>
        Account.Create(id, BookId, id == SourceId ? "1000" : "1010", "Test asset",
            type, Now.AddDays(-1));

    private static CreateTransferCommandHandler CreateHandler(
        Guid userId, IReadOnlyCollection<Account> accounts,
        FakeJournalRepository journal, FakeUnitOfWork unit) =>
        new(new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(FinancialBook.Create(
                BookId, OwnerId, "Transfer test", "USD", Now.AddYears(-1))),
            new FakeAccountRepository(accounts), journal, unit, new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<FinancialBook?>(id == book.Id ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid bookId, Guid userId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid bookId, Guid membershipId, CancellationToken ct) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid bookId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(IReadOnlyCollection<Account> accounts) : IAccountRepository
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
