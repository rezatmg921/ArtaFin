# Lease payment schedule API

A payment schedule is a dated, versioned set of contractual cash payments for
one lease contract. Each row carries the effective rate for the exact interval
ending on its payment date. The API does not derive periodic rates from an
annual rate or infer payment dates and amounts.

## Create a draft version

`POST /api/financial-books/{financialBookId}/lease-contracts/{leaseContractId}/payment-schedules`

Example request:

```json
{
  "payments": [
    {
      "paymentDate": "2027-02-01",
      "paymentAmount": 10000000,
      "effectivePeriodicRate": 0.01
    },
    {
      "paymentDate": "2027-03-01",
      "paymentAmount": 10000000,
      "effectivePeriodicRate": 0.012
    }
  ]
}
```

The payment amounts use the lease contract currency. Rates are decimal
fractions, not percentages: `0.01` means 1% for that interval. The API
validates strictly increasing dates, contract date boundaries, supported
precision, and the liability roll-forward before persisting the draft.

Only one draft can exist for a contract at a time. A new version references
the latest finalized version that it is intended to supersede.

## Read schedule versions

`GET /api/financial-books/{financialBookId}/lease-contracts/{leaseContractId}/payment-schedules`

The response contains all versions, newest first. Each payment row includes
the calculated opening liability, interest expense, principal reduction, and
closing liability in the contract currency.

## Finalize a draft

`POST /api/financial-books/{financialBookId}/lease-contracts/{leaseContractId}/payment-schedules/{scheduleId}/finalize`

Finalization is transactional. The previous finalized version, if any, is
marked Superseded in the same transaction. A finalized schedule and its
payment rows cannot be edited or deleted; corrections require a new version.

Finalizing a schedule does not post a journal entry. Accounting posting,
idempotency, and cash-flow allocation are separate steps and are not implied
by schedule finalization.

## Accounting boundaries

The effective rate on each payment row must already reflect the executed
contract's applicable rate convention and interval length. The API does not
assume a day-count convention, annual-to-periodic conversion, compounding
convention, or irregular-period method. The historical journal entries remain
immutable and are not retroactively split using the newly entered schedule.
