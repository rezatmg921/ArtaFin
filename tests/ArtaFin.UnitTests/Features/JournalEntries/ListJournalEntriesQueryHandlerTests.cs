using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.JournalEntries.ListJournalEntries;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.JournalEntries;

public sealed class ListJournalEntriesQueryHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerRequestsPage_ReturnsFilteredPage()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var expected = new JournalEntryPage([], 3);
        var repository = new FakeJournalEntryReadRepository(expected);
        var handler = CreateHandler(ownerId, book, repository);

        var result = await handler.Handle(
            new ListJournalEntriesQuery(
                book.Id,
                Now.AddDays(-7),
                Now,
                "  office  ",
                false,
                2,
                10),
            CancellationToken.None);

        Assert.Same(expected, result);
        Assert.Equal(book.Id, repository.FinancialBookId);
        Assert.Equal(Now.AddDays(-7), repository.From);
        Assert.Equal(Now, repository.To);
        Assert.Equal("office", repository.SearchTerm);
        Assert.False(repository.IsReversal);
        Assert.Equal(2, repository.Page);
        Assert.Equal(10, repository.PageSize);
        Assert.Equal(1, repository.Calls);
    }

    [Theory]
    [InlineData(FinancialBookRole.Accountant)]
    [InlineData(FinancialBookRole.Auditor)]
    [InlineData(FinancialBookRole.Viewer)]
    public async Task Handle_WhenActiveMemberRequestsPage_AllowsRead(FinancialBookRole role)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = FinancialBookMember.Grant(
            Guid.NewGuid(), book.Id, memberId, role, Now.AddMinutes(-1));
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        var handler = CreateHandler(memberId, book, repository, member);

        var result = await handler.Handle(
            new ListJournalEntriesQuery(book.Id, null, null, null, null, 1, 50),
            CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotBookMember_RejectsBeforeQueryingEntries()
    {
        var book = CreateBook(Guid.NewGuid());
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        var handler = CreateHandler(Guid.NewGuid(), book, repository);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(
                new ListJournalEntriesQuery(book.Id, null, null, null, null, 1, 50),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Handle_WhenBookDoesNotExist_RejectsBeforeQueryingEntries()
    {
        var book = CreateBook(Guid.NewGuid());
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        var handler = CreateHandler(book.OwnerId, null, repository);

        await Assert.ThrowsAsync<FinancialBookAccessDeniedException>(() =>
            handler.Handle(
                new ListJournalEntriesQuery(book.Id, null, null, null, null, 1, 50),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task Handle_WhenFromIsLaterThanTo_RejectsBeforeAccessingBook()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        var handler = CreateHandler(ownerId, book, repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(
                new ListJournalEntriesQuery(
                    book.Id, Now, Now.AddDays(-1), null, null, 1, 50),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_WhenPagingArgumentsAreInvalid_RejectsBeforeQuerying(
        int page,
        int pageSize)
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeJournalEntryReadRepository(new JournalEntryPage([], 0));
        var handler = CreateHandler(ownerId, book, repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.Handle(
                new ListJournalEntriesQuery(book.Id, null, null, null, null, page, pageSize),
                CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(Guid.NewGuid(), ownerId, "List entries test", "USD", Now);

    private static ListJournalEntriesQueryHandler CreateHandler(
        Guid userId,
        FinancialBook? book,
        FakeJournalEntryReadRepository repository,
        FinancialBookMember? member = null) =>
        new(
            new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(book, member),
            repository,
            new FixedTimeProvider(Now));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook? book,
        FinancialBookMember? member) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(
                book is not null && book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId,
            Guid userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member is not null &&
                member.FinancialBookId == financialBookId &&
                member.UserId == userId ? member : null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(
            Guid financialBookId,
            Guid membershipId,
            CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

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

    private sealed class FakeJournalEntryReadRepository(JournalEntryPage resultPage)
        : IJournalEntryReadRepository
    {
        public int Calls { get; private set; }
        public Guid FinancialBookId { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }
        public string? SearchTerm { get; private set; }
        public bool? IsReversal { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }

        public Task<JournalEntryPage> GetPageAsync(
            Guid financialBookId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? searchTerm,
            bool? isReversal,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Calls++;
            FinancialBookId = financialBookId;
            From = from;
            To = to;
            SearchTerm = searchTerm;
            IsReversal = isReversal;
            Page = page;
            PageSize = pageSize;
            return Task.FromResult(resultPage);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
