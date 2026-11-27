# Statement of changes in equity

The report is calculated from posted journal entries. It does not store derived balances.

## Endpoint

`GET /api/financial-books/{financialBookId}/reports/changes-in-equity?from=...&to=...`

The period includes `from` and excludes `to`. The endpoint requires `Reports.Read` permission and is available to the financial book owner and active members.

## Report contents

- Opening and closing balance for each equity account.
- Credit movements as increases and debit movements as decreases.
- Opening and closing unclosed net income, calculated from income and expense account balances.
- Opening and closing total equity, including unclosed net income.

Only posted journal entries are included. Accounts with no activity are also returned with zero movements.

## Important limitation

This first version reports movements by equity account. It does not automatically decide whether a credit is an owner contribution, a profit transfer, or another increase, nor whether a debit is an owner distribution or another decrease. Keep these transactions in separate, clearly named equity accounts if they need to be distinguished in the report.

Other comprehensive income (OCI), comparative prior-year columns, and explicit movement categories require additional accounting data and are not inferred.
