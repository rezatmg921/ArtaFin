# SQL Server database migrations

ArtaFin uses EF Core migrations as the versioned source of truth for the SQL Server schema.

## Create a migration

From the repository root:

```bash
dotnet tool restore
dotnet ef migrations add <MigrationName> \
  --project src/ArtaFin.Infrastructure/ArtaFin.Infrastructure.csproj \
  --startup-project src/ArtaFin.Infrastructure/ArtaFin.Infrastructure.csproj
```

The Infrastructure project contains a design-time context factory. EF tooling does not need a reachable SQL Server instance to build the model. If a connection string is needed for other tooling, set `ARTAFIN_SQLSERVER` or `ConnectionStrings__ArtaFin`.

## Generate an idempotent deployment script

```bash
mkdir -p artifacts
dotnet ef migrations script --idempotent \
  --project src/ArtaFin.Infrastructure/ArtaFin.Infrastructure.csproj \
  --startup-project src/ArtaFin.Infrastructure/ArtaFin.Infrastructure.csproj \
  --output artifacts/ArtaFin.sql
```

Review the generated SQL before deployment. Apply it through the controlled database deployment process using an appropriately privileged deployment identity. Keep a copy of the exact script and deployment result with the release record.

## Runtime policy

- Application startup must not call `Database.Migrate()` or `EnsureCreated()`.
- Production schema changes are deployment-controlled, not performed by application instances.
- Integration tests use disposable SQL Server databases and call `Database.MigrateAsync()` to exercise the real migration chain.
- Do not use `EnsureCreated` for a database that is expected to be managed by migrations.
- Generate and review a new migration whenever the EF model changes; CI checks for model changes not represented by a migration.
