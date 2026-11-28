using ArtaFin.Application.Features.UserManagement;

namespace ArtaFin.Api.Endpoints;

internal static class UserManagementEndpoints
{
    public static IEndpointRouteBuilder MapUserManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/users")
            .WithTags("System User Management");

        group.MapGet("", async (
                UserManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetUsersAsync(cancellationToken)))
            .WithName("GetSystemUsers")
            .RequireAuthorization("Permission:Users.Read");

        group.MapPut("/{userId:guid}/roles", async (
                Guid userId,
                ReplaceUserRolesRequest request,
                UserManagementService service,
                CancellationToken cancellationToken) =>
            {
                if (request.RoleIds is null)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["roleIds"] = ["Role ids are required."]
                    });

                await service.ReplaceUserRolesAsync(
                    userId, request.RoleIds, cancellationToken);
                return Results.NoContent();
            })
            .WithName("ReplaceSystemUserRoles")
            .RequireAuthorization("Permission:Users.Manage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{userId:guid}/status", async (
                Guid userId,
                UpdateUserStatusRequest request,
                UserManagementService service,
                CancellationToken cancellationToken) =>
            {
                await service.SetUserActiveAsync(
                    userId, request.IsActive, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpdateSystemUserStatus")
            .RequireAuthorization("Permission:Users.Manage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}

internal sealed record ReplaceUserRolesRequest(IReadOnlyList<Guid>? RoleIds);
internal sealed record UpdateUserStatusRequest(bool IsActive);
