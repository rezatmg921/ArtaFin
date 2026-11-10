using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.DeleteAccount;
using ArtaFin.Application.Features.Accounts.UpdateAccount;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Accounts;

public sealed class DeleteAccountCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerSuppliesCurrentVersionAndAccountIsUnused_DeletesAndSaves()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var accounts = new FakeAccountRepository(new AccountReadSnapshot(account, version));
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        var deleted = await handler.Handle(
            new DeleteAccountCommand(book.Id, account.Id, [version], MatchAny: false),
            CancellationToken.None);

        Assert.True(deleted);
        Assert.Same(account, accounts.DeletedAccount);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenVersionIsStale_ThrowsWithoutDeletingOrSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var accounts = new FakeAccountRepository(
            new AccountReadSnapshot(account, [1, 2, 3, 4, 5, 6, 7, 8]));
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        await Assert.ThrowsAsync<AccountPreconditionFailedException>(() =>
            handler.Handle(
                new DeleteAccountCommand(book.Id, account.Id, [[8, 7, 6, 5, 4, 3, 2, 1]], false),
                CancellationToken.None));

        Assert.Null(accounts.DeletedAccount);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenAccountHasJournalHistory_ThrowsConflictWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var version = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var accounts = new FakeAccountRepository(new AccountReadSnapshot(account, version))
        {
            IsUsed = true
        };
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        await Assert.ThrowsAsync<AccountHasJournalEntriesException>(() =>
            handler.Handle(
                new DeleteAccountCommand(book.Id, account.Id, [version], false),
                CancellationToken.None));

        Assert.Null(accounts.DeletedAccount);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotExist_ReturnsFalseWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var accounts = new FakeAccountRepository(null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        var result = await handler.Handle(
            new DeleteAccountCommand(book.Id, Guid.NewGuid(), null, MatchAny: true),
            CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenReadOnlyMemberAttemptsDelete_RejectsAccess()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, memberId, FinancialBookRole.Auditor, Now.AddMinutes(-1));
        var accounts = new FakeAccountRepository(
            new AccountReadSnapshot(account, [1, 2, 3, 4, 5, 6, 7, 8]));
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(memberId, book, accounts, unitOfWork, member);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(
                new DeleteAccountCommand(book.Id, account.Id, null, MatchAny: true),
                CancellationToken.None));

        Assert.Equal(0, accounts.GetForUpdateCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Test book", "USD", Now);

    private static Account CreateAccount(Guid bookId) =>
        Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, Now);

    private static DeleteAccountCommandHandler CreateHandler(
        Guid userId,
        FinancialBook book,
        FakeAccountRepository accounts,
        FakeUnitOfWork unitOfWork,
        FinancialBookMember? member = null) =>
        new(
            new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(book, member),
            accounts,
            unitOfWork,
            new FixedTimeProvider(Now));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook book,
        FinancialBookMember? member) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == id ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member is not null && member.FinancialBookId == financialBookId &&
                member.UserId == userId ? member : null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(AccountReadSnapshot? snapshot) : IAccountRepository
    {
        public bool IsUsed { get; set; }
        public Account? DeletedAccount { get; private set; }
        public int GetForUpdateCalls { get; private set; }

        public Task<Account?> GetByIdAsync(Guid bookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid bookId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(snapshot);

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid bookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken)
        {
            GetForUpdateCalls++;
            return Task.FromResult(snapshot);
        }

        public Task<bool> ExistsByCodeAsync(
            Guid bookId, string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken)
        {
            if (IsUsed)
                return Task.FromResult(false);

            DeletedAccount = account;
            return Task.FromResult(true);
        }

        public Task AddAsync(Account account, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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