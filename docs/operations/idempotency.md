# Idempotent financial write requests

ArtaFin supports the optional `Idempotency-Key` HTTP header on these financial posting endpoints:

- `POST /api/financial-books/{financialBookId}/expenses`
- `POST /api/financial-books/{financialBookId}/income`
- `POST /api/financial-books/{financialBookId}/transfers`

## Client behavior

Generate a unique key for each logical financial operation (a UUID is a suitable choice) and reuse that exact key only when retrying the same operation after a timeout, lost response, or uncertain network outcome.

```http
POST /api/financial-books/{financialBookId}/expenses
Authorization: Bearer {access-token}
Content-Type: application/json
Idempotency-Key: 7c0e6db1-7e7e-4a45-ae80-fb6fdc3b813e
```

The header is optional for backward compatibility. When supplied, it must contain one non-empty value of at most 128 characters and no control characters. Keys are trimmed and normalized to uppercase before lookup, so they are case-insensitive.

## Server guarantees

- The key is scoped to the authenticated user and financial book.
- The key, normalized request fingerprint, and resulting journal entry are committed in the same SQL transaction as the posting.
- Concurrent requests using the same key are serialized with a transaction-owned SQL Server application lock. A unique filtered index is the final database safeguard.
- Repeating the same key with the same financial request returns the original journal entry ID and does not post another entry.
- Reusing a key with a different operation or payload returns `409 Conflict`.
- Failed requests do not reserve a key; the client may retry after correcting the request.
- Idempotency metadata is retained with the immutable journal entry. Therefore, a key remains reserved for that user and book for as long as the journal entry is retained. Clients should generate a new key for every new logical operation.

Authentication and financial-book authorization are checked on every request, including retries. Idempotency does not bypass access control.

## Scope

This first implementation covers expense, income, and transfer creation. Manual journal-entry creation and reversal endpoints do not yet accept idempotency keys. Requests that omit the header retain their existing behavior and are not protected against client retries.
