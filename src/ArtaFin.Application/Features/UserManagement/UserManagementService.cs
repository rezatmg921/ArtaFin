using ArtaFin.Application.Abstractions.Persistence;

namespace ArtaFin.Application.Features.UserManagement;

public sealed class ManagedUserNotFoundException(Guid userId)
    : Exception($"User '{userId}' was not found.");

public sealed class LastAdministratorException()
    : Exception("The operation would leave ArtaFin without an active Administrator.");

public sealed class UnknownRoleIdsException(IReadOnlyCollection<Guid> roleIds)
    : Exception($"Unknown role ids: {string.Join(", ", roleIds)}.");

public sealed class InvalidRoleAssignmentException(string message) : Exception(message);

/// <summary>Application use cases for system-user role assignment and account activation.</summary>
public sealed class UserManagementService(
    IUserManagementRepository repository,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<ManagedUser>> GetUsersAsync(CancellationToken cancellationToken) =>
        repository.GetUsersAsync(cancellationToken);

    public Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? roleIds,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new InvalidRoleAssignmentException("User id is required.");

        if (roleIds is null)
            throw new InvalidRoleAssignmentException("Role ids are required.");

        if (roleIds.Any(id => id == Guid.Empty) || roleIds.Distinct().Count() != roleIds.Count)
            throw new InvalidRoleAssignmentException("Role ids cannot contain empty or duplicate values.");

        return repository.ReplaceUserRolesAsync(
            userId, roleIds, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new InvalidRoleAssignmentException("User id is required.");

        return repository.SetUserActiveAsync(
            userId, isActive, timeProvider.GetUtcNow(), cancellationToken);
    }
}
