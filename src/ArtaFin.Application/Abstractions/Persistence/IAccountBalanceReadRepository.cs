using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

public interface IAccountBalanceReadRepository
{
    Task<AccountBalancePage> GetPageAsync(
        Guid financialBookId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record AccountBalancePage(
    IReadOnlyList<AccountBalanceSnapshot> Items,
    int TotalCount);

public sealed record AccountBalanceSnapshot(
    Guid AccountId,
    string Code,
    string Name,
    AccountType Type,
    AccountNormalBalance NormalBalance,
    AccountStatus Status,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance);
