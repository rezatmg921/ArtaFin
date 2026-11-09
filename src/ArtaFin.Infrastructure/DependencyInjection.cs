using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Abstractions.Security;
using ArtaFin.Infrastructure.Persistence;
using ArtaFin.Infrastructure.Persistence.Repositories;
using ArtaFin.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArtaFin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ArtaFin");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ArtaFin' was not configured.");
        }

        services.AddDbContext<ArtaFinDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<ArtaFinDbContext>());
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IFinancialBookRepository, FinancialBookRepository>();

        return services;
    }
}
