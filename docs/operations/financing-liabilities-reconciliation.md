# Financing liabilities reconciliation (IAS 7.44A)

## Purpose and scope

The report reconciles opening and closing balances of liabilities explicitly designated as arising from financing activities. It does not treat every liability account as a financing liability: trade payables, tax liabilities, and ordinary operating accruals remain outside the report unless the entity deliberately classifies them otherwise.

The report is derived from immutable posted journal lines. It does not store account balances.

## Account classification

Update a liability account using the existing account update endpoint and its ETag:

```http
PUT /api/financial-books/{financialBookId}/accounts/{accountId}
If-Match: "{etag}"
Content-Type: application/json
```

Add one of the following values to the request:

- `Borrowings`
- `LeaseLiabilities`
- `OtherFinancingLiabilities`
- `None` to remove it from the scope

Only Liability accounts can carry a non-None classification. The database enforces this rule. The classification is explicit; account code or name is never used to infer financing scope.

## Report

```http
GET /api/financial-books/{financialBookId}/reports/financing-liabilities-reconciliation?from={from}&to={to}
```

The period is half-open: From is inclusive and To is exclusive. Balances and movements are presented in the financial book base currency. Foreign-currency financing-liability movements are reported as exceptions because FX translation is not yet implemented.

For each classified account the report returns:

- Opening balance: posted liability balance immediately before From.
- Cash changes: movements linked through finalized cash-flow allocations whose counterpart line is the financing-liability line.
- Non-cash changes: movements linked to immutable non-cash disclosures on the financing-liability line.
- Unclassified changes: residual posted movement not explained by either source.
- Closing balance: posted liability balance immediately before To.
- Reconciliation difference: closing less opening and the three movement buckets.

An unclassified movement is not silently labelled as FX, fair value, acquisition, or other. It produces an exception and makes `isComplete` false. Explicit support for those additional IAS 7.44A movement types will be added as separately auditable classifications.

## Finance lease example

At lease commencement:

```text
Dr Equipment                         100,000,000
    Cr Bank                            20,000,000
    Cr Finance lease liability         80,000,000
```

The lease-liability account must be classified as `LeaseLiabilities`. The disclosure attached to the liability journal line explains the non-cash increase of 80,000,000. The bank payment is not a movement in the lease liability because its allocation counterpart is the equipment line.

Expected reconciliation when there are no opening balances or other lease movements:

| Movement | Amount |
|---|---:|
| Opening lease liability | 0 |
| Cash changes | 0 |
| Non-cash changes | 80,000,000 |
| Unclassified changes | 0 |
| Closing lease liability | 80,000,000 |

A later installment that debits the lease liability and credits bank is a cash reduction only when its bank line has a finalized allocation pointing to the lease-liability line.

## Limitations

This is a foundational IAS 7.44A reconciliation. It currently identifies cash changes through finalized cash-flow allocations and non-cash changes through the non-cash disclosure register. FX, fair-value, business-combination, and other non-cash movements require explicit classifications and are surfaced as unclassified exceptions. It is not a claim of full IFRS compliance.
