using System.Data;
using System.Security.Cryptography;
using System.Text;
using ArtaFin.Application.Abstractions.Persistence;
using ArtaFin.Application.Features.Accounts.CreateAccount;
using ArtaFin.Application.Features.Accounts.DeleteAccount;
using ArtaFin.Application.Features.JournalEntries.CreateJournalEntry;
using ArtaFin.Application.Features.JournalEntries.ReverseJournalEntry;
using ArtaFin.Domain.Finance;
using ArtaFin.Domain.Security;
using ArtaFin.Infrastructure.Persistence.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtaFin.Infrastructure.Persistence;

public sealed class ArtaFinDbContext(
    DbContextOptions<ArtaFinDbContext> options,
    ILogger<ArtaFinDbContext>? logger = null)
    : DbContext(options), IUnitOfWork
{
    private const string AccountCodeUniqueIndexName = "UX_Accounts_FinancialBookId_Code";
    private const string JournalEntryReversalUniqueIndexName = "UX_JournalEntries_ReversesJournalEntryId";
    private const string JournalLineAccountForeignKeyName = "FK_JournalLines_Accounts_FinancialBookId_AccountId";

    public DbSet<FinancialBook> FinancialBooks => Set<FinancialBook>();
    public DbSet<FinancialBookMember> FinancialBookMembers => Set<FinancialBookMember>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<CashFlowAllocationSet> CashFlowAllocationSets => Set<CashFlowAllocationSet>();
    public DbSet<CashFlowNonCashDisclosure> CashFlowNonCashDisclosures => Set<CashFlowNonCashDisclosure>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinancialBook>();
        modelBuilder.Entity<FinancialBookMember>();
        modelBuilder.Entity<Account>();
        modelBuilder.Entity<JournalEntry>();
        modelBuilder.Entity<CashFlowAllocationSet>();
        modelBuilder.Entity<CashFlowNonCashDisclosure>();
        modelBuilder.Entity<CashFlowAllocation>();
        modelBuilder.Entity<AppUser>();
        modelBuilder.Entity<UserCredential>();
        modelBuilder.Entity<UserSession>();
        modelBuilder.Entity<AppRole>();
        modelBuilder.Entity<UserRole>();

        modelBuilder.ApplyConfiguration(new FinancialBookConfiguration());
        modelBuilder.ApplyConfiguration(new FinancialBookMemberConfiguration());
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new JournalEntryConfiguration());
        modelBuilder.ApplyConfiguration(new CashFlowAllocationSetConfiguration());
        modelBuilder.ApplyConfiguration(new CashFlowNonCashDisclosureConfiguration());
        modelBuilder.ApplyConfiguration(new CashFlowAllocationConfiguration());
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
        EnsureNonCashDisclosuresAreImmutable();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException exception) when (IsAccountCodeUniqueViolation(exception))
        {
            throw new AccountCodeAlreadyExistsException();
        }
        catch (DbUpdateException exception) when (IsJournalEntryReversalUniqueViolation(exception))
        {
            throw new JournalEntryAlreadyReversedException();
        }
        catch (DbUpdateException exception) when (IsAccountDeleteForeignKeyViolation(exception))
        {
            throw new AccountHasJournalEntriesException();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var diagnostics = DescribeConcurrencyConflict(exception);
            LogConcurrencyConflict(exception, diagnostics);
            throw new PersistenceConcurrencyException(
                "The record was changed by another operation. Reload it and retry.",
                exception,
                diagnostics);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken: cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsurePostedJournalEntriesAreImmutable();
        EnsureNonCashDisclosuresAreImmutable();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsAccountCodeUniqueViolation(exception))
        {
            throw new AccountCodeAlreadyExistsException();
        }
        catch (DbUpdateException exception) when (IsJournalEntryReversalUniqueViolation(exception))
        {
            throw new JournalEntryAlreadyReversedException();
        }
        catch (DbUpdateException exception) when (IsAccountDeleteForeignKeyViolation(exception))
        {
            throw new AccountHasJournalEntriesException();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var diagnostics = DescribeConcurrencyConflict(exception);
            LogConcurrencyConflict(exception, diagnostics);
            throw new PersistenceConcurrencyException(
                "The record was changed by another operation. Reload it and retry.",
                exception,
                diagnostics);
        }
    }

    public async Task AcquireIdempotencyLockAsync(
        Guid financialBookId,
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("An idempotency lock requires an active transaction.");

        var lockIdentity = $"{financialBookId:N}:{userId:N}:{idempotencyKey}";
        var lockHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lockIdentity)));
        var resource = $"ArtaFin:Idempotency:{lockHash}";

        await Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Unable to acquire idempotency lock.', 1;",
            cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        try
        {
            await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Must be called inside ExecuteInTransactionAsync. Account rows are locked
    /// in deterministic order until the journal entry is committed, preventing
    /// concurrent postings from spending the same available balance.
    /// </summary>
    public async Task EnsureCanPostAsync(
        Guid financialBookId,
        IReadOnlyCollection<JournalLine> lines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (financialBookId == Guid.Empty || lines.Count == 0)
            return;
        if (Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account balance validation must run inside a posting transaction.");

        var proposedLines = lines
            .GroupBy(line => line.AccountId)
            .OrderBy(group => group.Key)
            .ToArray();

        foreach (var proposed in proposedLines)
        {
            var accountId = proposed.Key;

            // UPDLOCK + HOLDLOCK keeps a per-account write lock until transaction end.
            var lockedAccountId = await Accounts
                .FromSqlInterpolated($@"SELECT * FROM [Accounts] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [FinancialBookId] = {financialBookId} AND [Id] = {accountId}")
                .AsNoTracking()
                .Select(account => account.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (lockedAccountId == Guid.Empty)
                throw new InvalidOperationException("A journal line references an account that no longer exists.");

            // Read policy after acquiring the lock; a tracked account loaded by the
            // command handler before the transaction may contain a stale policy.
            var account = await Accounts
                .AsNoTracking()
                .Where(item => item.FinancialBookId == financialBookId && item.Id == accountId)
                .Select(item => new { item.NormalBalance, item.BalancePolicy, item.Status })
                .SingleAsync(cancellationToken);

            if (account.Status != AccountStatus.Active)
                throw new JournalEntryAccountUnavailableException();

            if (account.BalancePolicy == AccountBalancePolicy.AllowNegative)
                continue;

            var totals = await JournalEntries
                .AsNoTracking()
                .Where(entry => entry.FinancialBookId == financialBookId)
                .SelectMany(entry => entry.Lines, (entry, line) => new
                {
                    line.AccountId,
                    line.Side,
                    Amount = EF.Property<decimal>(line, "_amount")
                })
                .Where(line => line.AccountId == accountId)
                .GroupBy(line => line.AccountId)
                .Select(group => new
                {
                    Debits = group.Sum(line =>
                        line.Side == PostingSide.Debit ? line.Amount : 0m),
                    Credits = group.Sum(line =>
                        line.Side == PostingSide.Credit ? line.Amount : 0m)
                })
                .SingleOrDefaultAsync(cancellationToken);

            var currentBalance = account.NormalBalance == AccountNormalBalance.Debit
                ? (totals?.Debits ?? 0m) - (totals?.Credits ?? 0m)
                : (totals?.Credits ?? 0m) - (totals?.Debits ?? 0m);

            var proposedDelta = proposed.Sum(line =>
            {
                var increasesNormalBalance = account.NormalBalance == AccountNormalBalance.Debit
                    ? line.Side == PostingSide.Debit
                    : line.Side == PostingSide.Credit;

                return increasesNormalBalance ? line.Amount.Amount : -line.Amount.Amount;
            });

            var projectedBalance = currentBalance + proposedDelta;
            // Existing negative balances may come from legacy history. Permit
            // corrective postings that improve them, but never worsen them.
            if (projectedBalance < 0m && proposedDelta < 0m)
                throw new AccountNegativeBalanceException(accountId, projectedBalance);
        }
    }

    private static string DescribeConcurrencyConflict(DbUpdateConcurrencyException exception) =>
        string.Join(" | ", exception.Entries.Select(entry =>
        {
            var key = entry.Properties
                .Where(property => property.Metadata.IsPrimaryKey())
                .Select(property => $"{property.Metadata.Name}={property.CurrentValue ?? property.OriginalValue}");
            var tokens = entry.Properties
                .Where(property => property.Metadata.IsConcurrencyToken)
                .Select(property => property.Metadata.Name);

            return $"{entry.Metadata.ClrType.Name} state={entry.State}; key=[{string.Join(", ", key)}]; concurrencyTokens=[{string.Join(", ", tokens)}]";
        }));

    private void LogConcurrencyConflict(
        DbUpdateConcurrencyException exception,
        string diagnostics)
    {
        logger?.LogError(
            exception,
            "EF Core optimistic concurrency conflict. Affected entries: {ConcurrencyEntries}",
            diagnostics);
    }

    private void EnsureNonCashDisclosuresAreImmutable()
    {
        ChangeTracker.DetectChanges();

        var hasModifiedOrDeletedDisclosure = ChangeTracker.Entries<CashFlowNonCashDisclosure>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (hasModifiedOrDeletedDisclosure)
        {
            throw new InvalidOperationException(
                "Non-cash disclosures are immutable and cannot be modified or deleted.");
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
        var isDeletingAccount = exception.Entries
            .Any(entry => entry.Entity is Account && entry.State == EntityState.Deleted);

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
                    error.Message.Contains(JournalLineAccountForeignKeyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static bool IsJournalEntryReversalUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                var isDuplicateKey = error.Number is 2601 or 2627;
                var isReversalIndex = error.Message.Contains(
                    JournalEntryReversalUniqueIndexName, StringComparison.OrdinalIgnoreCase);

                if (isDuplicateKey && isReversalIndex)
                    return true;
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
                    AccountCodeUniqueIndexName, StringComparison.OrdinalIgnoreCase);

                if (isDuplicateKey && isAccountCodeIndex)
                    return true;
            }
        }

        return false;
    }
}
