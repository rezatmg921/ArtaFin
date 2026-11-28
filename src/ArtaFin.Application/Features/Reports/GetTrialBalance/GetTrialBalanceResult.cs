using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Reports.GetTrialBalance;

public sealed record TrialBalanceAccountResult(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal DebitBalance,
    decimal CreditBalance);

public sealed record TrialBalanceResult(
    DateTimeOffset AsOf,
    string Currency,
    IReadOnlyList<TrialBalanceAccountResult> Items,
    decimal TotalDebitBalance,
    decimal TotalCreditBalance);
