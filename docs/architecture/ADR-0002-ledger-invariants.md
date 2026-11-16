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

## Account balance convention

Account balances use the account's normal-balance convention. Debit-normal accounts (assets and expenses) increase on debit lines and decrease on credit lines. Credit-normal accounts (liabilities, equity, and income) increase on credit lines and decrease on debit lines. A ledger row's running balance is the cumulative signed sum of that account's posted journal lines, including prior activity before the requested date range. Paginated results carry the cumulative balance forward from all earlier posted lines; balances are projections and are never stored on the Account aggregate.

## Account balance report

The account-balances read model aggregates debit and credit totals from posted journal lines for each account. Its net balance is debits minus credits for debit-normal accounts and credits minus debits for credit-normal accounts. Accounts with no journal activity are returned with zero totals and zero balance. The report is paged by account code and ID, and balances are computed from the ledger rather than persisted as mutable account state.

## Operational negative-balance policy

Account type and balance policy are separate concepts. Account type determines accounting classification and normal balance; balance policy determines whether a posting is allowed to take the account below zero in its normal-balance convention.

- Asset accounts default to `PreventNegative`.
- Non-asset accounts default to `AllowNegative`; the policy can be explicitly changed where a business rule requires it.
- An Asset account that represents an overdraft-enabled bank account can opt into `AllowNegative`.
- The policy is configurable on account creation and through the account update endpoint. Changing it does not rewrite or normalize historical ledger entries.
- The ledger and account-balance reports always show the actual posted balance, including negative balances caused by historical data or policy changes. A read projection must never clamp a negative amount to zero.
- A posting that would newly create or worsen a negative normal balance on a `PreventNegative` account is rejected with HTTP 409. If legacy history has already left an account negative, corrective postings that improve its balance are allowed. The rule applies uniformly to manual journal entries, expenses, income deposits, transfers, and reversals.
- Reversals remain new immutable journal entries. They are subject to the same policy, so a reversal that would create a prohibited negative balance is rejected; users must first post an appropriate correction/reclassification or obtain an explicitly permitted policy change.
- The policy check and journal insert execute in one SQL Server serializable transaction. Account rows are locked in deterministic ID order with update/hold locks until commit, preventing concurrent requests from consuming the same available balance.

This is an operational posting control, not a change to double-entry accounting. A negative ledger balance remains visible and auditable; the control prevents new postings that violate the configured policy.
