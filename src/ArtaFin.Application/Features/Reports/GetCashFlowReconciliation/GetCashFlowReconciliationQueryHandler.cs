using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Reports.GetCashFlowStatement;
using ArtaFin.Application.Features.Reports.GetTrialBalance;
using ArtaFin.Domain.Finance;
using MediatR;

namespace ArtaFin.Application.Features.Reports.GetCashFlowReconciliation;

public sealed class GetCashFlowReconciliationQueryHandler(
    ISender sender,
    ICashFlowStatementReadRepository cashFlowStatementReadRepository)
    : IRequestHandler<GetCashFlowReconciliationQuery, CashFlowReconciliationResult>
{
    public async Task<CashFlowReconciliationResult> Handle(
        GetCashFlowReconciliationQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.FinancialBookId == Guid.Empty)
            throw new ArgumentException("Financial book id cannot be empty.", nameof(query));
        if (query.From >= query.To)
            throw new ArgumentException("From must be earlier than To.", nameof(query));

        // Both trial-balance snapshots use the exact Cash Flow period boundaries.
        // The nested report handlers enforce financial-book access permissions.
        // TrialBalance's as-of repository is inclusive (PostedAt <= AsOf), while
        // Cash Flow uses an exclusive upper boundary (PostedAt < instant).
        // Move each snapshot cutoff back by one tick to align both reports.
        var openingTrialBalance = await sender.Send(
            new GetTrialBalanceQuery(query.FinancialBookId, query.From.AddTicks(-1)), cancellationToken);
        var closingTrialBalance = await sender.Send(
            new GetTrialBalanceQuery(query.FinancialBookId, query.To.AddTicks(-1)), cancellationToken);
        var cashFlow = await sender.Send(
            new GetCashFlowStatementQuery(query.FinancialBookId, query.From, query.To),
            cancellationToken);

        var snapshot = await cashFlowStatementReadRepository.GetBeforeAsync(
            query.FinancialBookId, query.To, cancellationToken);

        var cashAccountIds = snapshot.Lines
            .Where(line => line.PostedAt < query.To &&
                line.CashFlowAccountType is CashFlowAccountType.Cash or CashFlowAccountType.CashEquivalent &&
                StringComparer.Ordinal.Equals(line.Currency, closingTrialBalance.Currency))
            .Select(line => line.AccountId)
            .ToHashSet();

        var opening = openingTrialBalance.Items.Where(item => cashAccountIds.Contains(item.AccountId))
            .ToDictionary(item => item.AccountId);
        var closing = closingTrialBalance.Items.Where(item => cashAccountIds.Contains(item.AccountId))
            .ToDictionary(item => item.AccountId);

        // Use the union so accounts introduced during the period are not omitted.
        var accounts = opening.Keys.Union(closing.Keys)
            .Select(accountId =>
            {
                opening.TryGetValue(accountId, out var openingItem);
                closing.TryGetValue(accountId, out var closingItem);
                var current = closingItem ?? openingItem!;
                return new CashFlowReconciliationAccountResult(
                    current.AccountId,
                    current.Code,
                    current.Name,
                    (closingItem?.TotalDebits ?? 0m) - (openingItem?.TotalDebits ?? 0m),
                    (closingItem?.TotalCredits ?? 0m) - (openingItem?.TotalCredits ?? 0m));
            })
            .OrderBy(item => item.AccountCode, StringComparer.Ordinal)
            .ToArray();

        var cashDebits = accounts.Sum(item => item.Debits);
        var cashCredits = accounts.Sum(item => item.Credits);
        var internalTransfers = cashFlow.InternalCashTransfers
            .Where(item => StringComparer.Ordinal.Equals(item.Currency, closingTrialBalance.Currency))
            .Sum(item => item.Amount);

        var externalInflows = cashDebits - internalTransfers;
        var externalOutflows = cashCredits - internalTransfers;
        var receiptsDifference = externalInflows - cashFlow.Activities.Sum(item => item.CashReceipts);
        var paymentsDifference = externalOutflows - cashFlow.Activities.Sum(item => item.CashPayments);

        return new CashFlowReconciliationResult(
            query.From,
            query.To,
            query.To,
            closingTrialBalance.Currency,
            cashDebits,
            cashCredits,
            internalTransfers,
            externalInflows,
            externalOutflows,
            cashFlow.Activities.Sum(item => item.CashReceipts),
            cashFlow.Activities.Sum(item => item.CashPayments),
            receiptsDifference,
            paymentsDifference,
            cashFlow.IsReconciled &&
                cashFlow.From == query.From &&
                cashFlow.To == query.To &&
                receiptsDifference == 0m &&
                paymentsDifference == 0m,
            accounts);
    }
}
