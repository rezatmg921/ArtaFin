using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Application.Features.FinancialBooks.ManageMember;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests.Features.FinancialBooks;

public sealed class ManageFinancialBookMemberTests
{
    [Fact]
    public async Task ChangeRole_WhenOwnerChangesActiveMember_UpdatesRoleAndSaves()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = CreateMember(book.Id);
        var repository = new FakeFinancialBookRepository(book, member);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangeFinancialBookMemberRoleCommandHandler(
            new FakeCurrentUser(ownerId), repository, unitOfWork);

        await handler.HandleAsync(
            new ChangeFinancialBookMemberRoleCommand(
                book.Id, member.Id, FinancialBookRole.Auditor),
            CancellationToken.None);

        Assert.Equal(FinancialBookRole.Auditor, member.Role);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ChangeRole_WhenCallerIsNotOwner_RejectsWithoutSaving()
    {
        var book = CreateBook(Guid.NewGuid());
        var member = CreateMember(book.Id);
        var repository = new FakeFinancialBookRepository(book, member);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangeFinancialBookMemberRoleCommandHandler(
            new FakeCurrentUser(Guid.NewGuid()), repository, unitOfWork);

        await Assert.ThrowsAsync<FinancialBookManagementDeniedException>(() =>
            handler.HandleAsync(
                new ChangeFinancialBookMemberRoleCommand(
                    book.Id, member.Id, FinancialBookRole.Viewer),
                CancellationToken.None));

        Assert.Equal(FinancialBookRole.Accountant, member.Role);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task ChangeRole_WhenMembershipDoesNotExist_RejectsWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var repository = new FakeFinancialBookRepository(book, null);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangeFinancialBookMemberRoleCommandHandler(
            new FakeCurrentUser(ownerId), repository, unitOfWork);

        await Assert.ThrowsAsync<FinancialBookMemberNotFoundException>(() =>
            handler.HandleAsync(
                new ChangeFinancialBookMemberRoleCommand(
                    book.Id, Guid.NewGuid(), FinancialBookRole.Viewer),
                CancellationToken.None));

        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Revoke_WhenOwnerRevokesActiveMember_SetsTimestampAndSaves()
    {
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-11-14T12:00:00Z");
        var book = CreateBook(ownerId);
        var member = CreateMember(book.Id);
        var repository = new FakeFinancialBookRepository(book, member);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RevokeFinancialBookMemberCommandHandler(
            new FakeCurrentUser(ownerId), repository, unitOfWork, new FixedTimeProvider(now));

        await handler.HandleAsync(
            new RevokeFinancialBookMemberCommand(book.Id, member.Id),
            CancellationToken.None);

        Assert.Equal(now, member.RevokedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Revoke_WhenMembershipIsAlreadyRevoked_RejectsWithoutSaving()
    {
        var ownerId = Guid.NewGuid();
        var book = CreateBook(ownerId);
        var member = CreateMember(book.Id);
        member.Revoke(member.GrantedAt.AddMinutes(1));
        var repository = new FakeFinancialBookRepository(book, member);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new RevokeFinancialBookMemberCommandHandler(
            new FakeCurrentUser(ownerId), repository, unitOfWork, TimeProvider.System);

        await Assert.ThrowsAsync<FinancialBookMemberNotActiveException>(() =>
            handler.HandleAsync(
                new RevokeFinancialBookMemberCommand(book.Id, member.Id),
                CancellationToken.None));

        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(
            Guid.NewGuid(), ownerId, "Shared book", "USD", DateTimeOffset.UtcNow);

    private static FinancialBookMember CreateMember(Guid bookId) =>
        FinancialBookMember.Grant(
            Guid.NewGuid(),
            bookId,
            Guid.NewGuid(),
            FinancialBookRole.Accountant,
            DateTimeOffset.UtcNow);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(
        FinancialBook? book,
        FinancialBookMember? member) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult(book?.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<FinancialBookMember?> GetMemberByIdAsync(
            Guid financialBookId, Guid membershipId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(
                member is not null &&
                member.FinancialBookId == financialBookId &&
                member.Id == membershipId
                    ? member
                    : null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(
            FinancialBookMember newMember, CancellationToken cancellationToken) =>
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
