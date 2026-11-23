# ADR-0009: Obligation payments and installment allocations

- Status: Accepted
- Date: 2026-11-23
- Related: ADR-0002, ADR-0004, ADR-0006, ADR-0008

## Context

Financial obligations and their immutable contractual installment schedules are implemented. The next capability must record receipts against receivables and disbursements against payables, allocate each payment to installments, prevent over-settlement under concurrent requests, and post the corresponding accounting entry atomically.

The existing ledger supports immutable journal entries, reversal entries, SQL Server transaction-scoped application locks for idempotency, serializable posting transactions, and account-row locking during posting. Journal entries are restricted to the financial book's base currency.

## Decision

### 1. Payment boundary

Introduce a book-scoped `FinancialObligationPayment` aggregate (or equivalently named domain type) representing one posted receipt or disbursement against exactly one `FinancialObligation`.

A payment contains:
- Financial book and obligation identifiers;
- cash/bank account identifier;
- positive amount and currency;
- payment date, description and optional external reference;
- the posted `JournalEntry` identifier;
- authenticated creator and creation timestamp;
- idempotency user, key and normalized request hash.

A payment is immutable after posting. Its allocations are immutable children/records. No update or delete endpoint is provided.

One payment may be allocated to multiple installments, but all allocations in that payment must belong to its single obligation. Supporting one payment across multiple obligations is deliberately deferred.

### 2. Allocation and outstanding amounts

Introduce immutable `FinancialObligationPaymentAllocation` records with payment, obligation, installment and positive amount.

Invariants:
- Every payment has at least one allocation.
- Sum of allocations equals the payment amount exactly.
- Allocation currency is the obligation/payment currency.
- An allocation can only target an installment of the payment's obligation.
- Cumulative allocations for an installment cannot exceed its contractual amount.
- Cumulative payments cannot exceed the obligation's original amount.

Installment remaining amount is derived as contractual amount minus the sum of posted allocations. No mutable paid amount is stored on the installment; the contractual schedule remains immutable and the audit trail remains the source of truth.

The obligation status is derived from cumulative paid amount and is refreshed within the payment transaction:
- zero paid: Open;
- paid greater than zero and less than original amount: PartiallySettled;
- paid equal to original amount: Settled.

The aggregate's payment-status transition must support repeated partial payments and update its timestamp without allowing a cancelled obligation to receive payments.

### 3. Currency policy

Version one supports payments only when the obligation currency equals the financial book's base currency. The payment amount, obligation amount and journal-line amounts must all use that currency.

Foreign-currency settlement, exchange rates, realized FX gains/losses and FX clearing accounts are out of scope until a dedicated multicurrency accounting design is approved. This is consistent with the current journal-posting invariant that every journal entry uses the book's base currency.

### 4. Account validation and journal posting

The cash/bank account must be active, belong to the same financial book and be an Asset account. The obligation's existing control account remains the counter-account.

Journal lines:
- Receivable receipt: Debit cash/bank; Credit receivable control account.
- Payable disbursement: Debit payable control account; Credit cash/bank.

The payment, allocations, obligation status transition and journal entry must be persisted in one database transaction. The transaction must call the existing account posting guard so account status and negative-balance policies are enforced.

The payment's journal entry is created by the payment use case, not by accepting arbitrary journal lines from the API.

### 5. Idempotency

Payment creation requires exactly one `Idempotency-Key` header value. The key is scoped by financial book and authenticated user.

Within the same serializable transaction:
1. Acquire the existing transaction-owned SQL Server application lock for (book, user, key).
2. Look up an existing payment with that scope.
3. If found with the same normalized request hash, return the original payment identifier and do not post again.
4. If found with a different hash, return the existing idempotency conflict (HTTP 409).
5. Otherwise create the payment, allocations and journal entry and commit them together.

The unique database index remains the final duplicate protection. Payment idempotency metadata belongs to the payment record; the journal entry remains the accounting evidence and is linked by a unique relationship.

### 6. Concurrency and transaction isolation

Payment posting uses the existing `IUnitOfWork.ExecuteInTransactionAsync` serializable transaction. Before calculating remaining amounts, lock the target obligation and its installment rows using SQL Server update/hold locks in deterministic order. Recalculate cumulative allocations inside that transaction; do not rely on values previously read by the API or a stale snapshot.

Then validate and create the journal entry, invoking `EnsureCanPostAsync` to lock and validate accounts. A conflict caused by another payment must fail without persisting any part of the attempted operation.

### 7. Reversal policy

Payments and allocations are immutable. A payment must not be reversed by calling the generic journal-entry reversal endpoint directly, because that would leave settlement allocations active while reversing the ledger effect.

A dedicated payment-reversal use case is required to atomically create the reversing journal entry and a durable payment-reversal record, and to restore the obligation's derived settlement status. Until that use case exists, journal entries linked to payments must be protected from generic reversal.

### 8. API and authorization

Expose book-scoped operations:
- Create/post payment for an obligation;
- List payments for an obligation;
- Get a payment by identifier.

Use dedicated permissions `Obligations.Payments.Read` and `Obligations.Payments.Create` (or an equivalent consistent naming convention). Application services must independently enforce book ownership/member role, obligation scope, account scope and active status; API permission alone is insufficient.

The create endpoint returns the payment identifier and the linked journal-entry identifier. Responses include allocation details and computed installment balances where relevant.

## Consequences

- Receivables and payables share one payment workflow; direction determines the journal sides.
- Installment contractual terms remain immutable and payment history is fully traceable.
- Exact allocation prevents unallocated cash from being silently treated as settlement.
- Serializable transactions, application locks and row locks prevent duplicate or concurrent overpayment.
- Version one intentionally excludes multi-obligation payments, foreign currency, payment edits/deletes and payment reversal until their complete accounting workflows are implemented.
