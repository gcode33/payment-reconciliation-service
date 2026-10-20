# 0003. Money as integer minor units with checked arithmetic

Date: 2026-10-19 · Status: Accepted

## Context
Floating-point numbers can't represent most cent values exactly, and rounding at each step
creates or loses cents. C# adds two traps of its own: a struct's `default` value, and `long`
arithmetic that silently wraps around on overflow.

## Decision
`Money` is a `readonly record struct` of `long MinorUnits` plus a `Currency` (CAD or USD):
- no `double` or `decimal`;
- `checked` arithmetic, so overflow throws;
- mixing currencies throws `CurrencyMismatchException`;
- the `Currency` enum starts at 1, so `default(Money)` is not a valid amount.

Conversion between currencies happens only in the FX rule (SPEC §7.7), which rounds once.

## Consequences
- Every sum in the ledger is exact, and a currency mix-up fails loudly.
- Amounts must be converted at the edges (JSON, the database) with explicit converters, so the
  type can't be built without validation.
