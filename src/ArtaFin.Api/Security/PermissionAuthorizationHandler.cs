using System.Security.Claims;
using ArtaFin.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Api.Security;

internal sealed class PermissionAuthorizationHandler(
    ArtaFinDbContext dbContext)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var subject = context.User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
            return;

        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted
            ?? CancellationToken.None;

        var hasPermission = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive)
            .Join(
                dbContext.UserRoles.AsNoTracking(),
                user => user.Id,
                assignment => assignment.UserId,
                (_, assignment) => assignment.RoleId)
            .Join(
                dbContext.RolePermissions.AsNoTracking(),
                roleId => roleId,
                grant => grant.RoleId,
                (_, grant) => grant.PermissionId)
            .Join(
                dbContext.Permissions.AsNoTracking(),
                permissionId => permissionId,
                permission => permission.Id,
                (_, permission) => permission.Code)
            .AnyAsync(code => code == requirement.PermissionCode, cancellationToken);

        if (hasPermission)
            context.Succeed(requirement);
    }
}
