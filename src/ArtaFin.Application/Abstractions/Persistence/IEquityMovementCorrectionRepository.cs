using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IEquityMovementCorrectionRepository
{
    Task<IReadOnlyList<EquityMovementCorrection>> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken);

    Task<JournalEntry?> GetReversalAsync(
        Guid financialBookId,
        Guid originalJournalEntryId,
        CancellationToken cancellationToken);

    Task<bool> IsEquityAccountAsync(
        Guid financialBookId,
        Guid accountId,
        CancellationToken cancellationToken);

    Task AddRangeAsync(
        IReadOnlyCollection<EquityMovementCorrection> corrections,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
