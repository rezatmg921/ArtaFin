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

Run from the repository root after configuring the three environment variables above:

```powershell
.\scripts\Initialize-ArtaFin.ps1
```

The script restores the repository's local .NET tools, applies the checked-in EF Core migration chain, and invokes the API's one-shot seed command. The seed command creates the built-in `Administrator` and `User` roles and the configured first administrator, including a PBKDF2 password hash. It does not store or log the plaintext password.

The administrator role assignment is persisted in the database. The current JWT implementation does not yet emit role claims or enforce role-based authorization policies; this seed step prepares the role records but does not itself add admin-only API permissions.

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
- Run the seed command above only after the schema is ready.
- Start the API normally without `--seed-initial-data`.
- Never enable `Database.Migrate()` or `EnsureCreated()` at application startup.
