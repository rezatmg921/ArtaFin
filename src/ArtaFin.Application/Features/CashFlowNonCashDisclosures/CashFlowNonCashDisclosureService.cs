using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Domain.Common;
using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.CashFlowNonCashDisclosures;

public sealed class CashFlowNonCashDisclosureService(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    ICashFlowNonCashDisclosureRepository disclosureRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<CashFlowNonCashDisclosureResult> CreateAsync(
        Guid financialBookId,
        Guid journalEntryId,
        int lineNumber,
        CreateCashFlowNonCashDisclosureInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        await EnsureCanManageAsync(financialBookId, cancellationToken);

        if (lineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(lineNumber));

        CashFlowNonCashDisclosure? created = null;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var source = await disclosureRepository.GetSourceLineAsync(
                financialBookId, journalEntryId, lineNumber, token);

            if (source is null)
                throw new CashFlowNonCashDisclosureSourceNotFoundException();

            if (source.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent)
                throw new CashFlowNonCashDisclosureSourceInvalidException(
                    "A Cash or CashEquivalent journal line cannot be disclosed as a non-cash transaction.");

            if (source.Amount != input.Amount ||
                !StringComparer.Ordinal.Equals(source.Currency, input.Currency?.Trim().ToUpperInvariant()))
                throw new CashFlowNonCashDisclosureSourceInvalidException(
                    "Disclosure amount and currency must match the selected posted journal line.");

            created = CashFlowNonCashDisclosure.Create(
                Guid.NewGuid(), financialBookId, journalEntryId, lineNumber,
                input.Amount, input.Currency, input.Activity, input.Category,
                input.Memo, currentUser.UserId, timeProvider.GetUtcNow());

            await disclosureRepository.AddAsync(created, token);
            await disclosureRepository.SaveChangesAsync(token);
        }, cancellationToken);

        return ToResult(created!);
    }

    public async Task<IReadOnlyList<CashFlowNonCashDisclosureResult>> GetByJournalEntryAsync(
        Guid financialBookId,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(financialBookId, cancellationToken);
        var source = await disclosureRepository.GetByJournalEntryAsync(
            financialBookId, journalEntryId, cancellationToken);
        return source.Select(ToResult).ToArray();
    }

    private async Task EnsureCanManageAsync(Guid financialBookId, CancellationToken cancellationToken)
    {
        if (financialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(financialBookId));

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

    private static CashFlowNonCashDisclosureResult ToResult(CashFlowNonCashDisclosure item) =>
        new(item.Id, item.FinancialBookId, item.JournalEntryId, item.SourceLineNumber,
            item.Amount, item.Currency, item.Activity.ToString(), item.Category,
            item.Memo, item.CreatedBy, item.CreatedAt);
}

public sealed record CreateCashFlowNonCashDisclosureInput(
    decimal Amount,
    string Currency,
    CashFlowActivity Activity,
    string Category,
    string? Memo);

public sealed record CashFlowNonCashDisclosureResult(
    Guid Id,
    Guid FinancialBookId,
    Guid JournalEntryId,
    int SourceLineNumber,
    decimal Amount,
    string Currency,
    string Activity,
    string Category,
    string? Memo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

public sealed class CashFlowNonCashDisclosureSourceNotFoundException : Exception { }
public sealed class CashFlowNonCashDisclosureSourceInvalidException(string message) : Exception(message);
public sealed class CashFlowNonCashDisclosureAlreadyExistsException : Exception { }
