using ArtaFin.Domain.Common;

namespace ArtaFin.Domain.Security;

/// <summary>Grants one catalog permission to one system role.</summary>
public sealed class RolePermission : Entity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    private RolePermission(Guid id, Guid roleId, Guid permissionId, DateTimeOffset assignedAt)
        : base(id)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        AssignedAt = assignedAt;
    }

    public static RolePermission Assign(
        Guid id,
        Guid roleId,
        Guid permissionId,
        DateTimeOffset assignedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Role-permission id cannot be empty.", nameof(id));
        if (roleId == Guid.Empty)
            throw new ArgumentException("Role id cannot be empty.", nameof(roleId));
        if (permissionId == Guid.Empty)
            throw new ArgumentException("Permission id cannot be empty.", nameof(permissionId));

        return new RolePermission(id, roleId, permissionId, assignedAt);
    }
}
