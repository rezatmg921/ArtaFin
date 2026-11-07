using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArtaFin.Infrastructure.Persistence.Migrations;

public sealed partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.14")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        // Register entity types first so navigation discovery matches the runtime DbContext.
        modelBuilder.Entity<FinancialBook>();
        modelBuilder.Entity<Account>();
        modelBuilder.Entity<JournalEntry>();

        modelBuilder.ApplyConfiguration(new FinancialBookConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
    }
}
