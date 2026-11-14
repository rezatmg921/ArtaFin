using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;
using ArtaFin.Domain.Finance;
using Xunit;

namespace ArtaFin.UnitTests;

public sealed class ReverseJournalEntryCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid BookId = Guid.NewGuid();
    private static readonly Guid DebitAccountId = Guid.NewGuid();
    private static readonly Guid CreditAccountId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenOwnerRequestsReversal_CreatesAndCommitsReversal()
    {
        var original = CreateOriginal();
        var journalRepository = new FakeJournalEntryRepository(original);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId, journalRepository, unitOfWork);

        var reversalId = await handler.Handle(
            new ReverseJournalEntryCommand(BookId, original.Id, "Reverse original"),
            CancellationToken.None);

        var reversal = Assert.IsType<JournalEntry>(journalRepository.AddedEntry);
        Assert.Equal(reversalId, reversal.Id);
        Assert.Equal(original.Id, reversal.ReversesJournalEntryId);
        Assert.Equal(PostingSide.Credit, reversal.Lines[0].Side);
        Assert.Equal(PostingSide.Debit, reversal.Lines[1].Side);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenOriginalAlreadyHasReversal_RejectsWithoutWriting()
    {
        var original = CreateOriginal();
        var journalRepository = new FakeJournalEntryRepository(original)
        {
            HasReversal = true
        };
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(OwnerId, journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryAlreadyReversedException>(() =>
            handler.Handle(
                new ReverseJournalEntryCommand(BookId, original.Id, "Second reversal"),
                CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotOwnerOrAuthorizedMember_RejectsWithoutWriting()
    {
        var original = CreateOriginal();
        var journalRepository = new FakeJournalEntryRepository(original);
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(Guid.NewGuid(), journalRepository, unitOfWork);

        await Assert.ThrowsAsync<JournalEntryAccessDeniedException>(() =>
            handler.Handle(
                new ReverseJournalEntryCommand(BookId, original.Id, "Reverse original"),
                CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
        Assert.Equal(0, unitOfWork.SaveChangesCalls);
    }

    [Fact]
    public async Task Handle_WhenRequestedEntryIsAlreadyAReversal_DomainRejectsIt()
    {
        var original = CreateOriginal();
        var alreadyReversal = JournalEntry.Reverse(
            Guid.NewGuid(), original, Now, "First reversal");
        var journalRepository = new FakeJournalEntryRepository(alreadyReversal);
        var handler = CreateHandler(OwnerId, journalRepository, new FakeUnitOfWork());

        await Assert.ThrowsAsync<ArtaFin.Domain.Common.DomainException>(() =>
            handler.Handle(
                new ReverseJournalEntryCommand(BookId, alreadyReversal.Id, "Reverse reversal"),
                CancellationToken.None));

        Assert.Null(journalRepository.AddedEntry);
    }

    private static JournalEntry CreateOriginal() =>
        JournalEntry.Post(
            Guid.NewGuid(),
            BookId,
            Now.AddHours(-1),
            "Original entry",
            [
                new JournalLine(DebitAccountId, PostingSide.Debit, new Money(50m, "EUR")),
                new JournalLine(CreditAccountId, PostingSide.Credit, new Money(50m, "EUR"))
            ]);

    private static ReverseJournalEntryCommandHandler CreateHandler(
        Guid userId,
        FakeJournalEntryRepository journalRepository,
        FakeUnitOfWork unitOfWork) =>
        new(
            new FakeCurrentUser(userId),
            new FakeFinancialBookRepository(FinancialBook.Create(
                BookId, OwnerId, "Business", "EUR", Now.AddYears(-1))),
            journalRepository,
            unitOfWork,
            new FixedTimeProvider(Now));

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FakeFinancialBookRepository(FinancialBook book) : IFinancialBookRepository
    {
        public Task<FinancialBook?> GetByIdAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBook?>(book.Id == financialBookId ? book : null);

        public Task<FinancialBookMember?> GetMemberAsync(Guid financialBookId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<FinancialBookMember?>(null);

        public Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(Guid financialBookId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FinancialBookMemberSummary>>(Array.Empty<FinancialBookMemberSummary>());

        public Task AddMemberAsync(FinancialBookMember member, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class FakeJournalEntryRepository(JournalEntry original) : IJournalEntryRepository
    {
        public bool HasReversal { get; init; }
        public JournalEntry? AddedEntry { get; private set; }

        public Task<JournalEntry?> GetByIdAsync(Guid financialBookId, Guid journalEntryId, CancellationToken cancellationToken) =>
            Task.FromResult<JournalEntry?>(
                original.FinancialBookId == financialBookId && original.Id == journalEntryId
                    ? original
                    : null);

        public Task<bool> HasReversalAsync(Guid financialBookId, Guid originalJournalEntryId, CancellationToken cancellationToken) =>
            Task.FromResult(HasReversal);

        public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken)
        {
            AddedEntry = entry;
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
