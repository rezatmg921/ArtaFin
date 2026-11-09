# ADR-0003: Runtime and dependency policy

- **Status:** Accepted
- **Date:** 2026-11-03

## Decision

For a new production deployment, target a supported .NET LTS release (currently .NET 10 LTS). Keep ASP.NET Core and EF Core on compatible, supported versions. A .NET 9 target may be used only for an explicit constraint and requires a documented upgrade plan.

## Quality policy

- Pin SDK through global.json when repository tooling is introduced.
- Keep dependency versions centrally managed where practical.
- Enable nullable reference types and implicit usings.
- Treat compiler warnings as actionable; add analyzers and formatting checks.
- Use migrations reviewed and promoted through deployment pipelines.
