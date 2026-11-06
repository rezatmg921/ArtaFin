# ADR-0004: Journal posting invariants

- **Status:** Accepted
- **Date:** 2026-11-03

## Decision

A posted journal entry is an immutable domain record. It requires at least two positive-amount lines, a non-empty financial book, a description, and equal debit/credit totals. The first implementation supports one currency per entry.

## Rationale

The balanced-entry invariant belongs in the domain and must hold regardless of API, job, or persistence entry point. Currency conversion will be introduced only with an explicit FX policy and traceable conversion data.

## Consequences

- Corrections must be represented by compensating/reversal entries, never editing posted lines.
- Persistence and idempotency remain application/infrastructure concerns.
- Account existence, book ownership, and period authorization must be validated by the application transaction before persistence.
- This initial model does not yet define normal balances or account taxonomy.
