using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>Assigns one role to one user.</summary>
public sealed class UserRole : Entity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    private UserRole(Guid id, Guid userId, Guid roleId, DateTimeOffset assignedAt)
        : base(id)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedAt = assignedAt;
    }

    public static UserRole Assign(
        Guid id,
        Guid userId,
        Guid roleId,
        DateTimeOffset assignedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("User-role id cannot be empty.", nameof(id));
        if (userId == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role id cannot be empty.", nameof(roleId));

        return new UserRole(id, userId, roleId, assignedAt);
    }
}
