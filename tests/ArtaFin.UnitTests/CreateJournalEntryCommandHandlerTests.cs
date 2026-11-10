using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class CreateJournalEntryCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid DebitAccountId = Guid.NewGuid();
    private static readonly Guid CreditAccountId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenUserOwnsBook_CreatesAndCommitsJournalEntry()
    {
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(CreateBook(OwnerId), OwnerId,
            [CreateAccount(DebitAccountId), CreateAccount(CreditAccountId)], journalRepository, unitOfWork);

        var id = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        var entry = Assert.IsType<JournalEntry>(journalRepository.AddedEntry);
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(id, entry.Id);
        Assert.Equal(BookId, entry.FinancialBookId);
        Assert.Equal("Office supplies", entry.Description);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(Now, entry.PostedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenActiveAccountant_CreatesAndCommitsJournalEntry()
    {
        var member = FinancialBookMember.Grant(Guid.NewGuid(), BookId, OwnerId,
            FinancialBookRole.Accountant, Now.AddDays(-1));
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(CreateBook(Guid.NewGuid()), OwnerId,
            [CreateAccount(DebitAccountId), CreateAccount(CreditAccountId)],
            journalRepository, unitOfWork, member);

        await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.NotNull(journalRepository.AddedEntry);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(FinancialBookRole.Auditor)]
    [InlineData(FinancialBookRole.Viewer)]
    public async Task HandleAsync_WhenMemberCannotPost_RejectsWithoutWriting(FinancialBookRole role)
    {
        var member = FinancialBookMember.Grant(Guid.NewGuid(), BookId, OwnerId, role, Now.AddDays(-1));
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(CreateBook(Guid.NewGuid()), OwnerId,
            [CreateAccount(DebitAccountId), CreateAccount(CreditAccountId)],
            journalRepository, unitOfWork, member);

        await Assert.ThrowsAsync<JournalEntryAccessDeniedException>(() =>
            handler.HandleAsync(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenAccountIsNotInBook_RejectsWithoutWriting()
    {
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(CreateBook(OwnerId), OwnerId,
            [CreateAccount(DebitAccountId)], journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryAccountUnavailableException>(() =>
            handler.HandleAsync(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenAccountIsInactive_RejectsWithoutWriting()
    {
        var inactive = CreateAccount(CreditAccountId);
        inactive.Deactivate(Now.AddHours(-1));
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(CreateBook(OwnerId), OwnerId,
            [CreateAccount(DebitAccountId), inactive], journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryAccountUnavailableException>(() =>
            handler.HandleAsync(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenLinesAreUnbalanced_RejectsWithoutWriting()
    {
        var handler = CreateHandler(CreateBook(OwnerId), OwnerId,
            [CreateAccount(DebitAccountId), CreateAccount(CreditAccountId)],
            new FakeJournalEntryRepository(), new FakeUnitOfWork());
        var command = new CreateJournalEntryCommand(BookId, "Unbalanced",
        [
            new JournalEntryLineCommand(DebitAccountId, PostingSide.Debit, new Money(10m, "EUR")),
            new JournalEntryLineCommand(CreditAccountId, PostingSide.Credit, new Money(9m, "EUR"))
        ]);

        await Assert.ThrowsAsync<ArtaFin.Domain.Common.DomainException>(() =>
            handler.HandleAsync(command, CancellationToken.None));
    }

    private static CreateJournalEntryCommand CreateCommand() =>
        new(BookId, " Office supplies ",
        [
            new JournalEntryLineCommand(DebitAccountId, PostingSide.Debit, new Money(25m, "EUR")),
            new JournalEntryLineCommand(CreditAccountId, PostingSide.Credit, new Money(25m, "EUR"))
        ]);

    private static CreateJournalEntryCommandHandler CreateHandler(
        FinancialBook book, Guid userId, IReadOnlyCollection<Account> accounts,
        FakeJournalEntryRepository journalRepository, FakeUnitOfWork unitOfWork,
        FinancialBookMember? member = null) =>
        new(new FakeCurrentUser(userId), new FakeFinancialBookRepository(book, member),
            new FakeAccountRepository(accounts), journalRepository, unitOfWork, new FixedTimeProvider(Now));

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(BookId, ownerId, "Business", "EUR", Now.AddYears(-1));

    private static Account CreateAccount(Guid id) =>
        Account.Create(id, BookId, id.ToString("N"), "Test account", AccountType.Asset, Now.AddYears(-1));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book, FinancialBookMember? member)
        : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(member?.FinancialBookId == financialBookId && member.UserId == userId ? member : null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(Array.Empty<FinancialBookMemberSummary>());
        public Task AddMemberAsync(FinancialBookMember value, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(IReadOnlyCollection<Account> accounts) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(x => x.FinancialBookId == financialBookId && x.Id == accountId));

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<bool> ExistsByCodeAsync(Guid financialBookId, string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task AddAsync(Account account, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeJournalEntryRepository : IJournalEntryRepository
    {
        public JournalEntry? AddedEntry { get; private set; }

        public Task<JournalEntry?> GetByIdAsync(
            Guid financialBookId,
            Guid journalEntryId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                AddedEntry is not null &&
                AddedEntry.FinancialBookId == financialBookId &&
                AddedEntry.Id == journalEntryId
                    ? AddedEntry
                    : null);

        public Task<bool> HasReversalAsync(
            Guid financialBookId,
            Guid originalJournalEntryId,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
        {
            AddedEntry = entry;
            return Task.CompletedTask;
        }

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId,
            Guid accountId,
            byte[]? expectedRowVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
