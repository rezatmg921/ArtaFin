using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.GetAccountLedger;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Accounts;

public sealed class GetAccountLedgerQueryHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerRequestsLedger_ReturnsAccountAndCumulativeBalances()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var repository = new FakeLedgerReadRepository(new AccountLedgerPage(
            [
                new AccountLedgerLine(
                    Guid.NewGuid(), Now.AddDays(-1), "Cash received", null, 1,
                    PostingSide.Debit, 100m, "USD", 150m),
                new AccountLedgerLine(
                    Guid.NewGuid(), Now, "Cash paid", null, 2,
                    PostingSide.Credit, 20m, "USD", 130m)
            ],
            TotalCount: 2,
            OpeningBalance: 50m));
        var handler = CreateHandler(ownerId, book, account, repository);

        var result = await handler.Handle(
            new GetAccountLedgerQuery(book.Id, account.Id, null, null, 1, 10),
            CancellationToken.None);

        Assert.Equal(account.Id, result.AccountId);
        Assert.Equal("1000", result.AccountCode);
        Assert.Equal("Cash", result.AccountName);
        Assert.Equal(AccountNormalBalance.Debit, result.NormalBalance);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(50m, result.OpeningBalance);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(150m, result.Items[0].BalanceAfter);
        Assert.Equal(130m, result.Items[1].BalanceAfter);
        Assert.Equal(book.Id, repository.FinancialBookId);
        Assert.Equal(account.Id, repository.AccountId);
        Assert.Equal(AccountNormalBalance.Debit, repository.NormalBalance);
    }

    [Theory]
    [InlineData(FinancialBookRole.Auditor)]
    [InlineData(FinancialBookRole.Viewer)]
    public async Task Handle_WhenActiveReadOnlyMemberRequestsLedger_AllowsRead(
        FinancialBookRole role)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, memberId, role, Now.AddMinutes(-1));
        var repository = new FakeLedgerReadRepository(new AccountLedgerPage([], 0, 0m));

        var handler = CreateHandler(memberId, book, account, repository, member);

        var result = await handler.Handle(
            new GetAccountLedgerQuery(book.Id, account.Id, null, null, 1, 50),
            CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task Handle_WhenCallerHasNoBookAccess_RejectsBeforeLoadingAccountOrLedger()
    {
        var ownerId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var repository = new FakeLedgerReadRepository(new AccountLedgerPage([], 0, 0m));
        var accounts = new FakeAccountRepository(account);
        var handler = CreateHandler(callerId, book, account, repository, accountRepository: accounts);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(
                new GetAccountLedgerQuery(book.Id, account.Id, null, null, 1, 50),
                CancellationToken.None));

        Assert.Equal(0, accounts.Calls);
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Handle_WhenAccountDoesNotBelongToBook_ThrowsNotFound()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeLedgerReadRepository(new AccountLedgerPage([], 0, 0m));
        var handler = CreateHandler(ownerId, book, null, repository);

        await Assert.ThrowsAsync<AccountLedgerNotFoundException>(() =>
            handler.Handle(
                new GetAccountLedgerQuery(book.Id, Guid.NewGuid(), null, null, 1, 50),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_WhenPagingIsInvalid_RejectsBeforeRepositoryCall(int page, int pageSize)
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var account = CreateCashAccount(book.Id);
        var repository = new FakeLedgerReadRepository(new AccountLedgerPage([], 0, 0m));
        var handler = CreateHandler(ownerId, book, account, repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.Handle(
                new GetAccountLedgerQuery(book.Id, account.Id, null, null, page, pageSize),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "Account ledger test", "USD", Now);

    private static Account CreateCashAccount(Guid bookId) =>
        Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, Now);

    private static GetAccountLedgerQueryHandler CreateHandler(
        Guid userId,
        FinancialBook book,
        Account? account,
        FakeLedgerReadRepository ledgerRepository,
        FinancialBookMember? member = null,
        FakeAccountRepository? accountRepository = null) =>
        new(
            new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(book, member),
            accountRepository ?? new FakeAccountRepository(account),
            ledgerRepository,
            new FixedTimeProvider(Now));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook book,
        FinancialBookMember? member) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member is not null && member.FinancialBookId == financialBookId && member.UserId == userId
                    ? member
                    : null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(
            Guid financialBookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>([]);

        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeAccountRepository(Account? account) : IAccountRepository
    {
        public int Calls { get; private set; }

        public Task<Account?> GetByIdAsync(
            Guid financialBookId, Guid accountId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Account?>(
                account is not null && account.FinancialBookId == financialBookId && account.Id == accountId
                    ? account
                    : null);
        }

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid financialBookId, Guid accountId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> ExistsByCodeAsync(
            Guid financialBookId, string normalizedCode, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task AddAsync(Account account, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeLedgerReadRepository(AccountLedgerPage page)
        : IAccountLedgerReadRepository
    {
        public int Calls { get; private set; }
        public Guid FinancialBookId { get; private set; }
        public Guid AccountId { get; private set; }
        public AccountNormalBalance NormalBalance { get; private set; }

        public Task<AccountLedgerPage> GetPageAsync(
            Guid financialBookId,
            Guid accountId,
            AccountNormalBalance normalBalance,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            FinancialBookId = financialBookId;
            AccountId = accountId;
            NormalBalance = normalBalance;
            return Task.FromResult(page);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
