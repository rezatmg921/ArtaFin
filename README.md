# ArtaFin

A production-minded personal and household finance platform inspired by common personal accounting workflows.

> This project is an independent implementation. It is not affiliated with or a copy of proprietary Parmis software.

## Architecture decisions

- Start with a **Modular Monolith**; do not introduce microservices before operational or scaling needs justify them.
- Apply **DDD** to model financial invariants and bounded contexts.
- Use **CQRS** for clear write workflows and purpose-built read projections; separate databases are not required.
- Keep money movement and ledger posting atomic within a well-defined transaction boundary.
- Use immutable posted journal entries; correct posted financial activity with reversal/correction entries.
- Enforce book-level authorization and tenant isolation in every data access path.

## Proposed stack

- .NET 10 LTS (recommended for new production deployments); .NET 9 only if explicitly required.
- ASP.NET Core Web API
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

## Repository roadmap

- [ ] Architecture Decision Records (ADRs)
- [ ] Solution and module skeleton
- [ ] FinancialBook ownership and authorization model
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

Project initialized. Domain implementation and schema are the next milestones.
