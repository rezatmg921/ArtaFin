# Dated lease payment schedule

The lease schedule calculator accepts the actual contractual payment dates and
amounts. Each row also carries the effective rate applicable to the interval
ending on that payment date.

For each row it applies the effective-interest method:

- Interest expense = opening liability × that interval's effective rate,
  rounded once to the currency minor unit using the domain money rules.
- Principal reduction = contractual cash payment − interest expense.
- Closing liability = opening liability − principal reduction.

The closing liability of one row becomes the opening liability of the next.
A payment lower than accrued interest is retained as negative amortisation;
a zero payment is also representable.

## Deliberate boundaries

The calculator does not generate dates or infer contractual cash flows. The
caller must supply the executed agreement's actual payment dates and amounts.
It also does not convert an annual nominal/effective rate to a periodic rate,
assume a day-count convention, or derive stub-period rates. The rate on each
row is the effective rate for that exact interval, as established from the
contract terms.

The schedule is a calculation result only at this stage. Persistence,
contract-linked schedule versioning, and idempotent journal posting are
separate application slices. Existing posted journal entries are not changed.
