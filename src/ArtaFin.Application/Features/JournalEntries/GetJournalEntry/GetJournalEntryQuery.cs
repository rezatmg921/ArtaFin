using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.GetJournalEntry;

/// <summary>Requests one posted journal entry within its financial-book boundary.</summary>
public sealed record GetJournalEntryQuery(
    Guid FinancialBookId,
    Guid JournalEntryId) : IRequest<JournalEntry?>;
