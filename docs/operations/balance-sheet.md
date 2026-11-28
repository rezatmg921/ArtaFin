# Balance sheet report

The balance sheet is a read-only report calculated from posted journal entries.
It does not persist calculated account balances and requires no database migration.

## Endpoint

`GET /api/financial-books/{financialBookId}/reports/balance-sheet?asOf=...`

The optional `asOf` parameter is an ISO 8601 timestamp. When omitted, the
current UTC time is used. Entries posted on or before the timestamp are included.

## Accounting treatment

- Assets use debit balance: debits minus credits.
- Liabilities and equity use credit balance: credits minus debits.
- Unclosed net income is calculated from all income and expense account
  balances through the report timestamp and is included in equity.
- If closing entries have transferred the income and expense balances to
  retained earnings/equity, those accounts are already reflected in Equity
  and unclosed net income is zero.
- The difference is total assets minus total liabilities and equity. A balanced
  set of posted journal entries should produce a zero difference.

All accounts in the financial book are returned, including accounts with no
activity. Amounts use the financial book's base currency. The report is
available to the owner and active members.
