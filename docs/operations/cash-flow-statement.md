# IAS 7 cash-flow statement

## Endpoint

`GET /api/financial-books/{financialBookId}/reports/cash-flow-statement?from={from}&to={to}`

The period is half-open: `From` is inclusive and `To` is exclusive. Both values must be supplied and `From` must be earlier than `To`. The caller must own the financial book or be an active member.

## Direct-method presentation

The report returns:

- Opening cash and cash equivalents at `From`.
- Cash receipts, payments, and net cash flow for Operating, Investing, and Financing activities.
- Receipt/payment totals by finalized allocation category.
- Net increase/decrease in cash and cash equivalents.
- Closing cash and cash equivalents immediately before `To`.
- Reconciliation difference and an `IsReconciled` indicator.
- Explicit exceptions for cash movements that cannot be safely included.
- A separate `InternalCashTransfers` collection listing internal movements between cash/cash-equivalent accounts, including source and destination accounts, amount, currency, posting date, and journal entry.
- A separate `NonCashDisclosures` collection for non-cash investing and financing transactions, with source journal entry/line, posting date, amount, currency, activity, category, and memo.

Cash inflows are derived from debit-side cash/cash-equivalent lines; outflows are derived from credit-side lines. Their finalized allocations provide the activity and category. A split source line contributes to each category according to its allocation amount.

## Classification and safeguards

- Only posted journal entries are read. Draft allocation sets are never used in report totals.
- Only the current Finalized allocation version is used. Superseded versions are excluded.
- A cash/cash-equivalent line must have a finalized allocation that fully equals its source amount, unless the journal entry consists exclusively of cash/cash-equivalent lines.
- Journal entries consisting exclusively of cash/cash-equivalent lines are treated as internal movements and excluded from activity totals.
- Cash lines in mixed cash/non-cash entries require explicit finalized allocation. Missing or Draft-only allocations are returned as exceptions and are not silently classified as Operating.
- Finalized allocations are checked again for a valid opposite-side, non-cash counterpart.
- Amounts in a currency other than the financial book base currency are not converted or added to base-currency totals. They are returned as `UNSUPPORTED_CURRENCY` exceptions; FX translation is not implemented.
- Non-cash disclosures are loaded independently of cash-classified journal entries and filtered by the linked entry's `PostedAt` in the same half-open period. They never affect cash receipts, payments, net cash flow, or reconciliation.
- Opening and closing balances are derived from posted journal lines and are not stored.
- The report is considered reconciled only when there are no exceptions and the closing-minus-opening cash movement equals the net cash flows.

## Scope and limitations

This is the first IAS 7 direct-method projection. It uses the current account-level cash/cash-equivalent classification and finalized line allocations. Historical effective dating of account classification, configurable interest/dividend policies, financing-liability movement disclosures (IAS 7.44A), and indirect-method adjustments are subsequent capabilities. Category codes are reported as supplied; the API does not yet enforce a versioned category catalogue or entity-specific accounting policy.

The report does not claim full IFRS compliance by itself. The reporting framework and jurisdiction must be selected for the financial book and reviewed by the entity's accounting professional.
