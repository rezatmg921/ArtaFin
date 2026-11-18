# Lease contract domain foundation

This slice introduces the immutable contract terms needed before generating dated
lease schedule periods. It deliberately does not post journal entries or change
historical postings.

## Contract terms

A contract records its financial book, contract number and description, commencement,
first payment and end dates, initial lease liability, three distinct ledger account
roles, payment frequency/timing, and an effective periodic rate.

The rate is explicitly the effective rate **for one configured payment period**.
For example, a monthly contract stores a monthly effective rate. The domain does not
convert an annual rate, assume a day-count basis, or infer payment timing.

The liability, interest-expense, and payment accounts are stored separately. The
application/persistence integration must validate that they belong to the same book,
that the liability account is a lease liability, that the interest account is an
expense, and that the payment account is an eligible asset/cash account.

## Lifecycle

Contracts start Active and may transition once to Completed or Terminated. Posted
journal entries remain immutable; corrections must use the existing reversal model.

## Deliberate boundary

This is a domain-only foundation. Dated variable instalments, period accruals,
persistence, authorization, idempotent posting, and reconciliation are subsequent
slices. A contract cannot yet be used to create or post a payment. Existing ledger
data does not contain sufficient executed contract terms to populate a real contract
without user-supplied evidence.

## Persistence

The `LeaseContracts` table stores the initial liability amount and ISO currency
separately, together with the effective periodic rate at decimal(19,10) precision.
The contract number is unique within a financial book.

The database enforces foreign keys from all three account roles to accounts with
the same `FinancialBookId`. This prevents cross-book account references even if
a caller bypasses application validation. Account *types* and the lease-liability
classification still require application-level validation before a contract is
created.

Migration: `20261118150000_AddLeaseContracts`.

The EF model snapshot is updated with the migration so subsequent migrations can
diff the lease contract mapping correctly.
