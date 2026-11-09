using ArtaFin.Domain.Finance;

namespace ArtaFin.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for loading a FinancialBook and its book-scoped memberships.
/// </summary>
public interface IFinancialBookRepository
{
    Task<FinancialBook?> GetByIdAsync(
        Guid financialBookId,
        CancellationToken cancellationToken);

    Task<FinancialBookMember?> GetMemberAsync(
        Guid financialBookId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialBookMemberSummary>> GetActiveMembersAsync(
        Guid financialBookId,
        CancellationToken cancellationToken);

    Task AddMemberAsync(
        FinancialBookMember member,
        CancellationToken cancellationToken);
}
