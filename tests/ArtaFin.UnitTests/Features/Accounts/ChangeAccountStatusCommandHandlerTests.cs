using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.ChangeAccountStatus;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Accounts;

public sealed class ChangeAccountStatusCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, AccountStatus.Inactive)]
    [InlineData(true, AccountStatus.Active)]
    public async Task Handle_WhenOwnerSuppliesCurrentVersion_ChangesStatusAndReturnsNewVersion(bool reactivate, AccountStatus expectedStatus)
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Business", "EUR", Now.AddYears(-1));
        var account = Account.Create(Guid.NewGuid(), book.Id, "1000", "Cash", AccountType.Asset, Now.AddYears(-1));
        if (reactivate) account.Deactivate(Now.AddHours(-1));

        var currentVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var newVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };
        var repository = new FakeAccountRepository(account, currentVersion, newVersion);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, repository, unitOfWork);

        var result = await handler.Handle(new ChangeAccountStatusCommand(book.Id, account.Id, [currentVersion], false, reactivate), CancellationToken.None);

        Assert.Equal(expectedStatus, account.Status);
        Assert.NotNull(result);
        Assert.Equal(newVersion, result.RowVersion);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
        Assert.Equal(1, repository.ReadSnapshotCalls);
    }

    [Fact]
    public async Task Handle_WhenVersionIsStale_RejectsWithoutChangingAccount()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Business", "EUR", Now.AddYears(-1));
        var account = Account.Create(Guid.NewGuid(), book.Id, "1000", "Cash", AccountType.Asset, Now.AddYears(-1));
        var repository = new FakeAccountRepository(account, [1, 2, 3, 4, 5, 6, 7, 8], null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, repository, unitOfWork);

        await Assert.ThrowsAsync<AccountPreconditionFailedException>(() => handler.Handle(
            new ChangeAccountStatusCommand(book.Id, account.Id, [[8, 7, 6, 5, 4, 3, 2, 1]], false, false),
            CancellationToken.None));

        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
        Assert.Equal(0, repository.ReadSnapshotCalls);
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotExist_ReturnsNullWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(Guid.NewGuid(), ownerId, "Business", "EUR", Now.AddYears(-1));
        var repository = new FakeAccountRepository(null, [1, 2, 3, 4, 5, 6, 7, 8], null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, book, repository, unitOfWork);

        var result = await handler.Handle(new ChangeAccountStatusCommand(book.Id, Guid.NewGuid(), [[1, 2, 3, 4, 5, 6, 7, 8]], false, false), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static ChangeAccountStatusCommandHandler CreateHandler(Guid userId, FinancialBook book, FakeAccountRepository repository, FakeUnitOfWork unitOfWork) =>
        new(new FakeCurrentUser(userId), new FakeFinancialBookRepository(book), repository, unitOfWork, new FixedTimeProvider(Now));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<FinancialBook?>(book.Id == id ? book : null);
        public Task<FinancialBookMember?> GetMemberAsync(Guid bookId, Guid userId, CancellationToken ct) => Task.FromResult<FinancialBookMember?>(null);
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(Array.Empty<FinancialBookMemberSummary>());
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(Account? account, byte[] currentVersion, byte[]? newVersion) : IAccountRepository
    {
        public int ReadSnapshotCalls { get; private set; }
        public Task<Account?> GetByIdAsync(Guid bookId, Guid accountId, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid bookId, Guid accountId, CancellationToken ct)
        {
            ReadSnapshotCalls++;
            return Task.FromResult<AccountReadSnapshot?>(account is not null && newVersion is not null ? new AccountReadSnapshot(account, newVersion) : null);
        }
        public Task<AccountReadSnapshot?> GetForUpdateAsync(Guid bookId, Guid accountId, byte[]? expected, CancellationToken ct) =>
            Task.FromResult<AccountReadSnapshot?>(account is not null && account.FinancialBookId == bookId && account.Id == accountId ? new AccountReadSnapshot(account, currentVersion) : null);
        public Task<bool> ExistsByCodeAsync(Guid bookId, string code, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> DeleteIfUnusedAsync(Account value, CancellationToken ct) => throw new NotImplementedException();
        public Task AddAsync(Account value, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { SaveChangesCalls++; return Task.FromResult(1); }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
