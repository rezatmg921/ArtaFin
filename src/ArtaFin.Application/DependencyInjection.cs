using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace ArtaFin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateAccountCommandHandler>();
        services.AddScoped<AuthenticationService>();
        return services;
    }
}
