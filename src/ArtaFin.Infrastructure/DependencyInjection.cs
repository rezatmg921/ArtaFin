using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Infrastructure.Persistence;
using ArtaFin.Infrastructure.Persistence.Repositories;
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

        services.AddScoped<IUnitOfWork>(provider =>
            provider.GetRequiredService<ArtaFinDbContext>());
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IFinancialBookRepository, FinancialBookRepository>();

        return services;
    }
}
