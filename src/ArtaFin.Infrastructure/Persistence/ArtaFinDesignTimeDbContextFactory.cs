using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArtaFin.Infrastructure.Persistence;

/// <summary>
/// Supplies EF tooling with the SQL Server provider without requiring a live
/// database connection. Runtime connection configuration remains in DI.
/// </summary>
public sealed class ArtaFinDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<ArtaFinDbContext>
{
    public ArtaFinDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ARTAFIN_SQLSERVER")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__ArtaFin")
            ?? "Server=IT-PC1\\MSSQLIT1;Database=ArtaFin_DesignTime;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<ArtaFinDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ArtaFinDbContext(options);
    }
}
