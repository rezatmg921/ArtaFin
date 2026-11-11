using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.DeleteAccount;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Security;
using ArtaFin.Infrastructure.Persistence.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ArtaFin.Infrastructure.Persistence;

public sealed class ArtaFinDbContext(DbContextOptions<ArtaFinDbContext> options)
    : DbContext(options), IUnitOfWork
{
    private const string AccountCodeUniqueIndexName = "UX_Accounts_FinancialBookId_Code";
    private const string JournalLineAccountForeignKeyName = "FK_JournalLines_Accounts_FinancialBookId_AccountId";

    public DbSet<FinancialBook> FinancialBooks => Set<FinancialBook>();
    public DbSet<FinancialBookMember> FinancialBookMembers => Set<FinancialBookMember>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Keep runtime entity registration order aligned with the migration snapshot.
        modelBuilder.Entity<FinancialBook>();
        modelBuilder.Entity<FinancialBookMember>();
        modelBuilder.Entity<Account>();
        modelBuilder.Entity<JournalEntry>();
        modelBuilder.Entity<AppUser>();
        modelBuilder.Entity<UserCredential>();
        modelBuilder.Entity<UserSession>();
        modelBuilder.Entity<AppRole>();
        modelBuilder.Entity<UserRole>();

        modelBuilder.ApplyConfiguration(new FinancialBookConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialBookMemberConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new UserCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new UserSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AppRoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsurePostedJournalEntriesAreImmutable();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception) when (IsAccountCodeUniqueViolation(exception))
        {
            throw new AccountCodeAlreadyExistsException();
        }
        catch (DbUpdateException exception) when (IsAccountDeleteForeignKeyViolation(exception))
        {
            throw new AccountHasJournalEntriesException();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException(
                "The record was changed by another operation. Reload it and retry.", exception);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken: cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsurePostedJournalEntriesAreImmutable();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAccountCodeUniqueViolation(exception))
        {
            throw new AccountCodeAlreadyExistsException();
        }
        catch (DbUpdateException exception) when (IsAccountDeleteForeignKeyViolation(exception))
        {
            throw new AccountHasJournalEntriesException();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException(
                "The record was changed by another operation. Reload it and retry.", exception);
        }
    }

    private void EnsurePostedJournalEntriesAreImmutable()
    {
        ChangeTracker.DetectChanges();

        var hasModifiedOrDeletedEntry = ChangeTracker.Entries<JournalEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        var hasModifiedOrDeletedLine = ChangeTracker.Entries<JournalLine>()
            .Any(line => line.State is EntityState.Modified or EntityState.Deleted);

        if (hasModifiedOrDeletedEntry || hasModifiedOrDeletedLine)
        {
            throw new InvalidOperationException(
                "Posted journal entries and their lines cannot be modified or deleted.");
        }
    }

    private bool IsAccountDeleteForeignKeyViolation(DbUpdateException exception)
    {
        var isDeletingAccount = ChangeTracker.Entries<Account>()
            .Any(entry => entry.State == EntityState.Deleted);

        return isDeletingAccount && IsJournalLineAccountForeignKeyViolation(exception);
    }

    private static bool IsJournalLineAccountForeignKeyViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Number == 547 &&
                    error.Message.Contains(
                        JournalLineAccountForeignKeyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
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
