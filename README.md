# ArtaFin

A production-minded personal and household finance platform inspired by common personal accounting workflows.

> This project is an independent implementation. It is not affiliated with or a copy of proprietary Parmis software.

## Architecture decisions

- Start with a **Modular Monolith**; do not introduce microservices before operational or scaling needs justify them.
- Apply **DDD** to model financial invariants and bounded contexts.
- Use **CQRS** for clear write workflows and purpose-built read projections; separate databases are not required.
- Keep money movement and ledger posting atomic within a well-defined transaction boundary.
- Use ASP.NET Core Identity for local user accounts and JWT Bearer for API authentication.
- Enforce book-level authorization and tenant isolation in every data access path.

## Proposed stack

- .NET 10 LTS
- ASP.NET Core Web API and Identity
- JWT Bearer authentication
- Entity Framework Core
- SQL Server
- xUnit, integration and architecture tests
- OpenTelemetry and structured logging

## Initial bounded contexts

1. Identity & Access
2. Finance (financial books, accounts, currencies)
3. Transactions (income, expense, transfers, reversals)
4. Accounting (chart of accounts, journal entries, ledger)
5. Contacts & Debts (parties, debts, loans, installments)
6. Planning & Reporting (budgets, recurring transactions, read models)

These are logical module boundaries, not a commitment to one deployable service per context.

## Core financial invariants

- Journal entries balance: total debits equal total credits in the posting currency.
- Posted entries are immutable; corrections use reversal and replacement.
- Money amounts use explicit currency and documented precision/rounding rules.
- Transfers are not income or expense.
- Write requests support idempotency.
- Posting and related records are committed atomically.
- Every resource is scoped to an authorized financial book.
- Derived balances can be reconciled and rebuilt from the authoritative ledger.

## Authentication configuration

The API uses ASP.NET Core Identity for account storage and JWT Bearer for access tokens. The API deliberately does not contain a default signing key or credentials.

For local development, initialize User Secrets and set the JWT settings (use a randomly generated signing key of at least 32 UTF-8 bytes):

```powershell
dotnet user-secrets init --project src/ArtaFin.Api
dotnet user-secrets set "Authentication:Jwt:Issuer" "ArtaFin" --project src/ArtaFin.Api
dotnet user-secrets set "Authentication:Jwt:Audience" "ArtaFin.Api" --project src/ArtaFin.Api
dotnet user-secrets set "Authentication:Jwt:SigningKey" "<random-secret-at-least-32-bytes>" --project src/ArtaFin.Api
```

Configure the SQL Server connection string locally through User Secrets as well:

```powershell
dotnet user-secrets set "ConnectionStrings:ArtaFin" "<your-sql-server-connection-string>" --project src/ArtaFin.Api
```

On the Windows Server VM, provide these values through the hosting service's protected environment configuration or a dedicated secret store; do not put them in appsettings files or source control. ASP.NET Core environment variable names use double underscores, for example `Authentication__Jwt__SigningKey`, `Authentication__Jwt__Issuer`, `Authentication__Jwt__Audience`, and `ConnectionStrings__ArtaFin`.

The API fails at startup if issuer, audience, or a sufficiently long signing key is missing. Access tokens are limited to 5–60 minutes (15 minutes by default). Rotate signing keys using a planned key rollover; replacing the key immediately invalidates existing tokens.

### Identity database migration

The Identity tables are mapped into `ArtaFinDbContext`, alongside the finance tables. Generate and review a migration before running the application against a database:

```powershell
dotnet ef migrations add AddIdentitySchema --project src/ArtaFin.Infrastructure --startup-project src/ArtaFin.Api --context ArtaFinDbContext --output-dir Persistence/Migrations
dotnet ef database update --project src/ArtaFin.Infrastructure --startup-project src/ArtaFin.Api --context ArtaFinDbContext
```

For production, generate an idempotent SQL script, review it, back it up, and apply it during a controlled deployment rather than automatically migrating on API startup:

```powershell
dotnet ef migrations script --idempotent --project src/ArtaFin.Infrastructure --startup-project src/ArtaFin.Api --context ArtaFinDbContext --output .\\artifacts\\AddIdentitySchema.sql
```

### Authentication endpoints

- `POST /api/auth/register` — create a local Identity user and return a short-lived bearer token.
- `POST /api/auth/login` — verify credentials (with Identity lockout protection) and return a bearer token.
- `POST /api/financial-books/{financialBookId}/accounts` — requires a valid bearer token; the user ID is taken only from the authenticated token's subject claim.

Example registration/login body:

```json
{
  "email": "user@example.com",
  "password": "Use-A-Strong-Password-123!"
}
```

## Repository roadmap

- [ ] Architecture Decision Records (ADRs)
- [ ] Solution and module skeleton
- [x] FinancialBook ownership and authorization model
- [x] Identity & JWT authentication foundation
- [ ] Ledger domain and posting invariants
- [ ] EF Core mappings and migrations
- [ ] First vertical slice: create an expense
- [ ] Integration tests against SQL Server
- [ ] Income, transfers, reversals
- [ ] Contacts, debts, installments
- [ ] Budgets and reports
- [ ] CI, security checks, observability and deployment docs

## Development principles

- Keep domain logic independent of EF Core and HTTP.
- Prefer explicit use cases and small interfaces.
- Avoid generic repositories that hide useful database capabilities.
- Do not publish integration events before the database transaction commits; use a transactional outbox.
- Never log credentials, tokens, or sensitive financial details.
- Use reviewed migration scripts for production rollout.

## Status

Identity and JWT authentication plumbing has been added. The Identity schema migration must be generated and reviewed before the API can be used with a database.
