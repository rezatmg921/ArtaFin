# Non-cash investing and financing disclosures

## Purpose

IAS 7 excludes non-cash investing and financing transactions from the statement of cash flows, but requires them to be disclosed separately. Examples include acquiring an asset by assuming a lease liability, acquiring property by issuing equity, and converting debt to equity.

These records are annotations over immutable posted journal lines. They do not create cash receipts or payments and must never contribute to operating, investing, or financing cash-flow totals.

## Current persistence foundation

```text
CashFlowNonCashDisclosure
  FinancialBookId
  JournalEntryId
  SourceLineNumber
  Amount
  Currency
  Activity (Investing | Financing)
  Category
  Memo
  CreatedBy
  CreatedAt
```

Each disclosure identifies one posted journal line and is immutable after creation. A source line can have at most one disclosure record. Multiple components of one transaction can be disclosed by linking separate non-cash journal lines.

The database validates positive amount, positive source line number, and an Investing/Financing activity. A composite foreign key ensures the referenced journal line exists.

## Example

A company recognizes equipment of USD 125,000 through a lease, without paying cash at commencement. The non-cash disclosure is linked to the lease-liability journal line and classified as Financing with category `ASSET_ACQUIRED_THROUGH_LEASE`. The equipment acquisition is disclosed separately; neither side is added to cash payments or receipts.

## Scope

This commit introduces the domain and persistence foundation only. The authenticated API, validation that the source line is genuinely non-cash and matches the disclosure amount/currency, report projection, and integration tests are subsequent implementation steps. Until those are implemented, these records are not yet available for operational reporting.
