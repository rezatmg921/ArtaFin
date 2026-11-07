# ADR-0005: Account aggregate and persistence port

- **Status:** Accepted
- **Date:** 2026-11-07

## Context

The Finance domain needs a stable account identity that can be referenced by journal lines without loading or embedding an Account object in every JournalEntry. Account lifecycle and account-code uniqueness must be controlled independently from journal posting.

## Decision

- Account is an Aggregate Root and belongs to exactly one FinancialBook through FinancialBookId.
- Account code is required, trimmed, and normalized to uppercase at creation. Its uniqueness scope is one FinancialBook.
- Account name is required and can be renamed through a domain method.
- Account types are Asset, Liability, Equity, Income, and Expense.
- Initial normal-balance convention: Asset and Expense are Debit-normal; Liability, Equity, and Income are Credit-normal.
- Account balance is derived from posted JournalEntry data; it is not a mutable property of Account.
- Account lifecycle starts Active and can transition to Inactive. Reactivation is an explicit domain transition. Physical deletion is not part of this model.
- JournalLine references Account by AccountId, not by holding an Account object.
- The Account repository contract and Unit of Work contract live in Application as ports required by use cases. Infrastructure will implement them.
- The repository's ExistsByCodeAsync is only an early validation. A database unique constraint on (FinancialBookId, normalized Code) is required to guarantee uniqueness under concurrency.

## Consequences

- Account Aggregate protects its own state and lifecycle invariants.
- Application validates that the FinancialBook exists and that account codes are not already used before adding an Account.
- Persistence must enforce the same account-code uniqueness rule with a unique index.
- The first taxonomy does not yet model contra-account overrides, subaccounts, account groups, or multi-currency account denominations. These require explicit domain decisions before implementation.
- EF Core mapping and a concrete database provider are deferred to a separate implementation step; the current repository port introduces no ORM dependency into Domain or Application.
