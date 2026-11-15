using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Expenses.CreateExpense;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Expenses;

public sealed class CreateExpenseCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid ExpenseAccountId = Guid.NewGuid();
    private static readonly Guid PaymentAccountId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerRecordsExpense_PostsDebitAndCreditAndCommits()
    {
        var expense = CreateAccount(ExpenseAccountId, AccountType.Expense);
        var payment = CreateAccount(PaymentAccountId, AccountType.Asset);
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId, [expense, payment], journalRepository, unitOfWork);

        var id = await handler.Handle(
            CreateCommand(), CancellationToken.None);

        var entry = Assert.IsType<JournalEntry>(journalRepository.AddedEntry);
        Assert.Equal(id, entry.Id);
        Assert.Equal(BookId, entry.FinancialBookId);
        Assert.Equal(Now, entry.PostedAt);
        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(ExpenseAccountId, entry.Lines[0].AccountId);
        Assert.Equal(PostingSide.Debit, entry.Lines[0].Side);
        Assert.Equal(PaymentAccountId, entry.Lines[1].AccountId);
        Assert.Equal(PostingSide.Credit, entry.Lines[1].Side);
        Assert.Equal(125m, entry.Lines[0].Amount.Amount);
        Assert.Equal("USD", entry.Lines[0].Amount.Currency);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenUserCannotPost_RejectsWithoutWriting()
    {
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(Guid.NewGuid(),
            [CreateAccount(ExpenseAccountId, AccountType.Expense),
             CreateAccount(PaymentAccountId, AccountType.Asset)],
            journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryAccessDeniedException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenExpenseAccountHasWrongType_RejectsWithoutWriting()
    {
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [CreateAccount(ExpenseAccountId, AccountType.Asset),
             CreateAccount(PaymentAccountId, AccountType.Asset)],
            journalRepository, unitOfWork);

        await Assert.ThrowsAsync<ExpenseAccountTypeInvalidException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenPaymentAccountHasWrongType_RejectsWithoutWriting()
    {
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [CreateAccount(ExpenseAccountId, AccountType.Expense),
             CreateAccount(PaymentAccountId, AccountType.Liability)],
            journalRepository, unitOfWork);

        await Assert.ThrowsAsync<ExpensePaymentAccountTypeInvalidException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenCurrencyDiffersFromBookBaseCurrency_RejectsWithoutWriting()
    {
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [CreateAccount(ExpenseAccountId, AccountType.Expense),
             CreateAccount(PaymentAccountId, AccountType.Asset)],
            journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryBaseCurrencyMismatchException>(() =>
            handler.Handle(CreateCommand(new Money(125m, "EUR")), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenAnAccountIsInactive_RejectsWithoutWriting()
    {
        var expense = CreateAccount(ExpenseAccountId, AccountType.Expense);
        expense.Deactivate(Now.AddMinutes(-1));
        var journalRepository = new FakeJournalEntryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId,
            [expense, CreateAccount(PaymentAccountId, AccountType.Asset)],
            journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryAccountUnavailableException>(() =>
            handler.Handle(CreateCommand(), CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static CreateExpenseCommand CreateCommand(Money? amount = null) =>
        new(BookId, ExpenseAccountId, PaymentAccountId, "Office supplies",
            amount ?? new Money(125m, "USD"));

    private static Account CreateAccount(Guid id, AccountType type) =>
        Account.Create(id, BookId, id == ExpenseAccountId ? "6000" : "1000",
            "Test account", type, Now.AddDays(-1));

    private static CreateExpenseCommandHandler CreateHandler(
        Guid userId,
        IReadOnlyCollection<Account> accounts,
        FakeJournalEntryRepository journalRepository,
        FakeUnitOfWork unitOfWork) =>
        new(new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(FinancialBook.Create(
                BookId, OwnerId, "Test book", "USD", Now.AddYears(-1))),
            new FakeAccountRepository(accounts),
            journalRepository,
            unitOfWork,
            new FixedTimeProvider(Now));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<FinancialBookMember?> GetMemberByIdAsync(Guid financialBookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(Array.Empty<FinancialBookMemberSummary>());
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(IReadOnlyCollection<Account> accounts) : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(accounts.SingleOrDefault(account =>
                account.FinancialBookId == financialBookId && account.Id == accountId));
        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<AccountReadSnapshot?> GetForUpdateAsync(Guid financialBookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);
        public Task<bool> ExistsByCodeAsync(Guid financialBookId, string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task AddAsync(Account account, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeJournalEntryRepository : IJournalEntryRepository
    {
        public JournalEntry? AddedEntry { get; private set; }
        public Task<JournalEntry?> GetByIdAsync(Guid financialBookId, Guid journalEntryId, CancellationToken cancellationToken) =>
            Task.FromResult<JournalEntry?>(null);
        public Task<bool> HasReversalAsync(Guid financialBookId, Guid originalJournalEntryId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
        {
            AddedEntry = entry;
            return Task.CompletedTask;
        }
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
