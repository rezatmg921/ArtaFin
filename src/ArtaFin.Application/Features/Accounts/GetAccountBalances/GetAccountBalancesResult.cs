using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Features.Accounts.GetAccountBalances;

public sealed record AccountBalanceResult(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    AccountNormalBalance NormalBalance,
    AccountBalancePolicy BalancePolicy,
    AccountStatus Status,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);

public sealed record AccountBalancesResult(
    int Page,
    int PageSize,
    int TotalCount,
    string Currency,
    IReadOnlyList<AccountBalanceResult> Items);
