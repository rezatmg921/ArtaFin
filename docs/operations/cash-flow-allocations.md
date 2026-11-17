# Cash-flow allocation workflow

Cash-flow classifications are annotations over immutable posted journal lines. They do not create, edit, or reverse accounting entries. The source of accounting truth remains `JournalEntries` and `JournalLines`.

Only journal lines belonging to accounts classified as `Cash` or `CashEquivalent` can be used as allocation sources. A cash-equivalent account must already satisfy the account-level eligibility assessment described in [cash-flow account classification](cash-flow-account-classification.md).

## Lifecycle

1. Create a Draft for a cash or cash-equivalent journal line.
2. Add one or more allocations. Each allocation has a positive amount, an IAS 7 activity (`Operating`, `Investing`, or `Financing`), a category, a required counterpart journal line on the opposite posting side, and optional memo details.
3. The Draft can be edited by adding or removing allocations.
4. Finalize only when the allocated total exactly equals the source line amount.
5. A Finalized set is immutable. To correct it, create a new revision. The previous version remains Finalized while the revision is a Draft; when the revision is finalized, the previous version is atomically marked Superseded.

A source line can have at most one active Draft and at most one current Finalized version. Version numbers are monotonic for each source journal line. The API serializes changes using SQL Server transaction locks; database unique indexes provide an additional integrity boundary.

## Endpoints

All endpoints require authentication and an owner or active Accountant membership in the financial book.

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/financial-books/{bookId}/journal-entries/{entryId}/lines/{lineNumber}/cash-flow-allocations` | Create the first Draft |
| GET | `/api/financial-books/{bookId}/cash-flow-allocations/{setId}` | Read a set and its allocations |
| POST | `/api/financial-books/{bookId}/cash-flow-allocations/{setId}/items` | Add an allocation |
| DELETE | `/api/financial-books/{bookId}/cash-flow-allocations/{setId}/items/{allocationId}` | Remove a Draft allocation |
| POST | `/api/financial-books/{bookId}/cash-flow-allocations/{setId}/finalize` | Finalize an exactly allocated set |
| POST | `/api/financial-books/{bookId}/cash-flow-allocations/{setId}/revision` | Create a Draft revision of a Finalized set |

## Example

For a cash receipt of USD 100, create a Draft against the cash debit line, then add allocations such as:

```json
{
  "amount": 60,
  "activity": "Operating",
  "category": "Customer receipts",
  "counterpartLineNumber": 2,
  "memo": "Service receipt"
}
```

and:

```json
{
  "amount": 40,
  "activity": "Financing",
  "category": "Owner contribution",
  "counterpartLineNumber": 3,
  "memo": null
}
```

The Draft can be finalized only when the sum is exactly USD 100. Under-allocation and over-allocation are rejected.

## Accounting safeguards and scope

- Journal entries and journal lines remain immutable.
- The source amount and currency are copied from the posted journal line; clients cannot supply or change them.
- The API rejects non-cash and non-cash-equivalent source accounts.
- A finalized allocation is never edited in place; corrections preserve the previous version.
- Transfers between cash and cash equivalents are not operating, investing, or financing cash flows under IAS 7. The future statement projection must eliminate these internal movements rather than report them as external cash flows.
- Non-cash investing and financing transactions are not represented as cash allocations; they require separate disclosure in the statement.
- The current API establishes line-level classification and audit history. It does not yet produce the Cash Flow Statement. Statement calculation, opening/closing cash reconciliation, unclassified-line reporting, and accounting-policy versioning are subsequent work.
