using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetFinancingLiabilitiesReconciliation;

public sealed class GetFinancingLiabilitiesReconciliationQueryHandler(
    ICurrentUser currentUser,
    IFinancialBookRepository financialBookRepository,
    IFinancingLiabilitiesReconciliationReadRepository readRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetFinancingLiabilitiesReconciliationQuery, FinancingLiabilitiesReconciliationResult>
{
    public async Task<FinancingLiabilitiesReconciliationResult> Handle(
        GetFinancingLiabilitiesReconciliationQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.From >= query.To)
            throw new ArgumentException("From must be earlier than To.", nameof(query));

        var book = await financialBookRepository.GetByIdAsync(query.FinancialBookId, cancellationToken);
        if (book is null)
            throw new FinancialBookAccessDeniedException();

        if (!book.IsOwnedBy(currentUser.UserId))
        {
            var member = await financialBookRepository.GetMemberAsync(
                book.Id, currentUser.UserId, cancellationToken);
            if (member is null || !member.IsActiveAt(timeProvider.GetUtcNow()))
                throw new FinancialBookAccessDeniedException();
        }

        var snapshot = await readRepository.GetBeforeAsync(book.Id, query.To, cancellationToken);
        var entries = snapshot.Lines
            .GroupBy(line => line.JournalEntryId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var allocations = snapshot.FinalizedAllocations
            .GroupBy(item => (item.JournalEntryId, item.CounterpartLineNumber))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var disclosures = snapshot.NonCashDisclosures
            .ToDictionary(item => (item.JournalEntryId, item.SourceLineNumber));

        var exceptions = new List<FinancingLiabilityMovementExceptionResult>();
        var results = new List<FinancingLiabilityMovementResult>();

        foreach (var accountGroup in snapshot.Lines
                     .Where(line => line.FinancingLiabilityType != FinancingLiabilityType.None)
                     .GroupBy(line => new
                     {
                         line.AccountId,
                         line.AccountCode,
                         line.AccountName,
                         line.FinancingLiabilityType
                     }))
        {
            var accountLines = accountGroup.ToArray();
            var foreignCurrencyLines = accountLines
                .Where(line => !StringComparer.Ordinal.Equals(line.Currency, book.BaseCurrency.Code))
                .ToArray();

            foreach (var line in foreignCurrencyLines)
            {
                exceptions.Add(ToException(
                    "UNSUPPORTED_FINANCING_LIABILITY_CURRENCY",
                    $"Financing liability currency {line.Currency} cannot be translated to book base currency {book.BaseCurrency.Code}.",
                    line,
                    line.Amount));
            }

            var baseCurrencyLines = accountLines
                .Where(line => StringComparer.Ordinal.Equals(line.Currency, book.BaseCurrency.Code))
                .ToArray();

            decimal Signed(FinancingLiabilityLineSnapshot line) =>
                line.Side == PostingSide.Credit ? line.Amount : -line.Amount;

            var opening = baseCurrencyLines
                .Where(line => line.PostedAt < query.From)
                .Sum(Signed);
            var closing = baseCurrencyLines
                .Where(line => line.PostedAt < query.To)
                .Sum(Signed);

            decimal cashChanges = 0m;
            decimal nonCashChanges = 0m;
            decimal unclassifiedChanges = 0m;

            foreach (var line in baseCurrencyLines.Where(line =>
                         line.PostedAt >= query.From && line.PostedAt < query.To))
            {
                var key = (line.JournalEntryId, line.LineNumber);
                var linkedCashAllocations = allocations.TryGetValue(key, out var matchedAllocations)
                    ? matchedAllocations.Where(allocation =>
                    {
                        var sourceLine = entries[line.JournalEntryId]
                            .SingleOrDefault(candidate => candidate.LineNumber == allocation.SourceLineNumber);
                        return sourceLine is not null &&
                               (sourceLine.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent);
                    }).ToArray()
                    : Array.Empty<FinancingLiabilityCashAllocationSnapshot>();

                var cashAmount = linkedCashAllocations.Sum(item => item.Amount);
                var hasDisclosure = disclosures.TryGetValue(key, out var disclosure);
                var signedLineAmount = Signed(line);
                var cashChange = Math.Sign(signedLineAmount) * cashAmount;
                var nonCashChange = hasDisclosure
                    ? Math.Sign(signedLineAmount) * disclosure!.Amount
                    : 0m;

                if (cashAmount > 0m && hasDisclosure)
                {
                    exceptions.Add(ToException(
                        "FINANCING_LIABILITY_CHANGE_DOUBLE_CLASSIFIED",
                        "The same financing-liability journal line is linked to both a cash allocation and a non-cash disclosure.",
                        line,
                        cashAmount + disclosure!.Amount));
                    cashChange = 0m;
                    nonCashChange = 0m;
                }

                var unclassifiedChange = signedLineAmount - cashChange - nonCashChange;
                cashChanges += cashChange;
                nonCashChanges += nonCashChange;
                unclassifiedChanges += unclassifiedChange;

                if (unclassifiedChange != 0m)
                {
                    exceptions.Add(ToException(
                        "UNCLASSIFIED_FINANCING_LIABILITY_CHANGE",
                        "This financing-liability movement is not fully explained by a finalized cash-flow allocation or a linked non-cash disclosure. Classify FX, fair-value, business-combination, and other movements explicitly before treating the reconciliation as complete.",
                        line,
                        unclassifiedChange));
                }
            }

            var difference = closing - opening - cashChanges - nonCashChanges - unclassifiedChanges;
            results.Add(new FinancingLiabilityMovementResult(
                accountGroup.Key.AccountId,
                accountGroup.Key.AccountCode,
                accountGroup.Key.AccountName,
                accountGroup.Key.FinancingLiabilityType,
                opening,
                cashChanges,
                nonCashChanges,
                unclassifiedChanges,
                closing,
                difference));
        }

        var ordered = results
            .OrderBy(item => item.AccountCode, StringComparer.Ordinal)
            .ToArray();
        var totalOpening = ordered.Sum(item => item.OpeningBalance);
        var totalCash = ordered.Sum(item => item.CashChanges);
        var totalNonCash = ordered.Sum(item => item.NonCashChanges);
        var totalUnclassified = ordered.Sum(item => item.UnclassifiedChanges);
        var totalClosing = ordered.Sum(item => item.ClosingBalance);
        var totalDifference = totalClosing - totalOpening - totalCash - totalNonCash - totalUnclassified;

        return new FinancingLiabilitiesReconciliationResult(
            query.From,
            query.To,
            book.BaseCurrency.Code,
            ordered,
            totalOpening,
            totalCash,
            totalNonCash,
            totalUnclassified,
            totalClosing,
            totalDifference,
            exceptions.Count == 0 && totalDifference == 0m,
            exceptions);
    }

    private static FinancingLiabilityMovementExceptionResult ToException(
        string code,
        string message,
        FinancingLiabilityLineSnapshot line,
        decimal amount) =>
        new(code, message, line.JournalEntryId, line.LineNumber, line.PostedAt,
            line.Description, line.AccountId, line.AccountCode, line.AccountName,
            amount, line.Currency);
}
