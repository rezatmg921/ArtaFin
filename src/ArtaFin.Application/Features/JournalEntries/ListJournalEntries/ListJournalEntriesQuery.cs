using ArtaFin.Application.Abstractions.Persistence;
using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.ListJournalEntries;

public sealed record ListJournalEntriesQuery(
    Guid FinancialBookId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? SearchTerm,
    bool? IsReversal,
    int Page,
    int PageSize) : IRequest<JournalEntryPage>;
