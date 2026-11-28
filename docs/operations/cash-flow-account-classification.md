# Cash flow account classification

This first cash-flow implementation step identifies which asset accounts form part of cash and cash equivalents. It does not yet calculate the cash flow statement; journal-line counterpart allocations are a separate next step.

## Configure an account

Use the existing account update endpoint:

`PUT /api/financial-books/{financialBookId}/accounts/{accountId}`

The endpoint requires the current account ETag in `If-Match`. Existing fields remain supported. The new optional fields are:

- `cashFlowAccountType`: `None`, `Cash`, or `CashEquivalent`.
- `cashEquivalentMaturityDays`: required for `CashEquivalent`; acquisition maturity must be 0–90 days.
- `cashEquivalentRiskInsignificant`: must be `true` for `CashEquivalent`, representing an explicit assessment that the risk of changes in value is insignificant.

Example for a cash account:

```json
{
  "name": "Main bank",
  "cashFlowAccountType": "Cash"
}
```

Example for a qualifying cash equivalent:

```json
{
  "name": "Short-term deposit",
  "cashFlowAccountType": "CashEquivalent",
  "cashEquivalentMaturityDays": 60,
  "cashEquivalentRiskInsignificant": true
}
```

To remove the classification, send `cashFlowAccountType: "None"` and omit the assessment fields.

## Accounting safeguards

- Only Asset accounts can be classified as Cash or CashEquivalent.
- A Cash account cannot carry cash-equivalent assessment metadata.
- A CashEquivalent requires an acquisition maturity no longer than three months (represented here as at most 90 days) and an explicit insignificant-risk confirmation.
- Database check constraints enforce valid combinations and the Asset-only rule.
- Existing accounts default to `None`; no historical account is silently treated as cash.
- Updates use the account's existing ETag/row-version concurrency protection.

The 90-day value is a practical validation boundary, not a substitute for the IAS 7 qualitative tests. The account must also be held to meet short-term cash commitments, be readily convertible to a known amount of cash, and be subject to an insignificant risk of changes in value.
