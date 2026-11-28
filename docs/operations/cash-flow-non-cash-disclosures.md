# Non-cash investing and financing disclosures

## Purpose

IAS 7 excludes non-cash investing and financing transactions from the statement of cash flows, but requires them to be disclosed separately. Examples include acquiring an asset by assuming a lease liability, acquiring property by issuing equity, and converting debt to equity.

These records annotate immutable posted journal lines. They do not create cash receipts or payments and never contribute to operating, investing, or financing cash-flow totals.

## Data model and controls

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

The API requires an authenticated owner or active member who has posting permission. It verifies that the source line belongs to the requested book, exists in a posted journal entry, is not classified as Cash or CashEquivalent, and has exactly the same amount and currency as the disclosure. Activity is restricted to Investing or Financing. Duplicate disclosures return HTTP 409. The database also enforces source-line uniqueness and the journal-line foreign key.

## API

Create a disclosure:

```http
POST /api/financial-books/{financialBookId}/journal-entries/{journalEntryId}/lines/{lineNumber}/non-cash-disclosures
Authorization: Bearer {token}
Content-Type: application/json
```

Example request:

```json
{
  "amount": 125000,
  "currency": "USD",
  "activity": "Financing",
  "category": "ASSET_ACQUIRED_THROUGH_LEASE",
  "memo": "Equipment recognized at lease commencement; no cash paid"
}
```

List disclosures for a journal entry:

```http
GET /api/financial-books/{financialBookId}/journal-entries/{journalEntryId}/non-cash-disclosures
Authorization: Bearer {token}
```

There is deliberately no update or delete endpoint. Corrections require an accounting-controlled correction process rather than overwriting the audit record.

## Cash-flow report behavior

The existing report endpoint returns a separate `nonCashDisclosures` collection. It is filtered by the linked journal entry's `PostedAt` using the report's half-open period (`From` inclusive, `To` exclusive). It includes disclosures even when the journal entry contains no cash or cash-equivalent line. These amounts are never added to cash receipts, payments, net cash flow, or the cash reconciliation.

## Example

A company recognizes equipment of USD 125,000 through a lease, without paying cash at commencement. The disclosure is linked to the lease-liability journal line and classified as Financing with category `ASSET_ACQUIRED_THROUGH_LEASE`. The equipment acquisition is disclosed separately; neither side is added to cash payments or receipts.

## Scope and limitations

This implements a foundational separate-disclosure register and report projection. It does not yet provide a configurable taxonomy, multiple reporting-framework policies, FX translation, or the IAS 7.44A financing-liability reconciliation. The report does not claim full IFRS compliance by itself; reporting framework and jurisdiction must be selected for the financial book and reviewed by the entity's accounting professional.
