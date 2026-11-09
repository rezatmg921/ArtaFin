using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Application.Features.FinancialBooks.ListMembers;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.FinancialBooks;

public sealed class ListFinancialBookMembersTests
{
    [Fact]
    public async Task HandleAsync_WhenCallerIsOwner_ReturnsActiveMembers()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(
            Guid.NewGuid(), ownerId, "Shared book", "USD", DateTimeOffset.UtcNow);
        var expected = new FinancialBookMemberSummary(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "member@artafin.test",
            FinancialBookRole.Auditor,
            DateTimeOffset.UtcNow);
        var repository = new FakeFinancialBookRepository(book, [expected]);
        var handler = new ListFinancialBookMembersQueryHandler(
            new FakeCurrentUser(ownerId), repository);

        var result = await handler.HandleAsync(
            new ListFinancialBookMembersQuery(book.Id),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(expected, result[0]);
        Assert.Equal(1, repository.ListCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotOwner_RejectsWithoutListingMembers()
    {
        var ownerId = Guid.NewGuid();
        var book = FinancialBook.Create(
            Guid.NewGuid(), ownerId, "Shared book", "USD", DateTimeOffset.UtcNow);
        var repository = new FakeFinancialBookRepository(book, []);
        var handler = new ListFinancialBookMembersQueryHandler(
            new FakeCurrentUser(Guid.NewGuid()), repository);

        await Assert.ThrowsAsync<FinancialBookManagementDeniedException>(() =>
            handler.HandleAsync(
                new ListFinancialBookMembersQuery(book.Id),
                CancellationToken.None));

        Assert.Equal(0, repository.ListCalls);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook book,
        IReadOnlyList<FinancialBookMemberSummary> members)
        : IFinancialBookRepository
    {
        public int ListCalls { get; private set; }

        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(
                book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId, CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult(members);
        }

        public Task AddMemberAsync(
            FinancialBookMember member, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
