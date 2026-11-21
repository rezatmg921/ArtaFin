# SQL Server database initialization

This procedure prepares a new ArtaFin database without deleting or recreating an existing database.

## Prerequisites

- SQL Server database and a deployment identity with permission to apply the reviewed migrations.
- .NET 10 SDK and the repository's restored local tools.
- Environment variables configured outside source control:
  - `ConnectionStrings__ArtaFin`: target SQL Server connection string.
  - `BootstrapAdmin__Email`: email for the first system administrator.
  - `BootstrapAdmin__Password`: strong initial password (at least 12 characters, with uppercase, lowercase, a digit, and a symbol).

Do not place passwords or production connection strings in appsettings files, scripts, or Git.

## Initialize a new database (PowerShell)

Run from the repository root:

```powershell
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__ArtaFin)) {
    throw "Set ConnectionStrings__ArtaFin in the server's protected environment."
}
if ([string]::IsNullOrWhiteSpace($env:BootstrapAdmin__Email) -or
    [string]::IsNullOrEmpty($env:BootstrapAdmin__Password)) {
    throw "Set BootstrapAdmin__Email and BootstrapAdmin__Password in the server's protected environment."
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

dotnet ef database update `
    --project src/ArtaFin.Infrastructure `
    --startup-project src/ArtaFin.Api `
    --context ArtaFinDbContext
if ($LASTEXITCODE -ne 0) { throw "Database migration failed." }

dotnet run --project src/ArtaFin.Api --no-build -- --seed-initial-data
if ($LASTEXITCODE -ne 0) { throw "Initial data seeding failed." }
```

The migration command creates/updates the schema using the checked-in EF Core migration chain. The explicit seed command then creates the built-in `Administrator` and `User` roles and the configured first administrator, including a PBKDF2 password hash. It does not store or log the plaintext password.

## Safe reruns and safeguards

- Seeding is opt-in; normal API startup does not run migrations or seed data.
- The seed operation is transactional and safe to repeat when the configured bootstrap administrator is already the administrator.
- If the configured email belongs to a non-administrator account, or a different administrator already exists, seeding stops rather than silently promoting or creating another administrator.
- Existing passwords are never overwritten.
- The command does not create sample financial books, journal entries, or transactions.
- Standard chart-of-account entries are created when a user creates a financial book; they are not duplicated in this global seed.

After successful initialization, remove the bootstrap password from the process environment/secret injection if it is no longer needed. Keep the administrator's password in the approved secret-management process.

## Production deployment policy

- Generate and review an idempotent migration script for production releases.
- Back up the database and apply the reviewed script using the controlled deployment process.
- Run only the seed command above after the schema is ready.
- Start the API normally without `--seed-initial-data`.
- Never enable `Database.Migrate()` or `EnsureCreated()` at application startup.
