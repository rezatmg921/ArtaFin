using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Accounts;

public sealed class CreateAccountOwnershipTests
{
    [Fact]
    public async Task HandleAsync_WhenCurrentUserOwnsBook_CreatesAccount()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var books = new FakeFinancialBookRepository(book);
        var accounts = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, books, accounts, unitOfWork);

        var accountId = await handler.HandleAsync(
            new CreateAccountCommand(book.Id, "  cash-001 ", "Cash", AccountType.Asset),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, accountId);
        Assert.NotNull(accounts.AddedAccount);
        Assert.Equal(book.Id, accounts.AddedAccount!.FinancialBookId);
        Assert.Equal("CASH-001", accounts.AddedAccount.Code);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentUserDoesNotOwnBook_RejectsWithoutWriting()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var books = new FakeFinancialBookRepository(book);
        var accounts = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(otherUserId, books, accounts, unitOfWork);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.HandleAsync(
                new CreateAccountCommand(book.Id, "1000", "Cash", AccountType.Asset),
                CancellationToken.None));

        Assert.Null(accounts.AddedAccount);
        Assert.Equal(0, accounts.ExistsByCodeCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenBookDoesNotExist_RejectsWithoutWriting()
    {
        var userId = Guid.NewGuid();
        var books = new FakeFinancialBookRepository(null);
        var accounts = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(userId, books, accounts, unitOfWork);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.HandleAsync(
                new CreateAccountCommand(Guid.NewGuid(), "1000", "Cash", AccountType.Asset),
                CancellationToken.None));

        Assert.Null(accounts.AddedAccount);
        Assert.Equal(0, accounts.ExistsByCodeCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(
            Guid.NewGuid(),
            ownerId,
            "Test book",
            "USD",
            DateTimeOffset.UtcNow);

    private static CreateAccountCommandHandler CreateHandler(
        Guid currentUserId,
        FakeFinancialBookRepository books,
        FakeAccountRepository accounts,
        FakeUnitOfWork unitOfWork) =>
        new(
            new FakeCurrentUser(currentUserId),
            books,
            accounts,
            unitOfWork,
            TimeProvider.System);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook? book)
        : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult(book?.Id == financialBookId ? book : null);
    }

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public Account? AddedAccount { get; private set; }
        public int ExistsByCodeCalls { get; private set; }

        public Task<Account?> GetByIdAsync(
            Guid financialBookId,
            Guid accountId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Account?>(null);

        public Task<bool> ExistsByCodeAsync(
            Guid financialBookId,
            string normalizedCode,
            CancellationToken cancellationToken)
        {
            ExistsByCodeCalls++;
            return Task.FromResult(false);
        }

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
}
