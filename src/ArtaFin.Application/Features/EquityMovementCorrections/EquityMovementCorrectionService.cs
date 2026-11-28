using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.EquityMovementCorrections;

public sealed class EquityMovementCorrectionService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IJournalEntryRepository journalEntryRepository,
    IEquityMovementCorrectionRepository correctionRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<EquityMovementCorrectionResult>> CreateAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CreateEquityMovementCorrectionsInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (financialBookId == Guid.Empty || journalEntryId == Guid.Empty)
            throw new ArgumentException("Financial book and journal entry identifiers are required.");
        if (input.Items is null || input.Items.Count == 0)
            throw new ArgumentException("At least one correction item is required.");
        if (input.Items.Any(item => item.LineNumber <= 0 ||
                                    !Enum.IsDefined(item.CorrectedMovementType) ||
                                    item.CorrectedMovementType == EquityMovementType.Unclassified ||
                                    string.IsNullOrWhiteSpace(item.Reason) ||
                                    item.Reason.Trim().Length > EquityMovementCorrection.MaxReasonLength))
            throw new ArgumentException("One or more correction items are invalid.");
        if (input.Items.Select(item => item.LineNumber).Distinct().Count() != input.Items.Count)
            throw new ArgumentException("A line can appear only once in a correction request.");

        await EnsureCanManageAsync(financialBookId, cancellationToken);

        var created = new List<EquityMovementCorrection>();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var entry = await journalEntryRepository.GetByIdAsync(
                financialBookId, journalEntryId, token);
            if (entry is null)
                throw new JournalEntryAccessDeniedException();

            var reversalId = entry.ReversesJournalEntryId;
            var hasReversal = reversalId.HasValue ||
                await journalEntryRepository.HasReversalAsync(financialBookId, journalEntryId, token);

            JournalEntry? pairedEntry = null;
            if (hasReversal)
            {
                if (reversalId.HasValue)
                    throw new EquityMovementCorrectionConflictException(
                        "A reversal entry cannot be corrected independently. Correct its original entry instead.");

                pairedEntry = await FindReversalAsync(financialBookId, journalEntryId, token);
                if (pairedEntry is null)
                    throw new EquityMovementCorrectionConflictException(
                        "The reversal entry could not be loaded.");
            }

            var primaryHistory = await correctionRepository.GetByJournalEntryAsync(
                financialBookId, entry.Id, token);
            IReadOnlyList<EquityMovementCorrection> pairedHistory = pairedEntry is null
                ? Array.Empty<EquityMovementCorrection>()
                : await correctionRepository.GetByJournalEntryAsync(
                    financialBookId, pairedEntry.Id, token);

            foreach (var item in input.Items.OrderBy(item => item.LineNumber))
            {
                var line = entry.Lines.SingleOrDefault(line => line.LineNumber == item.LineNumber);
                if (line is null)
                    throw new EquityMovementCorrectionConflictException(
                        $"Journal line {item.LineNumber} does not exist.");

                if (!await correctionRepository.IsEquityAccountAsync(
                        financialBookId, line.AccountId, token))
                    throw new EquityMovementCorrectionConflictException(
                        $"Journal line {item.LineNumber} does not belong to an equity account.");

                var currentType = GetEffectiveType(line.EquityMovementType, primaryHistory, item.LineNumber);
                if (currentType != item.ExpectedOriginalMovementType)
                    throw new EquityMovementCorrectionConflictException(
                        $"Journal line {item.LineNumber} has changed classification. Expected {currentType}.");

                if (currentType == item.CorrectedMovementType)
                    throw new EquityMovementCorrectionConflictException(
                        $"Journal line {item.LineNumber} already has the requested classification.");

                created.Add(EquityMovementCorrection.Create(
                    Guid.NewGuid(), financialBookId, entry.Id, item.LineNumber,
                    currentType, item.CorrectedMovementType, item.Reason,
                    currentUser.UserId, timeProvider.GetUtcNow()));

                if (pairedEntry is not null)
                {
                    var pairedLine = pairedEntry.Lines.SingleOrDefault(
                        candidate => candidate.LineNumber == item.LineNumber);
                    if (pairedLine is null ||
                        pairedLine.AccountId != line.AccountId ||
                        pairedLine.EquityMovementType != line.EquityMovementType ||
                        pairedLine.Side == line.Side ||
                        pairedLine.Amount.Amount != line.Amount.Amount ||
                        !StringComparer.Ordinal.Equals(pairedLine.Amount.Currency, line.Amount.Currency))
                        throw new EquityMovementCorrectionConflictException(
                            $"The reversal line paired with line {item.LineNumber} is inconsistent.");

                    var pairedType = GetEffectiveType(
                        pairedLine.EquityMovementType, pairedHistory, item.LineNumber);
                    if (pairedType != currentType)
                        throw new EquityMovementCorrectionConflictException(
                            $"The reversal line {item.LineNumber} has a different effective classification.");

                    created.Add(EquityMovementCorrection.Create(
                        Guid.NewGuid(), financialBookId, pairedEntry.Id, item.LineNumber,
                        pairedType, item.CorrectedMovementType, item.Reason,
                        currentUser.UserId, timeProvider.GetUtcNow()));
                }
            }

            await correctionRepository.AddRangeAsync(created, token);
            await correctionRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return created.Select(ToResult).ToArray();
    }

    public async Task<IReadOnlyList<EquityMovementCorrectionResult>> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        if (await journalEntryRepository.GetByIdAsync(
                financialBookId, journalEntryId, cancellationToken) is null)
            throw new JournalEntryAccessDeniedException();

        var items = await correctionRepository.GetByJournalEntryAsync(
            financialBookId, journalEntryId, cancellationToken);
        return items.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(ToResult).ToArray();
    }

    private async Task<JournalEntry?> FindReversalAsync(
        Guid financialBookId,
        Guid originalId,
        CancellationToken cancellationToken)
    {
        return await correctionRepository.GetReversalAsync(
            financialBookId, originalId, cancellationToken);
    }

    private async Task EnsureCanManageAsync(
        Guid financialBookId,
        CancellationToken cancellationToken)
    {
        var book = await financialBookRepository.GetByIdAsync(financialBookId, cancellationToken);
        if (book is null)
            throw new JournalEntryAccessDeniedException();
        if (book.IsOwnedBy(currentUser.UserId))
            return;

        var member = await financialBookRepository.GetMemberAsync(
            financialBookId, currentUser.UserId, cancellationToken);
        if (member is null || !member.CanPostJournalEntriesAt(timeProvider.GetUtcNow()))
            throw new JournalEntryAccessDeniedException();
    }

    private static EquityMovementType GetEffectiveType(
        EquityMovementType postedType,
        IReadOnlyList<EquityMovementCorrection> history,
        int lineNumber)
    {
        var latest = history
            .Where(item => item.LineNumber == lineNumber)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .LastOrDefault();

        return latest?.CorrectedMovementType ?? postedType;
    }

    private static EquityMovementCorrectionResult ToResult(EquityMovementCorrection item) =>
        new(item.Id, item.FinancialBookId, item.JournalEntryId, item.LineNumber,
            item.OriginalMovementType.ToString(), item.CorrectedMovementType.ToString(),
            item.Reason, item.CreatedBy, item.CreatedAt);
}


public sealed record CreateEquityMovementCorrectionsInput(
    IReadOnlyList<CreateEquityMovementCorrectionItem> Items);

public sealed record CreateEquityMovementCorrectionItem(
    int LineNumber,
    EquityMovementType ExpectedOriginalMovementType,
    EquityMovementType CorrectedMovementType,
    string Reason);

public sealed record EquityMovementCorrectionResult(
    Guid Id,
    Guid FinancialBookId,
    Guid JournalEntryId,
    int LineNumber,
    string OriginalMovementType,
    string CorrectedMovementType,
    string Reason,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

public sealed class EquityMovementCorrectionConflictException(string message) : Exception(message);
