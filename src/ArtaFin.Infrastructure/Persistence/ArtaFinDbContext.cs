using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence;

public sealed class ArtaFinDbContext(DbContextOptions<ArtaFinDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<FinancialBook> FinancialBooks => Set<FinancialBook>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FinancialBookConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
