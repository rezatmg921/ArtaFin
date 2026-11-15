using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.ListJournalEntries;

/// <summary>
/// Lists posted journal entries only for a financial book the caller owns
/// or actively belongs to. All read-only member roles are allowed.
/// </summary>
public sealed class ListJournalEntriesQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IJournalEntryReadRepository journalEntryReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ListJournalEntriesQuery, JournalEntryPage>
{
    public async Task<JournalEntryPage> Handle(
        ListJournalEntriesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be greater than zero.");
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query), "Page size must be between 1 and 100.");
        if (query.Page > int.MaxValue / query.PageSize)
            throw new ArgumentOutOfRangeException(nameof(query), "Requested page is too large.");
        if (query.SearchTerm is not null && query.SearchTerm.Trim().Length > 200)
            throw new ArgumentException("Search term cannot exceed 200 characters.", nameof(query));
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new ArgumentException("From date cannot be later than To date.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId,
            cancellationToken);

        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var membership = await financialBookRepository.GetMemberAsync(
                book.Id,
                currentUser.UserId,
                cancellationToken);

            if (membership is null || !membership.IsActiveAt(timeProvider.GetUtcNow()))
                throw new FinancialBookAccessDeniedException();
        }

        return await journalEntryReadRepository.GetPageAsync(
            book.Id,
            query.From,
            query.To,
            query.SearchTerm?.Trim(),
            query.IsReversal,
            query.Page,
            query.PageSize,
            cancellationToken);
    }
}
