# SQL Server database initialization

This procedure prepares a new ArtaFin database without deleting or recreating an existing database.

## One connection-string contract

The entire solution uses exactly one configuration key:

- .NET configuration key: `ConnectionStrings:ArtaFin`
- Environment-variable equivalent: `ConnectionStrings__ArtaFin`

Both the API runtime and EF Core Design-Time factory read this key through `IConfiguration.GetConnectionString("ArtaFin")`. The Design-Time factory loads the API configuration sources in the same order: `appsettings.json`, environment-specific appsettings, Development User Secrets, environment variables, then command-line arguments.

Configuration precedence (later sources override earlier ones):

1. `src/ArtaFin.Api/appsettings.json` — non-secret defaults only.
2. `src/ArtaFin.Api/appsettings.{Environment}.json` — environment-specific, non-secret settings.
3. User Secrets — Development only.
4. Environment variable `ConnectionStrings__ArtaFin` — deployment/CI override.
5. Command-line configuration arguments — when explicitly supplied to the EF tooling process.

Do not use `ARTAFIN_SQLSERVER`, a separate Factory connection string, or any hard-coded server/database fallback. If the key is missing, both runtime and EF tooling fail with an explicit configuration error instead of silently targeting another database.

## Prerequisites

- SQL Server database and a deployment identity with permission to apply the reviewed migrations.
- .NET 10 SDK and the repository's restored local tools.
- Configure `ConnectionStrings:ArtaFin` using one of the supported sources above.
- Configure `BootstrapAdmin__Email` and `BootstrapAdmin__Password` outside source control for first-time seeding.

Do not place passwords or production connection strings in Git or committed appsettings files.

## Development setup (PowerShell)

For local development, store the connection string as an API User Secret (one-time setup):

```powershell
dotnet user-secrets set "ConnectionStrings:ArtaFin" "<your-local-connection-string>" --project .\src\ArtaFin.Api
```

The API and `Update-Database` both load this same User Secret when the environment is Development.

For deployment, configure the single environment variable instead:

```powershell
$env:ConnectionStrings__ArtaFin = "<deployment-connection-string>"
```

Do not set the legacy `ARTAFIN_SQLSERVER` variable. Remove any old value from User/Machine environment variables and restart Visual Studio/terminal after changing persistent environment variables.

## Initialize a new database (PowerShell)

Run from the repository root after configuring the connection string and bootstrap administrator settings:

```powershell
.\scripts\Initialize-ArtaFin.ps1
```

The script restores the repository's local .NET tools, applies the checked-in EF Core migration chain, and invokes the API's one-shot seed command. The seed command creates the built-in `Administrator` and `User` roles and the configured first administrator, including a PBKDF2 password hash. It does not store or log the plaintext password.

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
