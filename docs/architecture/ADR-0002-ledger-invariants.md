# ADR-0002: Treat posted ledger entries as authoritative

- **Status:** Accepted
- **Date:** 2026-11-03

## Decision

The accounting ledger is the authoritative record for posted financial effects. Draft entries may change; posted entries are immutable. Corrections use reversal and, where applicable, replacement entries linked to the original posting.

## Invariants

1. Every posted journal entry balances: debits equal credits in its accounting currency.
2. Currency, scale, rounding policy, and FX treatment are explicit.
3. A transfer between owned accounts is not income or expense.
4. Opening balances are represented by controlled opening entries, not untracked mutable balance changes.
5. Posting, idempotency record, and outbox messages commit atomically.
6. Posting requests are idempotent and concurrency-safe.
7. Book ownership and authorization are checked server-side.
8. Read projections and cached balances are derived and reconcilable to posted ledger data.

## Implementation notes

Choose and document the account normal-balance/sign convention. Cross-currency transfers need explicit source amount, destination amount, rate, fee, and FX accounting. Never use binary floating point for money. Define limits and precision for each supported currency.
