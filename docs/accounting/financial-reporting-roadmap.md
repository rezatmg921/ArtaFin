# Accounting Standards and Financial Reporting Roadmap

## Purpose

ArtaFin is designed as a general-purpose double-entry accounting system. Its accounting behavior must be explicit, auditable, reproducible, and configurable where the applicable reporting framework permits policy choices. This roadmap is an implementation guide, not a claim of IFRS certification or a substitute for jurisdiction-specific professional review.

## Core invariants

- Posted journal entries are immutable. Corrections are made through linked reversal or adjustment entries.
- Every posted journal entry is balanced: total debits equal total credits in its transaction currency.
- Account balances and financial statements are derived from posted journal lines; derived balances are not independently stored.
- Reports are period- or date-specific, identify their currency and reporting basis, and can be traced to source entries.
- Draft, unposted, voided, or otherwise non-posted data must not affect financial statements.
- Accounting periods, posting dates, and audit timestamps are distinct concepts. Closed-period controls must prevent unauthorized backdating.
- Rounding rules and currency precision must be explicit. Foreign-currency accounting requires exchange-rate, functional-currency, and realized/unrealized FX policies; it must not be approximated by adding unlike currencies.

## Financial statements

### Statement of financial position (IAS 1)

Present assets, liabilities, and equity as of a reporting date. Current/non-current classification, materiality, comparative information, and required disclosures must be supported. The accounting equation must reconcile, while any difference must remain visible as an error rather than being silently plugged.

### Statement of profit or loss (IAS 1; IFRS 18 when applicable)

Present income and expenses for a reporting period using a configurable chart-of-accounts presentation mapping. Support comparative periods, consistent classification, and separate disclosure of material items. The report must not infer a full IFRS presentation solely from the five coarse account types.

### Statement of cash flows (IAS 7)

Present cash and cash-equivalent movements classified as operating, investing, and financing. Cash equivalents require documented eligibility (short maturity, high liquidity, convertibility to known cash amounts, and insignificant value risk), not merely an account name or code.

Support the direct method first when source records identify gross cash receipts and payments. Support the indirect method only when the required non-cash adjustments and working-capital movements can be derived reliably. Exclude non-cash investing/financing transactions from cash flows and disclose them separately. Reconcile opening and closing cash and cash equivalents to the statement of financial position.

Internal transfers between accounts included in cash and cash equivalents are not external cash flows. Transfers between cash and non-cash-equivalent investments are not automatically internal cash transfers and require classification.

A single journal entry may contain multiple cash-flow classifications. Classification therefore belongs at the cash movement / journal-line level or in an auditable allocation record, not solely on the journal-entry header. Missing or ambiguous classifications must be reported as exceptions; they must never be silently assigned to operating activities.

### Statement of changes in equity (IAS 1)

Reconcile opening and closing balances for each equity component, separately identifying profit or loss, other comprehensive income where supported, owner contributions, distributions, and transfers between equity components.

## Cash-flow classification design requirements

- Each account must explicitly indicate whether it represents cash or a cash equivalent. Cash-equivalent eligibility and effective maturity must be recorded or evidenced.
- Cash movements must carry an explicit activity classification and, where needed, a presentation category. Classifications must be auditable and may differ between lines of one journal entry.
- Automated defaults may be offered for known workflows (for example, ordinary customer receipts and supplier payments), but the user must be able to review them and ambiguous cases must require an explicit choice.
- A transfer between two cash-equivalent accounts is eliminated from operating/investing/financing totals while remaining visible in account ledgers.
- A journal entry with no cash-equivalent line is not a cash flow. Non-cash investing and financing events are separately disclosed.
- Loan principal and interest portions must be separately classifiable. One journal entry must not force both portions into one category.
- Interest and dividend presentation must follow the selected reporting framework and entity accounting policy. The policy must be versioned/effective-dated to support changes in standards.
- Report output must include opening cash, net movement by activity, net increase/decrease, closing cash, reconciliation difference, and classification exceptions.

## Other standards-based capabilities to plan

- IAS 8: accounting policies, changes in estimates, and correction of prior-period errors.
- IAS 10: events after the reporting period.
- IAS 12: current and deferred income taxes, where in scope.
- IAS 16 / IAS 38: property, plant and equipment and intangible assets, depreciation/amortization, disposals, and impairment links.
- IAS 21: foreign-currency transactions and translation.
- IAS 2: inventory costing and valuation.
- IFRS 9: financial instruments, impairment, and effective interest method, where in scope.
- IFRS 15: revenue recognition and contract liabilities, where in scope.
- IFRS 16: lease accounting, where in scope.
- IAS 7.44A: reconciliation/disclosure of changes in financing liabilities, including cash and non-cash changes.
- Audit trail, period close, approval controls, segregation of duties, and retained evidence.

## Implementation sequence

1. Establish explicit accounting/reporting policies and effective dates.
2. Add cash/cash-equivalent account attributes and validate their IAS 7 eligibility.
3. Persist auditable, versioned cash-flow allocation sets for individual cash journal lines; support split classifications, exact finalization, immutable history, and concurrency control. See [ADR-0007: Auditable Cash-Flow Allocation](ADR-0007-cash-flow-allocation.md).
4. [Implemented foundation] Implement the IAS 7 direct-method cash-flow statement with internal-transfer elimination, opening/closing reconciliation, and explicit unclassified exceptions. See [cash-flow statement operations](../operations/cash-flow-statement.md).
5. [Implemented foundation] Add immutable non-cash investing/financing disclosures, authenticated create/list APIs, posted-line validation, duplicate protection, and a separate Cash Flow Statement disclosure section. Financing-liability movement reconciliation remains.
6. Add indirect-method operating cash flows only after working-capital and non-cash adjustment data are sufficient.
7. Implement the statement of changes in equity and comparative financial statements.
8. Add accounting-period close, locked periods, controlled reopening, and audit evidence.
9. Expand into tax, inventory, fixed assets, leases, foreign currency, and other standards according to product scope.

## Standards and applicability

The reporting framework and jurisdiction must be explicit per financial book or reporting entity. IFRS requirements, local GAAP, tax rules, and management reporting are not interchangeable. Standard amendments with future effective dates must be activated according to the entity's adopted framework and effective date. In particular, IFRS 18-related amendments to IAS 7 require forward-compatible policy choices for interest and dividend cash-flow classification.

References:
- IFRS Foundation, IAS 7: https://www.ifrs.org/issued-standards/list-of-standards/ias-7-statement-of-cash-flows/
- IFRS Foundation, IFRS 18: https://www.ifrs.org/issued-standards/list-of-standards/ifrs-18-presentation-and-disclosure-in-financial-statements/
