using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetCashFlowStatement;

public sealed class GetCashFlowStatementQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    ICashFlowStatementReadRepository cashFlowStatementReadRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetCashFlowStatementQuery, CashFlowStatementResult>
{
    public async Task<CashFlowStatementResult> Handle(
        GetCashFlowStatementQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.From >= query.To)
            throw new ArgumentException("From must be earlier than To.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(
            query.FinancialBookId, cancellationToken);

        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var member = await financialBookRepository.GetMemberAsync(
                book.Id, currentUser.UserId, cancellationToken);
            if (member is null || !member.IsActiveAt(timeProvider.GetUtcNow()))
                throw new FinancialBookAccessDeniedException();
        }

        var snapshot = await cashFlowStatementReadRepository.GetBeforeAsync(
            book.Id, query.To, cancellationToken);

        var entries = snapshot.Lines
            .GroupBy(line => line.JournalEntryId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var finalized = snapshot.FinalizedAllocations
            .GroupBy(item => (item.JournalEntryId, item.SourceLineNumber))
            .ToDictionary(group => group.Key, group => group.ToArray());

        var drafts = snapshot.Drafts
            .Select(item => (item.JournalEntryId, item.SourceLineNumber))
            .ToHashSet();

        var exceptions = new List<CashFlowStatementExceptionResult>();
        var categoryTotals = new Dictionary<(CashFlowActivity Activity, string Category), (decimal Receipts, decimal Payments)>();

        decimal CashBalanceAt(DateTimeOffset instant) =>
            snapshot.Lines
                .Where(line =>
                    line.PostedAt < instant &&
                    (line.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent) &&
                    StringComparer.Ordinal.Equals(line.Currency, book.BaseCurrency.Code))
                .Sum(line => line.Side == PostingSide.Debit ? line.Amount : -line.Amount);

        var openingCash = CashBalanceAt(query.From);
        var closingCash = CashBalanceAt(query.To);

        foreach (var line in snapshot.Lines.Where(line =>
                     (line.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent) &&
                     !StringComparer.Ordinal.Equals(line.Currency, book.BaseCurrency.Code)))
        {
            exceptions.Add(ToException(
                "UNSUPPORTED_CURRENCY",
                $"Cash movement currency {line.Currency} cannot be converted to the financial book base currency {book.BaseCurrency.Code}; foreign-currency translation is not implemented.",
                line));
        }

        foreach (var entry in entries.Values)
        {
            var cashLines = entry
                .Where(line => line.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent)
                .ToArray();

            if (cashLines.Length == 0)
                continue;

            // A journal entry made exclusively of cash/cash-equivalent accounts
            // is an internal movement and is excluded from external cash flows.
            if (cashLines.Length == entry.Length)
                continue;

            foreach (var cashLine in cashLines.Where(line =>
                         line.PostedAt >= query.From &&
                         line.PostedAt < query.To &&
                         StringComparer.Ordinal.Equals(line.Currency, book.BaseCurrency.Code)))
            {
                var key = (cashLine.JournalEntryId, cashLine.LineNumber);
                if (!finalized.TryGetValue(key, out var allocations) || allocations.Length == 0)
                {
                    exceptions.Add(ToException(
                        drafts.Contains(key) ? "ALLOCATION_DRAFT_NOT_FINALIZED" : "UNCLASSIFIED_CASH_MOVEMENT",
                        drafts.Contains(key)
                            ? "A cash-flow allocation Draft exists but has not been finalized; Draft data is excluded from financial statements."
                            : "This cash/cash-equivalent movement has no finalized cash-flow allocation.",
                        cashLine));
                    continue;
                }

                var invalidCounterpart = allocations.Any(allocation =>
                {
                    if (!allocation.CounterpartLineNumber.HasValue)
                        return true;

                    var counterpart = entry.SingleOrDefault(line =>
                        line.LineNumber == allocation.CounterpartLineNumber.Value);

                    return counterpart is null ||
                           counterpart.Side == cashLine.Side ||
                           counterpart.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent;
                });

                if (invalidCounterpart || allocations.Sum(item => item.Amount) != cashLine.Amount)
                {
                    exceptions.Add(ToException(
                        "INVALID_FINALIZED_ALLOCATION",
                        "The finalized allocation does not fully and validly classify its source cash line.",
                        cashLine));
                    continue;
                }

                foreach (var allocation in allocations)
                {
                    var categoryKey = (allocation.Activity, allocation.Category);
                    categoryTotals.TryGetValue(categoryKey, out var totals);

                    if (cashLine.Side == PostingSide.Debit)
                        totals.Receipts += allocation.Amount;
                    else
                        totals.Payments += allocation.Amount;

                    categoryTotals[categoryKey] = totals;
                }
            }
        }

        var categories = categoryTotals
            .OrderBy(item => item.Key.Activity)
            .ThenBy(item => item.Key.Category, StringComparer.Ordinal)
            .Select(item => new CashFlowCategoryResult(
                item.Key.Activity,
                item.Key.Category,
                item.Value.Receipts,
                item.Value.Payments,
                item.Value.Receipts - item.Value.Payments))
            .ToArray();

        var activities = Enum.GetValues<CashFlowActivity>()
            .Select(activity =>
            {
                var rows = categories.Where(item => item.Activity == activity).ToArray();
                var receipts = rows.Sum(item => item.CashReceipts);
                var payments = rows.Sum(item => item.CashPayments);
                return new CashFlowActivityResult(
                    activity, receipts, payments, receipts - payments);
            })
            .ToArray();

        var netIncreaseDecrease = activities.Sum(item => item.NetCashFlow);
        var reconciliationDifference = closingCash - openingCash - netIncreaseDecrease;
        var isReconciled = exceptions.Count == 0 && reconciliationDifference == 0m;

        return new CashFlowStatementResult(
            query.From,
            query.To,
            book.BaseCurrency.Code,
            openingCash,
            activities,
            categories,
            netIncreaseDecrease,
            closingCash,
            reconciliationDifference,
            isReconciled,
            exceptions,
            snapshot.NonCashDisclosures
                .Where(item => item.PostedAt >= query.From && item.PostedAt < query.To)
                .Select(item => new CashFlowNonCashDisclosureResult(
                    item.Id,
                    item.JournalEntryId,
                    item.SourceLineNumber,
                    item.PostedAt,
                    item.Description,
                    item.Amount,
                    item.Currency,
                    item.Activity,
                    item.Category,
                    item.Memo))
                .ToArray());
    }

    private static CashFlowStatementExceptionResult ToException(
        string code,
        string message,
        CashFlowStatementLineSnapshot line) =>
        new(code, message, line.JournalEntryId, line.LineNumber,
            line.PostedAt, line.Description, line.Amount, line.Currency);
}
