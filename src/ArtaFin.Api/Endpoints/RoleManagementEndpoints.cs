using ArtaFin.Application.Features.RoleManagement;

namespace ArtaFin.Api.Endpoints;

internal static class RoleManagementEndpoints
{
    public static IEndpointRouteBuilder MapRoleManagementEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .WithTags("System Role Management");

        group.MapGet("/permissions", (RoleManagementService service) =>
                Results.Ok(service.GetPermissions()))
            .WithName("GetPermissionCatalog")
            .RequireAuthorization("Permission:Roles.Read");

        group.MapGet("/roles", async (
                RoleManagementService service,
                CancellationToken cancellationToken) =>
                Results.Ok(await service.GetRolesAsync(cancellationToken)))
            .WithName("GetSystemRoles")
            .RequireAuthorization("Permission:Roles.Read");

        group.MapPost("/roles", async (
                CreateRoleRequest request,
                RoleManagementService service,
                CancellationToken cancellationToken) =>
            {
                var roleId = await service.CreateRoleAsync(
                    request.Name, request.Description, cancellationToken);
                return Results.Created($"/api/admin/roles/{roleId}", new { Id = roleId });
            })
            .WithName("CreateSystemRole")
            .RequireAuthorization("Permission:Roles.Manage")
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/roles/{roleId:guid}", async (
                Guid roleId,
                UpdateRoleRequest request,
                RoleManagementService service,
                CancellationToken cancellationToken) =>
            {
                await service.UpdateRoleAsync(
                    roleId, request.Name, request.Description, cancellationToken);
                return Results.NoContent();
            })
            .WithName("UpdateSystemRole")
            .RequireAuthorization("Permission:Roles.Manage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/roles/{roleId:guid}", async (
                Guid roleId,
                RoleManagementService service,
                CancellationToken cancellationToken) =>
            {
                await service.DeleteRoleAsync(roleId, cancellationToken);
                return Results.NoContent();
            })
            .WithName("DeleteSystemRole")
            .RequireAuthorization("Permission:Roles.Manage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/roles/{roleId:guid}/permissions", async (
                Guid roleId,
                ReplaceRolePermissionsRequest request,
                RoleManagementService service,
                CancellationToken cancellationToken) =>
            {
                if (request.PermissionCodes is null)
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["permissionCodes"] = ["Permission codes are required."]
                    });

                await service.ReplaceRolePermissionsAsync(
                    roleId, request.PermissionCodes, cancellationToken);
                return Results.NoContent();
            })
            .WithName("ReplaceSystemRolePermissions")
            .RequireAuthorization("Permission:Roles.Manage")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}

internal sealed record CreateRoleRequest(string? Name, string? Description);
internal sealed record UpdateRoleRequest(string? Name, string? Description);
internal sealed record ReplaceRolePermissionsRequest(IReadOnlyList<string>? PermissionCodes);
