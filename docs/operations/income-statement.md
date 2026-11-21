# Income statement report

The income statement is a read-only report calculated from posted journal entries.
It does not use bank-account balances and does not persist calculated totals.

## Endpoint

`GET /api/financial-books/{financialBookId}/reports/income-statement?from=...&to=...`

Both `from` and `to` are required ISO 8601 timestamps. The period is half-open:
`from` is included and `to` is excluded. For a whole calendar day, use midnight
at the start of that day as `from` and midnight at the start of the following day
as `to`. The API rejects a period where `from` is greater than or equal to `to`.

## Accounting treatment

Only accounts of type `Income` and `Expense` are included. Asset, liability,
equity and transfer-related postings are not treated as revenue or expense.

- Income amount = credits minus debits (income's normal credit balance).
- Expense amount = debits minus credits (expense's normal debit balance).
- Net profit (or loss) = total income minus total expenses.

Reversals are included according to their own `PostedAt` timestamp. Therefore,
a reversal affects the report period in which the reversal was posted, not the
period of the original entry.

All income and expense accounts in the financial book are returned, including
accounts with no activity in the selected period (amount zero). This preserves
the chart-of-accounts context. Amounts use the financial book's base currency.

The report is available to the financial book owner and active members. No
database migration is required because values are aggregated from existing
journal lines at read time.
