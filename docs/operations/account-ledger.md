# Account ledger report

The account ledger is a read-only view of posted journal lines for one account.
It is derived from the immutable journal and does not store or mutate balances.
No database migration is required.

## Endpoint

`GET /api/financial-books/{financialBookId}/accounts/{accountId}/ledger`

Query parameters:

- `from` (optional): inclusive lower timestamp, ISO 8601.
- `to` (optional): exclusive upper timestamp, ISO 8601.
- `page` (optional, default 1): one-based page number.
- `pageSize` (optional, default 50): between 1 and 100.

The selected period is `[from, to)`. If either boundary is omitted, that
side of the period is unbounded. When both are supplied, `from` must be
earlier than `to`.

## Balance semantics

Balances follow the account's normal-balance convention:

- Debit-normal accounts (assets and expenses): debit increases the balance;
  credit decreases it.
- Credit-normal accounts (liabilities, equity and income): credit increases
  the balance; debit decreases it.

`openingBalance` is the normal balance immediately before the first returned
line (or, for an empty first page with a date range, immediately before
`from`). Each item contains `balanceAfter`, the cumulative normal balance
after that journal line. For page 2 and later, the opening balance carries
forward all prior activity, including lines from earlier pages.

Rows are ordered deterministically by posting timestamp, journal-entry ID,
and line number. Reversal entries appear on their own posting timestamp and
reference the original entry through `reversesJournalEntryId`.

The response also includes account identity, account type, normal-balance
side, book base currency, page metadata, and total matching line count.
Only the owner and active financial-book members can access the report.
