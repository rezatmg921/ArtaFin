using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;

/// <summary>A single debit or credit line requested by the caller.</summary>
public sealed record JournalEntryLineCommand(
    Guid AccountId,
    PostingSide Side,
    Money Amount,
    EquityMovementType EquityMovementType = EquityMovementType.Unclassified);

/// <summary>
/// Requests posting a balanced journal entry into a financial book.
/// The authenticated user, entry id and posting timestamp are server-controlled.
/// </summary>
public sealed record CreateJournalEntryCommand(
    Guid FinancialBookId,
    string Description,
    IReadOnlyCollection<JournalEntryLineCommand> Lines) : IRequest<Guid>;
