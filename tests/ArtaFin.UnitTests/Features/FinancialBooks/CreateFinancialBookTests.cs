using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.FinancialBooks.CreateFinancialBook;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.FinancialBooks;

public sealed class CreateFinancialBookTests
{
    [Fact]
    public async Task HandleAsync_CreatesBookForAuthenticatedUser()
    {
        var ownerId = Guid.NewGuid();
        var writer = new FakeFinancialBookWriter();
        var accountRepository = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var now = DateTimeOffset.Parse("2026-11-09T10:00:00+00:00");
        var handler = new CreateFinancialBookCommandHandler(
            new FakeCurrentUser(ownerId), writer, accountRepository, unitOfWork,
            new FixedTimeProvider(now));

        var id = await handler.HandleAsync(
            new CreateFinancialBookCommand("Operating ledger", "eur"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(writer.AddedBook);
        Assert.Equal(id, writer.AddedBook!.Id);
        Assert.Equal(ownerId, writer.AddedBook.OwnerId);
        Assert.Equal("Operating ledger", writer.AddedBook.Name);
        Assert.Equal("EUR", writer.AddedBook.BaseCurrency.Code);
        Assert.Equal(now, writer.AddedBook.CreatedAt);
        Assert.NotEmpty(accountRepository.AddedAccounts);
        Assert.All(accountRepository.AddedAccounts, account =>
            Assert.Equal(id, account.FinancialBookId));
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenNameIsInvalid_DoesNotPersist()
    {
        var writer = new FakeFinancialBookWriter();
        var accountRepository = new FakeAccountRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateFinancialBookCommandHandler(
            new FakeCurrentUser(Guid.NewGuid()), writer, accountRepository,
            unitOfWork, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new CreateFinancialBookCommand(" ", "USD"),
                CancellationToken.None));

        Assert.Null(writer.AddedBook);
        Assert.Empty(accountRepository.AddedAccounts);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookWriter : IFinancialBookWriter
    {
        public FinancialBook? AddedBook { get; private set; }

        public Task AddAsync(FinancialBook financialBook, CancellationToken cancellationToken)
        {
            AddedBook = financialBook;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public List<Account> AddedAccounts { get; } = [];

        public Task<Account?> GetByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken)
            => Task.FromResult<Account?>(null);

        public Task<AccountReadSnapshot?> GetReadSnapshotByIdAsync(Guid financialBookId, Guid accountId, CancellationToken cancellationToken)
            => Task.FromResult<AccountReadSnapshot?>(null);

        public Task<AccountReadSnapshot?> GetForUpdateAsync(
            Guid financialBookId, Guid accountId, byte[]? expectedRowVersion, CancellationToken cancellationToken)
            => Task.FromResult<AccountReadSnapshot?>(null);

        public Task<bool> ExistsByCodeAsync(Guid financialBookId, string normalizedCode, CancellationToken cancellationToken)
            => Task.FromResult(AddedAccounts.Any(account =>
                account.FinancialBookId == financialBookId && account.Code == normalizedCode));

        public Task<bool> DeleteIfUnusedAsync(Account account, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            AddedAccounts.Add(account);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
