# ADR-0008: Contacts, financial obligations, loans, and installment settlement

- **Status:** Accepted
- **Date:** 2026-11-23

## Context

ArtaFin already has FinancialBook-scoped accounting, immutable balanced JournalEntry records,
money/currency rules, idempotent financial writes, and a specialized lease-accounting model.
It does not yet provide general counterparties, receivable/payable obligations, loan agreements,
or reusable installment settlement.

The new capability must support amounts owed to the book and amounts owed by the book without
duplicating the lifecycle, installment, payment, authorization, and reporting rules. It must not
weaken the existing journal invariants or absorb the specialized IFRS lease model.

## Decisions

1. Introduce a Contacts capability. A Contact is scoped to one FinancialBook and represents a
   person or organization that can be the counterparty of one or more obligations. Contact
   identity is not inferred from the authenticated user and contact records are not global
   across books by default.

2. Introduce one FinancialObligation aggregate as the common lifecycle root. Its direction is
   explicit:
   - Receivable: the counterparty owes money to the FinancialBook.
   - Payable: the FinancialBook owes money to the counterparty.

   Its source/kind distinguishes a general debt from a loan. Direction must never be inferred
   from a debit/credit account or a display label.

3. Every obligation belongs to exactly one FinancialBook and references exactly one Contact.
   It stores the original amount and currency, effective date, description/reference, due terms,
   status, and the accounting accounts needed by its posting use cases. Account references must
   be validated as belonging to the same FinancialBook.

4. Use a reusable installment schedule for obligations. A non-installment obligation is
   represented by one due item, rather than requiring a separate payment implementation.
   Installments contain due date and scheduled principal/interest/fee components where
   applicable. Amounts and allocations use the existing Money/currency policy.

5. Record actual cash settlement separately from the contractual schedule. An obligation payment
   is an immutable financial operation with one or more explicit allocations to installments.
   Partial payments are supported. By default, overpayment beyond the outstanding amount is
   rejected; unapplied customer/supplier credit is a separate future capability.

6. The authoritative settlement history is the payment and allocation records linked to immutable
   JournalEntry records. Payment, allocations, obligation/installment settlement state, and the
   journal posting must be committed in one database transaction. A failed posting must not leave
   a payment marked as completed, and a failed payment must not leave a journal entry behind.

7. Financial write endpoints require an idempotency key. Repeating the same key and same request
   returns the original result; reusing the key with a different request is a conflict. Database
   uniqueness constraints and transaction-level concurrency protection prevent duplicate
   settlement under concurrent requests.

8. Accounting direction is explicit:
   - Receivable collection: debit the selected cash/bank account and credit the receivable
     control account; interest income, if any, is credited separately.
   - Payable settlement: debit the payable control account and credit the selected cash/bank
     account; interest expense, if any, is debited separately.
   - Loan drawdown and principal/interest repayment are separate posting use cases. A repayment
     must allocate its principal and interest components explicitly; it must not treat the entire
     installment as principal.

9. Loan-specific terms are an extension of the common obligation, not a second independent
   receivable/payable lifecycle. Loan schedule generation is a separate domain service/policy
   (rate basis, compounding, day-count convention, payment frequency, rounding, and final
   installment adjustment must be explicit). The first implementation must not silently assume
   a universal interest convention.

10. LeaseContract and LeasePaymentSchedule remain in LeaseAccounting. Their effective-interest
    calculations and accounting treatment are specialized and must not be replaced by the
    generic debt/loan schedule.

11. All reads and writes are scoped by FinancialBook. API authorization permission checks are
    necessary but not sufficient: the application layer must also verify the caller's membership
    and role for the requested book, and repositories must include FinancialBookId in lookups.

12. Posted JournalEntry records remain immutable. Corrections to settled financial activity use
    reversal/correction journal entries and corresponding explicit business reversal workflows;
    posted payments are never silently edited or deleted.

## Initial implementation sequence

1. Contact domain, persistence, use cases, endpoints, permissions, and tests.
2. FinancialObligation domain and persistence for receivable/payable general debts.
3. Draft/finalize schedule and single-due-item support.
4. Partial/full payment allocation with idempotency, transaction, and journal posting.
5. Loan terms and schedule-generation policies after the general obligation settlement path is
   proven.
6. Read models for outstanding, overdue, aging, and book-level reconciliation.

## Consequences

- Receivables and payables share lifecycle and settlement mechanics while retaining explicit
  direction-specific accounting.
- A single installment/payment engine can serve general debts and loans.
- Loan calculation policies remain replaceable and testable without coupling them to HTTP or EF.
- The existing lease module remains stable and specialized.
- Payment posting requires careful integration with the existing UnitOfWork and JournalEntry
  repository so that all writes share the same DbContext transaction.
