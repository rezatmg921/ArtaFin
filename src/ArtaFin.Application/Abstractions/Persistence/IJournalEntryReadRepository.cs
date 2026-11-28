namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only persistence port for paged journal entry queries.
/// </summary>
public interface IJournalEntryReadRepository
{
    Task<JournalEntryPage> GetPageAsync(
        Guid financialBookId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? searchTerm,
        bool? isReversal,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record JournalEntryPage(
    IReadOnlyList<JournalEntryListItem> Items,
    int TotalCount);

public sealed record JournalEntryListItem(
    Guid Id,
    DateTimeOffset PostedAt,
    string Description,
    Guid? ReversesJournalEntryId,
    int LineCount);
