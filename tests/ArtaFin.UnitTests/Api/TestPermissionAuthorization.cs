using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ArtaFin.UnitTests.Api;

/// <summary>
/// API behavior tests replace the database-backed permission handler. Authorization
/// policy behavior is tested separately; endpoint tests should not require SQL Server.
/// </summary>
internal sealed class TestPermissionAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements.ToArray())
        {
            if (requirement.GetType().Name == "PermissionRequirement")
                context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal static class TestPermissionAuthorizationExtensions
{
    public static IServiceCollection UseTestPermissionAuthorization(
        this IServiceCollection services)
    {
        var productionHandlers = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IAuthorizationHandler) &&
                descriptor.ImplementationType?.Name == "PermissionAuthorizationHandler")
            .ToArray();

        foreach (var handler in productionHandlers)
            services.Remove(handler);

        services.AddSingleton<IAuthorizationHandler, TestPermissionAuthorizationHandler>();
        return services;
    }
}
