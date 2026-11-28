# ADR-0006: Currency and monetary precision policy

- **Status:** Accepted
- **Date:** 2026-11-07

## Context

A single global decimal scale is not a valid monetary policy. ISO 4217 currencies
have different minor-unit exponents. SQL Server persistence currently uses
`decimal(19,4)`, which permits at most 15 integral digits and 4 fractional digits.

## Decisions

1. `Currency` is an immutable Domain value type. Its explicit, version-controlled
   catalog maps each supported ISO 4217 code to its minor-unit exponent.
2. Unsupported currency codes are rejected. Adding a code requires checking its
   current ISO 4217 status and minor-unit exponent, then adding tests.
3. `Money` stores an exact decimal amount and a catalogued currency. It rejects
   values with non-zero fractional digits beyond that currency's minor unit.
   Insignificant trailing zeroes are accepted.
4. The technical persistence ceiling remains `decimal(19,4)`: no more than
   15 integral digits and 4 fractional digits. The currency catalog currently
   contains no currency with a minor-unit exponent greater than 4.
5. Construction, addition, and subtraction never round implicitly. Arithmetic
   returns an exact `Money` or fails if the result violates the currency or
   persistence constraints.
6. When rounding is required, callers must use `Money.CreateRounded` and pass
   an explicit .NET `MidpointRounding` rule. No application-wide midpoint rule
   is assumed; tax, allocation, cash settlement, and FX calculations may require
   different policies and must select them at their use-case boundary.
7. FX rates and intermediate calculations are not represented as `Money`.
   They require a separate precision and rounding design before FX is enabled.
8. FinancialBook base currency is validated against the same catalog. Journal
   entries remain single-currency; enforcement that a posted entry uses its
   book's base currency belongs in the posting application use case, which is
   not yet implemented.

## Consequences

- The supported currency catalog is deliberately explicit and must be maintained
  as ISO 4217 data changes; it is not claimed to be an automatically refreshed
  complete registry.
- The `decimal(19,4)` SQL Server mapping remains compatible with every currency
  currently supported.
- This decision does not define FX, tax, cash rounding, or currency conversion.
  Those require separate decisions and tests before those features are introduced.
