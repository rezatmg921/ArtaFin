using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class CreateAccountCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly DateTimeOffset Now =
        new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_WhenUserOwnsBook_CreatesAccountAndCommits()
    {
        var accountRepository = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeFinancialBookRepository(CreateBook(OwnerId)),
            accountRepository,
            unitOfWork,
            OwnerId);
        var command = new CreateAccountCommand(
            BookId, "  bank-01 ", " Main Bank ", AccountType.Asset);

        var accountId = await handler.HandleAsync(command, CancellationToken.None);

        var account = Assert.IsType<Account>(accountRepository.AddedAccount);
        Assert.NotEqual(Guid.Empty, accountId);
        Assert.Equal(accountId, account.Id);
        Assert.Equal(BookId, account.FinancialBookId);
        Assert.Equal("BANK-01", account.Code);
        Assert.Equal("Main Bank", account.Name);
        Assert.Equal(AccountNormalBalance.Debit, account.NormalBalance);
        Assert.Equal(Now, account.CreatedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenBookIsNotOwned_RejectsWithoutWriting()
    {
        var accountRepository = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeFinancialBookRepository(CreateBook(Guid.NewGuid())),
            accountRepository,
            unitOfWork,
            OwnerId);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.HandleAsync(
                new CreateAccountCommand(BookId, "1000", "Bank", AccountType.Asset),
                CancellationToken.None));

        Assert.Null(accountRepository.AddedAccount);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenCodeAlreadyExists_RejectsWithoutWriting()
    {
        var accountRepository = new FakeAccountRepository { CodeExists = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeFinancialBookRepository(CreateBook(OwnerId)),
            accountRepository,
            unitOfWork,
            OwnerId);

        var exception = await Assert.ThrowsAsync<AccountCodeAlreadyExistsException>(() =>
            handler.HandleAsync(
                new CreateAccountCommand(BookId, " bank-01 ", "Bank", AccountType.Asset),
                CancellationToken.None));

        Assert.Contains("BANK-01", exception.Message);
        Assert.Null(accountRepository.AddedAccount);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static CreateAccountCommandHandler CreateHandler(
        IFinancialBookRepository financialBookRepository,
        FakeAccountRepository accountRepository,
        FakeUnitOfWork unitOfWork,
        Guid userId) =>
        new(
            new FakeCurrentUser(userId),
            financialBookRepository,
            accountRepository,
            unitOfWork,
            new FixedTimeProvider(Now));

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(BookId, ownerId, "Personal", "USD", Now);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook? book)
        : IFinancialBookRepository
    {
        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
    Guid financialBookId,
    CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());
        }
        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult(book?.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public bool CodeExists { get; init; }
        public Account? AddedAccount { get; private set; }

        public Task<Account?> GetByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccountReadSnapshot?>(null);

        public Task<bool> ExistsByCodeAsync(
            Guid financialBookId,
            string normalizedCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(CodeExists);

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            AddedAccount = account;
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
