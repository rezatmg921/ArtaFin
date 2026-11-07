using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Domain.Finance;
using ArtaFin.Infrastructure.Persistence.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence;

public sealed class ArtaFinDbContext(DbContextOptions<ArtaFinDbContext> options)
    : DbContext(options), IUnitOfWork
{
    private const string AccountCodeUniqueIndexName = "UX_Accounts_FinancialBookId_Code";

    public DbSet<FinancialBook> FinancialBooks => Set<FinancialBook>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Keep runtime entity registration order aligned with the migration snapshot.
        modelBuilder.Entity<FinancialBook>();
        modelBuilder.Entity<Account>();
        modelBuilder.Entity<JournalEntry>();

        modelBuilder.ApplyConfiguration(new FinancialBookConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception) when (IsAccountCodeUniqueViolation(exception))
        {
            throw new AccountCodeAlreadyExistsException();
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken: cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAccountCodeUniqueViolation(exception))
        {
            throw new AccountCodeAlreadyExistsException();
        }
    }

    private static bool IsAccountCodeUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                var isDuplicateKey = error.Number is 2601 or 2627;
                var isAccountCodeIndex = error.Message.Contains(
                    AccountCodeUniqueIndexName,
                    StringComparison.OrdinalIgnoreCase);

                if (isDuplicateKey && isAccountCodeIndex)
                    return true;
            }
        }

        return false;
    }
}
