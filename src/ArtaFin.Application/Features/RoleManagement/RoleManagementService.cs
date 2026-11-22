using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Security;

namespace ArtaFin.Application.Features.RoleManagement;

public sealed record PermissionCatalogItem(string Code, string Name, string Description);

public sealed class RoleNotFoundException(Guid roleId)
    : Exception($"Role '{roleId}' was not found.");

public sealed class RoleNameAlreadyExistsException(string name)
    : Exception($"A role named '{name}' already exists.");

public sealed class BuiltInRoleIsProtectedException()
    : Exception("Built-in roles cannot be renamed, deleted, or have their permissions changed.");

public sealed class RoleInUseException()
    : Exception("A role assigned to one or more users cannot be deleted.");

public sealed class UnknownPermissionCodesException(IReadOnlyCollection<string> codes)
    : Exception($"Unknown permission codes: {string.Join(", ", codes)}.");

/// <summary>Application use cases for managing custom system roles and catalog permissions.</summary>
public sealed class RoleManagementService(
    IRoleManagementRepository repository,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<ManagedRole>> GetRolesAsync(CancellationToken cancellationToken) =>
        repository.GetRolesAsync(cancellationToken);

    public IReadOnlyList<PermissionCatalogItem> GetPermissions() =>
        PermissionCatalog.All
            .Select(permission => new PermissionCatalogItem(
                permission.Code, permission.Name, permission.Description))
            .ToArray();

    public Task CreateRoleAsync(
        string? name,
        string? description,
        CancellationToken cancellationToken)
    {
        ValidateDetails(name, description);
        return repository.CreateRoleAsync(
            name!.Trim(), description?.Trim(), timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task UpdateRoleAsync(
        Guid roleId,
        string? name,
        string? description,
        CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role id is required.", nameof(roleId));

        ValidateDetails(name, description);
        return repository.UpdateRoleAsync(
            roleId, name!.Trim(), description?.Trim(), cancellationToken);
    }

    public Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role id is required.", nameof(roleId));

        return repository.DeleteRoleAsync(roleId, cancellationToken);
    }

    public Task ReplaceRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string>? permissionCodes,
        CancellationToken cancellationToken)
    {
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role id is required.", nameof(roleId));

        if (permissionCodes is null)
            throw new ArgumentNullException(nameof(permissionCodes));

        var normalizedCodes = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var knownCodes = PermissionCatalog.All
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        var unknownCodes = normalizedCodes
            .Where(code => !knownCodes.Contains(code))
            .ToArray();

        if (unknownCodes.Length > 0)
            throw new UnknownPermissionCodesException(unknownCodes);

        if (normalizedCodes.Length != permissionCodes.Count)
            throw new ArgumentException(
                "Permission codes cannot contain null, empty, or duplicate values.",
                nameof(permissionCodes));

        return repository.ReplaceRolePermissionsAsync(
            roleId, normalizedCodes, timeProvider.GetUtcNow(), cancellationToken);
    }

    private static void ValidateDetails(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > AppRole.MaxNameLength)
            throw new ArgumentException(
                $"Role name is required and cannot exceed {AppRole.MaxNameLength} characters.",
                nameof(name));

        if (description?.Trim().Length > AppRole.MaxDescriptionLength)
            throw new ArgumentException(
                $"Role description cannot exceed {AppRole.MaxDescriptionLength} characters.",
                nameof(description));

        var normalized = name.Trim().ToUpperInvariant();
        if (normalized is "ADMINISTRATOR" or "USER")
            throw new BuiltInRoleIsProtectedException();
    }
}
