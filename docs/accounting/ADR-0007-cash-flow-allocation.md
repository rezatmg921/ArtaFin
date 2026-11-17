# ADR-0007: Auditable Cash-Flow Allocation

- Status: Accepted
- Date: 2026-11-17
- Scope: IAS 7 direct-method cash-flow classification

## Context

Posted journal entries and their journal lines are immutable. A single journal entry may contain multiple cash movements and a single cash movement may represent several economic components (for example, loan principal and interest). An account-level flag or a journal-entry-level category cannot represent these cases without losing classification detail.

Cash and cash equivalents are already identified on asset accounts. The reporting layer must classify only movements on those accounts, preserve source-line traceability, eliminate transfers wholly within cash and cash equivalents, and surface incomplete classifications instead of assuming that they are operating cash flows.

## Decision

Keep cash-flow classification in a separate, auditable allocation aggregate. Never update a posted JournalEntry or JournalLine to classify it.

### Allocation set

One allocation set belongs to exactly one cash JournalLine, identified by the composite source key (JournalEntryId, LineNumber). It contains:

- FinancialBookId and source cash line identity;
- a lifecycle state: Draft or Finalized;
- an immutable version number and optional SupersedesAllocationSetId;
- created/finalized timestamps and actor IDs;
- a row-version/concurrency token.

The source line must belong to the same financial book and its account must currently be classified as Cash or CashEquivalent. The source line amount and currency are read from the immutable journal line; they are not copied as editable values.

### Allocation rows

Each allocation row contains:

- positive Amount in the source line currency;
- Activity: Operating, Investing, or Financing;
- a stable presentation Category code (not a free-form label);
- required counterpart journal line identity within the same posted journal entry, on the opposite posting side;
- optional memo/reference;
- audit metadata.

Multiple rows may split one cash line across activities/categories. Counterpart lines must exist in the same journal entry, be on the opposite posting side, and must not belong to Cash/CashEquivalent accounts. Allocations referencing a counterpart line must not exceed that counterpart line's amount. This also prevents internal cash-to-cash-equivalent transfers from being classified as external cash flows. This supports mixed payments such as loan principal and interest.

### Lifecycle and invariants

- Draft sets may be edited; their total allocation must never exceed the source cash line amount.
- Finalization is allowed only when allocated amount equals the source cash line amount exactly. A partially allocated line remains an explicit exception and is not included in classified activity totals.
- Finalized sets and rows are immutable. Reclassification creates a new version linked to the superseded version; history is retained.
- Finalization and writes use a transaction plus a source-journal-entry scoped database lock and set-level lock so concurrent requests cannot over-allocate.
- A unique filtered constraint permits at most one current finalized version per source cash line.
- Amounts use the journal line's currency and the existing 4-decimal persistence precision. No cross-currency allocation or implicit FX conversion is permitted.
- Authorization is checked against the financial book on every read and write.

### Classification and elimination

- A journal entry with no Cash/CashEquivalent line is not a cash flow.
- When both sides of a movement are accounts classified as Cash or CashEquivalent, the movement is an internal transfer and is eliminated from activity totals. It remains visible in the account ledger.
- A movement between cash/cash equivalents and a non-cash-equivalent asset is not automatically eliminated; it requires an explicit Investing/Financing/Operating classification according to its substance.
- A cash line with no finalized allocation is returned as an unclassified exception. The system must never silently default it to Operating.
- Non-cash investing and financing transactions are disclosed separately and are not represented as cash-flow allocations.

### Presentation policy

Category codes are stable machine identifiers. Their labels and applicable activity are resolved through an effective-dated reporting policy, scoped to the financial book/reporting framework. Interest and dividend policy must be versioned so future IAS 7 amendments (including IFRS 18 consequential amendments effective from 2027) do not rewrite historical reports.

## Consequences

- Cash-flow allocation data is independent from the immutable accounting ledger and can be audited/reclassified without changing source accounting.
- The direct-method statement can aggregate finalized allocations by activity/category and reconcile to cash and cash equivalents.
- The report can expose draft, missing, over/under-allocation, and policy-mapping exceptions.
- Additional schema and API work is required for allocation lifecycle, audit history, and concurrency before the statement of cash flows is released.

## Implementation sequence

1. Persist allocation sets and rows with source-line foreign keys, constraints, audit fields, and versioning.
2. Add authenticated draft/create/update/finalize/read APIs with transactional concurrency safeguards.
3. Add validation and integration tests for splits, exact finalization, internal transfers, cross-book isolation, immutable history, and concurrent writes.
4. Build the IAS 7 direct-method report with opening/closing reconciliation and unclassified exceptions.
5. Add non-cash investing/financing disclosures and versioned presentation-policy administration.
