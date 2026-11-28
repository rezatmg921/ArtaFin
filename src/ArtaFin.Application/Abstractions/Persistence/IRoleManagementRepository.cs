namespace ArtaFin.Application.Abstractions.Persistence;

public sealed record ManagedRole(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    int UserCount,
    IReadOnlyList<string> PermissionCodes);

public interface IRoleManagementRepository
{
    Task<IReadOnlyList<ManagedRole>> GetRolesAsync(CancellationToken cancellationToken);
    Task<Guid> CreateRoleAsync(string name, string? description, DateTimeOffset now, CancellationToken cancellationToken);
    Task UpdateRoleAsync(Guid roleId, string name, string? description, CancellationToken cancellationToken);
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken);
    Task ReplaceRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken);
}
