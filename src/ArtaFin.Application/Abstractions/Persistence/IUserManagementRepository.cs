namespace ArtaFin.Application.Abstractions.Persistence;

public sealed record ManagedUser(
    Guid Id,
    string Email,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DisabledAt,
    IReadOnlyList<string> RoleNames);

public interface IUserManagementRepository
{
    Task<IReadOnlyList<ManagedUser>> GetUsersAsync(CancellationToken cancellationToken);
    Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken);
    Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken);
}
