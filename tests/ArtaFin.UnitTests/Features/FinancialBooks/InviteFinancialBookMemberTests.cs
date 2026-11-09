using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.FinancialBooks.InviteMember;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Security;
using Xunit;

namespace ArtaFin.UnitTests.Features.FinancialBooks;

public sealed class InviteFinancialBookMemberTests
{
    [Fact]
    public async Task HandleAsync_WhenOwnerInvitesActiveUser_CreatesMembership()
    {
        var ownerId = Guid.NewGuid();
        var target = AppUser.Register(Guid.NewGuid(), "accountant@artafin.test", DateTimeOffset.UtcNow);
        var book = CreateBook(ownerId);
        var books = new FakeFinancialBookRepository(book);
        var users = new FakeUserRepository(target);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, books, users, unitOfWork);

        var memberId = await handler.HandleAsync(
            new InviteFinancialBookMemberCommand(
                book.Id, target.Email, FinancialBookRole.Accountant),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, memberId);
        Assert.NotNull(books.AddedMember);
        Assert.Equal(book.Id, books.AddedMember!.FinancialBookId);
        Assert.Equal(target.Id, books.AddedMember.UserId);
        Assert.Equal(FinancialBookRole.Accountant, books.AddedMember.Role);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenCallerIsNotOwner_RejectsWithoutWriting()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var target = AppUser.Register(Guid.NewGuid(), "viewer@artafin.test", DateTimeOffset.UtcNow);
        var book = CreateBook(ownerId);
        var books = new FakeFinancialBookRepository(book);
        var users = new FakeUserRepository(target);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(otherUserId, books, users, unitOfWork);

        await Assert.ThrowsAsync<FinancialBookManagementDeniedException>(() =>
            handler.HandleAsync(
                new InviteFinancialBookMemberCommand(
                    book.Id, target.Email, FinancialBookRole.Viewer),
                CancellationToken.None));

        Assert.Null(books.AddedMember);
        Assert.Equal(0, users.LookupCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenTargetUserIsInactive_RejectsWithoutWriting()
    {
        var ownerId = Guid.NewGuid();
        var target = AppUser.Register(Guid.NewGuid(), "disabled@artafin.test", DateTimeOffset.UtcNow);
        target.Disable(DateTimeOffset.UtcNow);
        var book = CreateBook(ownerId);
        var books = new FakeFinancialBookRepository(book);
        var users = new FakeUserRepository(target);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(ownerId, books, users, unitOfWork);

        await Assert.ThrowsAsync<InviteTargetUserNotFoundException>(() =>
            handler.HandleAsync(
                new InviteFinancialBookMemberCommand(
                    book.Id, target.Email, FinancialBookRole.Auditor),
                CancellationToken.None));

        Assert.Null(books.AddedMember);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    private static FinancialBook CreateBook(Guid ownerId) =>
        FinancialBook.Create(
            Guid.NewGuid(), ownerId, "Shared book", "USD", DateTimeOffset.UtcNow);

    private static InviteFinancialBookMemberCommandHandler CreateHandler(
        Guid callerId,
        FakeFinancialBookRepository books,
        FakeUserRepository users,
        FakeUnitOfWork unitOfWork) =>
        new(new FakeCurrentUser(callerId), books, users, unitOfWork, TimeProvider.System);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook? book)
        : IFinancialBookRepository
    {
        public FinancialBookMember? AddedMember { get; private set; }

        public Task<FinancialBook?> GetByIdAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult(book?.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(
            Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
            Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(
                Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(
            FinancialBookMember member, CancellationToken cancellationToken)
        {
            AddedMember = member;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository(AppUser? user) : IUserRepository
    {
        public int LookupCalls { get; private set; }

        public Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<AppUser?>(user?.Id == userId ? user : null);

        public Task<AppUser?> GetByNormalizedEmailAsync(
            string normalizedEmail, CancellationToken cancellationToken)
        {
            LookupCalls++;
            return Task.FromResult<AppUser?>(
                user?.NormalizedEmail == normalizedEmail ? user : null);
        }

        public Task<UserCredential?> GetCredentialByUserIdAsync(
            Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<UserCredential?>(null);

        public Task AddUserAsync(AppUser appUser, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddCredentialAsync(UserCredential credential, CancellationToken cancellationToken) =>
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
}
