using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.GetJournalEntry;

/// <summary>
/// Loads a journal entry only when the caller owns or is an active member
/// of the requested financial book.
/// </summary>
public sealed class GetJournalEntryQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IJournalEntryRepository journalEntryRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetJournalEntryQuery, JournalEntry?>
{
    public async Task<JournalEntry?> Handle(
        GetJournalEntryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.JournalEntryId == Guid.Empty)
            throw new ArgumentException("Journal entry id cannot be empty.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId,
            cancellationToken);

        if (book is null)
            throw new JournalEntryAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                book.Id,
                currentUser.UserId,
                cancellationToken);

            if (membership is null || !membership.IsActiveAt(timeProvider.GetUtcNow()))
                throw new JournalEntryAccessDeniedException();
        }

        return await journalEntryRepository.GetByIdAsync(
            book.Id,
            query.JournalEntryId,
            cancellationToken);
    }
}
