using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.UpdateAccount;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Accounts;

public sealed class UpdateAccountCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerSuppliesMatchingVersion_RenamesAndReturnsNewVersion()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var originalVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var newVersion = new byte[] { 2, 3, 4, 5, 6, 7, 8, 9 };
        var accounts = new FakeAccountRepository(
            new AccountReadSnapshot(account, originalVersion),
            new AccountReadSnapshot(account, newVersion));
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        var result = await handler.Handle(
            new UpdateAccountCommand(
                book.Id, account.Id, "  Operating Cash  ",
                [originalVersion], MatchAny: false),
            CancellationToken.None);

        Assert.Equal("Operating Cash", account.Name);
        Assert.NotNull(result);
        Assert.Equal(newVersion, result.RowVersion);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal(1, accounts.GetReadSnapshotCalls);
    }

    [Fact]
    public async Task Handle_WhenStrongVersionDoesNotMatch_ThrowsPreconditionFailed()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var accounts = new FakeAccountRepository(
            new AccountReadSnapshot(account, [1, 2, 3, 4, 5, 6, 7, 8]),
            null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        await Assert.ThrowsAsync<AccountPreconditionFailedException>(() =>
            handler.Handle(
                new UpdateAccountCommand(
                    book.Id, account.Id, "Updated",
                    [[8, 7, 6, 5, 4, 3, 2, 1]], MatchAny: false),
                CancellationToken.None));

        Assert.Equal("Cash", account.Name);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Equal(0, accounts.GetReadSnapshotCalls);
    }

    [Fact]
    public async Task Handle_WhenIfMatchWildcardIsUsed_AllowsUpdateAndStillCommits()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var accounts = new FakeAccountRepository(
            new AccountReadSnapshot(account, [1, 2, 3, 4, 5, 6, 7, 8]),
            new AccountReadSnapshot(account, [2, 3, 4, 5, 6, 7, 8, 9]));
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        var result = await handler.Handle(
            new UpdateAccountCommand(
                book.Id, account.Id, "Updated", null, MatchAny: true),
            CancellationToken.None);

        Assert.Equal("Updated", account.Name);
        Assert.NotNull(result);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Theory]
    [InlineData(FinancialBookRole.Auditor)]
    [InlineData(FinancialBookRole.Viewer)]
    public async Task Handle_WhenMemberHasReadOnlyRole_RejectsUpdate(
        FinancialBookRole role)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateAccount(book.Id);
        var membership = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, memberId, role, Now.AddMinutes(-1));
        var accounts = new FakeAccountRepository(
            new AccountReadSnapshot(account, [1, 2, 3, 4, 5, 6, 7, 8]),
            null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(memberId, book, accounts, unitOfWork, membership);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(
                new UpdateAccountCommand(
                    book.Id, account.Id, "Updated", null, MatchAny: true),
                CancellationToken.None));

        Assert.Equal("Cash", account.Name);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Equal(0, accounts.GetForUpdateCalls);
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotExist_ReturnsNullWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var accounts = new FakeAccountRepository(null, null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, accounts, unitOfWork);

        var result = await handler.Handle(
            new UpdateAccountCommand(
                book.Id, Guid.NewGuid(), "Updated", null, MatchAny: true),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Test book", "USD", Now);

    private static Account CreateAccount(Guid bookId) =>
        Account.Create(
            Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, Now);

    private static UpdateAccountCommandHandler CreateHandler(
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
        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(
                book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member?.FinancialBookId == financialBookId &&
                member.UserId == userId ? member : null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(
            FinancialBookMember member,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(
        AccountReadSnapshot? updateSnapshot,
        AccountReadSnapshot? refreshedSnapshot) : IAccountRepository
    {
        public int GetForUpdateCalls { get; private set; }
        public int GetReadSnapshotCalls { get; private set; }

        public Task<Account?> GetByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken)
        {
            GetReadSnapshotCalls++;
            return Task.FromResult(refreshedSnapshot);
        }

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId,
            Guid accountId,
            byte[]? expectedRowVersion,
            CancellationToken cancellationToken)
        {
            GetForUpdateCalls++;
            return Task.FromResult(updateSnapshot);
        }

        public Task<bool> ExistsByCodeAsync(
            Guid financialBookId,
            string normalizedCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

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
