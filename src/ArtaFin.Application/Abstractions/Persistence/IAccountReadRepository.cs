using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Read-only persistence port for paged account queries.
/// Kept separate from the command repository so write-side fakes and handlers
/// do not need to implement query-specific operations.
/// </summary>
public interface IAccountReadRepository
{
    Task<AccountPage> GetPageAsync(
        Guid financialBookId,
        AccountStatus? status,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

/// <summary>
/// A page of accounts and the total number of matching records before paging.
/// </summary>
public sealed record AccountPage(IReadOnlyList<Account> Items, int TotalCount);
