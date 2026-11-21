namespace ArtaFin.Domain.Finance;

public sealed class AccountNegativeBalanceException(Guid accountId, decimal projectedBalance)
    : Exception($"Posting would make account '{accountId}' balance negative ({projectedBalance}).")
{
    public Guid AccountId { get; } = accountId;
    public decimal ProjectedBalance { get; } = projectedBalance;
}
