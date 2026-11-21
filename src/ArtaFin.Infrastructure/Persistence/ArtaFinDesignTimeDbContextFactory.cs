using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ArtaFin.Infrastructure.Persistence;

/// <summary>
/// Loads the same application configuration sources used by the API host
/// so EF tooling targets exactly the same configured database.
/// </summary>
public sealed class ArtaFinDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ArtaFinDbContext>
{
    public ArtaFinDbContext CreateDbContext(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production;

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false);

        // Match WebApplication.CreateBuilder: User Secrets are a Development-only source.
        if (string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase))
        {
            var apiAssembly = Assembly.Load("ArtaFin.Api");
            configurationBuilder.AddUserSecrets(apiAssembly, optional: true);
        }

        var configuration = configurationBuilder
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration.GetConnectionString("ArtaFin");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ArtaFin' is missing. Configure ConnectionStrings:ArtaFin " +
                "in the API appsettings/User Secrets, or ConnectionStrings__ArtaFin as an environment variable. " +
                "EF Core will not fall back to a different server.");
        }

        var options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ArtaFinDbContext(options);
    }
}
