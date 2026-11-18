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
