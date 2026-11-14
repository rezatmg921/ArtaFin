using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.ListAccounts;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.Accounts;

public sealed class ListAccountsQueryHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerRequestsPage_ReturnsFilteredPage()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var expected = new AccountPage(
            [CreateAccount(book.Id)],
            TotalCount: 7);
        var repository = new FakeAccountReadRepository(expected);
        var handler = CreateHandler(ownerId, book, repository);

        var result = await handler.Handle(
            new ListAccountsQuery(
                book.Id, AccountStatus.Active, " cash ", Page: 2, PageSize: 5),
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(book.Id, repository.FinancialBookId);
        Assert.Equal(AccountStatus.Active, repository.Status);
        Assert.Equal("cash", repository.SearchTerm);
        Assert.Equal(2, repository.Page);
        Assert.Equal(5, repository.PageSize);
    }

    [Theory]
    [InlineData(FinancialBookRole.Auditor)]
    [InlineData(FinancialBookRole.Viewer)]
    public async Task Handle_WhenActiveReadOnlyMemberRequestsPage_AllowsRead(
        FinancialBookRole role)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, memberId, role, Now.AddMinutes(-1));
        var repository = new FakeAccountReadRepository(new AccountPage([], 0));
        var handler = CreateHandler(memberId, book, repository, member);

        var result = await handler.Handle(
            new ListAccountsQuery(book.Id, null, null, 1, 50),
            CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotBookMember_RejectsBeforeQueryingAccounts()
    {
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeAccountReadRepository(new AccountPage([], 0));
        var handler = CreateHandler(attackerId, book, repository);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(
                new ListAccountsQuery(book.Id, null, null, 1, 50),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_WhenPagingArgumentsAreInvalid_RejectsRequest(
        int page,
        int pageSize)
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeAccountReadRepository(new AccountPage([], 0));
        var handler = CreateHandler(ownerId, book, repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.Handle(
                new ListAccountsQuery(book.Id, null, null, page, pageSize),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "List accounts test", "USD", Now);

    private static Account CreateAccount(Guid bookId) =>
        Account.Create(Guid.NewGuid(), bookId, "1000", "Cash", AccountType.Asset, Now);

    private static ListAccountsQueryHandler CreateHandler(
        Guid userId,
        FinancialBook book,
        FakeAccountReadRepository accounts,
        FinancialBookMember? member = null) =>
        new(
            new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(book, member),
            accounts,
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
                member is not null &&
                member.FinancialBookId == financialBookId &&
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

    private sealed class FakeAccountReadRepository(AccountPage page)
        : IAccountReadRepository
    {
        public int Calls { get; private set; }
        public Guid FinancialBookId { get; private set; }
        public AccountStatus? Status { get; private set; }
        public string? SearchTerm { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }

        public Task<AccountPage> GetPageAsync(
            Guid financialBookId,
            AccountStatus? status,
            string? searchTerm,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            FinancialBookId = financialBookId;
            Status = status;
            SearchTerm = searchTerm;
            Page = pageNumber;
            PageSize = pageSize;
            return Task.FromResult(page);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
