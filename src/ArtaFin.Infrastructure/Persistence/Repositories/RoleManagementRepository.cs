using System.Data;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.RoleManagement;
using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class RoleManagementRepository(ArtaFinDbContext dbContext)
    : IRoleManagementRepository
{
    public async Task<IReadOnlyList<ManagedRole>> GetRolesAsync(
        CancellationToken cancellationToken)
    {
        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                role.Id,
                role.Name,
                role.Description,
                role.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var result = new List<ManagedRole>(roles.Count);
        foreach (var role in roles)
        {
            var permissionCodes = await dbContext.RolePermissions
                .AsNoTracking()
                .Where(grant => grant.RoleId == role.Id)
                .Join(
                    dbContext.Permissions.AsNoTracking(),
                    grant => grant.PermissionId,
                    permission => permission.Id,
                    (_, permission) => permission.Code)
                .OrderBy(code => code)
                .ToListAsync(cancellationToken);

            var userCount = await dbContext.UserRoles
                .AsNoTracking()
                .CountAsync(assignment => assignment.RoleId == role.Id, cancellationToken);

            result.Add(new ManagedRole(
                role.Id,
                role.Name,
                role.Description,
                role.CreatedAt,
                userCount,
                permissionCodes));
        }

        return result;
    }

    public async Task<Guid> CreateRoleAsync(
        string name,
        string? description,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToUpperInvariant();
        if (await dbContext.Roles.AnyAsync(
                role => role.NormalizedName == normalizedName, cancellationToken))
            throw new RoleNameAlreadyExistsException(name);

        var role = AppRole.Create(Guid.NewGuid(), name, description, now);
        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return role.Id;
    }

    public async Task UpdateRoleAsync(
        Guid roleId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken)
            ?? throw new RoleNotFoundException(roleId);

        EnsureCustomRole(role);
        var normalizedName = name.Trim().ToUpperInvariant();
        if (await dbContext.Roles.AnyAsync(
                item => item.Id != roleId && item.NormalizedName == normalizedName,
                cancellationToken))
            throw new RoleNameAlreadyExistsException(name);

        role.UpdateDetails(name, description);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var role = await dbContext.Roles
            .SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken)
            ?? throw new RoleNotFoundException(roleId);

        EnsureCustomRole(role);
        if (await dbContext.UserRoles.AnyAsync(
                assignment => assignment.RoleId == roleId, cancellationToken))
            throw new RoleInUseException();

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReplaceRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var role = await dbContext.Roles
            .SingleOrDefaultAsync(item => item.Id == roleId, cancellationToken)
            ?? throw new RoleNotFoundException(roleId);

        EnsureCustomRole(role);

        var permissions = await dbContext.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .ToListAsync(cancellationToken);

        if (permissions.Count != permissionCodes.Count)
        {
            var persistedCodes = permissions.Select(permission => permission.Code)
                .ToHashSet(StringComparer.Ordinal);
            throw new UnknownPermissionCodesException(
                permissionCodes.Where(code => !persistedCodes.Contains(code)).ToArray());
        }

        var existingGrants = await dbContext.RolePermissions
            .Where(grant => grant.RoleId == roleId)
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existingGrants);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            await dbContext.RolePermissions.AddAsync(
                RolePermission.Assign(
                    Guid.NewGuid(), roleId, permission.Id, assignedAt),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void EnsureCustomRole(AppRole role)
    {
        if (role.NormalizedName is "ADMINISTRATOR" or "USER")
            throw new BuiltInRoleIsProtectedException();
    }
}
