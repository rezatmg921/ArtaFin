using Microsoft.Extensions.DependencyInjection;

namespace ArtaFin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
