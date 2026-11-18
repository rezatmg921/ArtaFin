# Lease liability effective-interest calculation

## Scope

The first implementation slice introduces a deterministic domain calculation for one
lease-liability period. It does not create or post journal entries and does not infer
contractual terms from existing ledger movements.

## Calculation

Inputs:

- opening lease-liability carrying amount;
- effective interest rate for the specific period (decimal fraction, not percentage);
- contractual cash payment for the period.

The calculation is:

```text
interest expense       = round(opening liability × effective periodic rate)
principal reduction    = cash payment − interest expense
closing liability      = opening liability − principal reduction
```

Interest is rounded once to the currency minor unit using midpoint-away-from-zero.
The domain `Money` type enforces the configured currency precision.

A negative principal reduction is preserved as negative amortisation when the
contractual payment is lower than the period's interest. Callers must represent that
case correctly in the journal (debit interest expense, credit lease liability for
the unpaid interest, and credit cash for the payment); it must not be forced into a
negative debit line.

## Boundaries and safeguards

- Opening liability and payment must use the same currency.
- Opening liability, rate, and payment cannot be negative.
- Payment cannot exceed opening liability plus accrued interest.
- The calculation does not guess an annual-to-period conversion, day-count basis,
  payment timing, or compounding convention. The contract/schedule layer must derive
  the effective periodic rate from the executed lease terms and document its
  convention.
- The existing posted acquisition and repayment entries remain immutable. In
  particular, the historical 10,000,000 IRR repayment cannot be retrospectively
  split without the executed contract terms and an approved correction/reversal.

## Next integration steps

1. Persist a lease contract with commencement date, initial liability, payment
   frequency/timing, currency, discount-rate convention, and effective rate.
2. Generate dated schedule periods using an explicitly selected day-count and
   payment-timing convention.
3. Persist each period's opening balance, interest, payment, principal movement,
   closing balance, and status.
4. Post payments idempotently with journal lines linked to the contract and period.
5. Reconcile the posted liability movements with the existing IAS 7.44A report.

The period calculation is intentionally independent of persistence and posting so it
can be unit-tested before those workflows are introduced.
