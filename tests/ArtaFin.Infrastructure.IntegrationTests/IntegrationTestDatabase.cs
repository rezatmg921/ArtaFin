using System.Data.Common;

namespace ArtaFin.Infrastructure.IntegrationTests;

/// <summary>
/// Integration tests use an explicitly configured SQL Server test connection.
/// The API's ConnectionStrings:ArtaFin setting is never read by this project.
/// </summary>
internal static class IntegrationTestDatabase
{
    private const string DatabaseName = "ArtaFin_IntegrationTests";

    public static string GetConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("ARTAFIN_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "SQL Server integration tests require ARTAFIN_TEST_SQLSERVER. " +
                "Configure a dedicated test connection targeting master. " +
                "The API ConnectionStrings__ArtaFin setting is intentionally ignored.");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = configured };
        var catalogKey = builder.Keys.Cast<string>().FirstOrDefault(key =>
            string.Equals(key, "Initial Catalog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Database", StringComparison.OrdinalIgnoreCase));
        var configuredDatabase = catalogKey is null ? string.Empty : Convert.ToString(builder[catalogKey]);

        if (!string.IsNullOrWhiteSpace(configuredDatabase) &&
            !string.Equals(configuredDatabase, "master", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(configuredDatabase, DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsafe integration-test connection. Database must be 'master' or '{DatabaseName}', " +
                $"not '{configuredDatabase}'.");
        }

        builder[catalogKey ?? "Initial Catalog"] = DatabaseName;
        return builder.ConnectionString;
    }
}
