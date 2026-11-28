# Trial balance report

The trial balance is a read-only report derived from posted journal entries. It does
not store or maintain account balances independently.

## Endpoint

`GET /api/financial-books/{financialBookId}/reports/trial-balance`

Optional query parameter:

- `asOf`: an ISO 8601 timestamp. Only journal entries posted at or before this
  timestamp are included. If omitted, the API uses the current UTC time.

The report includes every account in the financial book, including accounts with
no activity by the cutoff timestamp. This keeps the report aligned with the
financial book's chart of accounts and makes unused accounts visible.

## Amounts

For each account, the response includes:

- `totalDebits` and `totalCredits`: cumulative debit and credit postings through
  the cutoff timestamp.
- `debitBalance` and `creditBalance`: the account's net balance presented in
  the corresponding trial-balance column. A net debit is shown in debit balance;
  a net credit is shown in credit balance.

The report-level `totalDebitBalance` and `totalCreditBalance` should be equal.
A difference indicates an accounting data-integrity problem that should be
investigated; the report does not silently force the totals to balance.

All amounts use the financial book's base currency. Posting use cases enforce
that journal entries use that currency.

## Example

```http
GET /api/financial-books/{financialBookId}/reports/trial-balance?asOf=2026-11-17T23:59:59Z
Authorization: Bearer <access-token>
```

No database migration is required: the report aggregates existing journal lines
at read time.
