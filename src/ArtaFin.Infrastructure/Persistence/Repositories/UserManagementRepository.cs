using System.Data;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.UserManagement;
using ArtaFin.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence.Repositories;

internal sealed class UserManagementRepository(ArtaFinDbContext dbContext)
    : IUserManagementRepository
{
    private const string AdministratorRoleName = "ADMINISTRATOR";

    public async Task<IReadOnlyList<ManagedUser>> GetUsersAsync(
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.IsActive,
                user.CreatedAt,
                user.DisabledAt
            })
            .ToListAsync(cancellationToken);

        var result = new List<ManagedUser>(users.Count);
        foreach (var user in users)
        {
            var roleNames = await dbContext.UserRoles
                .AsNoTracking()
                .Where(assignment => assignment.UserId == user.Id)
                .Join(
                    dbContext.Roles.AsNoTracking(),
                    assignment => assignment.RoleId,
                    role => role.Id,
                    (_, role) => role.Name)
                .OrderBy(name => name)
                .ToListAsync(cancellationToken);

            result.Add(new ManagedUser(
                user.Id,
                user.Email,
                user.IsActive,
                user.CreatedAt,
                user.DisabledAt,
                roleNames));
        }

        return result;
    }

    public async Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new ManagedUserNotFoundException(userId);

        var roles = await dbContext.Roles
            .Where(role => roleIds.Contains(role.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != roleIds.Count)
        {
            var foundIds = roles.Select(role => role.Id).ToHashSet();
            throw new UnknownRoleIdsException(roleIds.Where(id => !foundIds.Contains(id)).ToArray());
        }

        var currentlyAdministrator = await dbContext.UserRoles
            .AnyAsync(assignment =>
                assignment.UserId == userId &&
                dbContext.Roles.Any(role =>
                    role.Id == assignment.RoleId &&
                    role.NormalizedName == AdministratorRoleName),
                cancellationToken);

        var willRemainAdministrator = roles.Any(
            role => role.NormalizedName == AdministratorRoleName);

        if (user.IsActive && currentlyAdministrator && !willRemainAdministrator)
            await EnsureAnotherActiveAdministratorExistsAsync(cancellationToken);

        var existingAssignments = await dbContext.UserRoles
            .Where(assignment => assignment.UserId == userId)
            .ToListAsync(cancellationToken);
        dbContext.UserRoles.RemoveRange(existingAssignments);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var role in roles)
        {
            await dbContext.UserRoles.AddAsync(
                UserRole.Assign(Guid.NewGuid(), userId, role.Id, assignedAt),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw new ManagedUserNotFoundException(userId);

        if (user.IsActive == isActive)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (!isActive)
        {
            var isAdministrator = await dbContext.UserRoles
                .AnyAsync(assignment =>
                    assignment.UserId == userId &&
                    dbContext.Roles.Any(role =>
                        role.Id == assignment.RoleId &&
                        role.NormalizedName == AdministratorRoleName),
                    cancellationToken);

            if (isAdministrator)
                await EnsureAnotherActiveAdministratorExistsAsync(cancellationToken);

            user.Disable(changedAt);
        }
        else
        {
            user.Reactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsureAnotherActiveAdministratorExistsAsync(
        CancellationToken cancellationToken)
    {
        var activeAdministratorCount = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .Join(
                dbContext.UserRoles.AsNoTracking(),
                user => user.Id,
                assignment => assignment.UserId,
                (user, assignment) => new { user.Id, assignment.RoleId })
            .Join(
                dbContext.Roles.AsNoTracking(),
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { assignment.Id, role.NormalizedName })
            .Where(item => item.NormalizedName == AdministratorRoleName)
            .Select(item => item.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        if (activeAdministratorCount <= 1)
            throw new LastAdministratorException();
    }
}
