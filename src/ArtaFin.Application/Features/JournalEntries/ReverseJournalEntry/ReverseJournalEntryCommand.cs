using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;

/// <summary>
/// Requests creation of a new journal entry that reverses an existing entry.
/// </summary>
public sealed record ReverseJournalEntryCommand(
    Guid FinancialBookId,
    Guid JournalEntryId,
    string Description) : IRequest<Guid>;
