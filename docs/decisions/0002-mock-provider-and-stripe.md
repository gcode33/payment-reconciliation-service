# 0002. A mock provider *and* Stripe in test mode

Date: 2026-10-19 · Status: Accepted

## Context
Real payment data is impossible in a portfolio project (PCI DSS, privacy, a merchant account).
The project's main claim is that it survives failures: a timeout after a successful charge must
never double-charge. A real provider cannot be made to fail on demand.

## Decision
Both, behind one `IPaymentProvider` interface:
- **MockProvider**: a separate service with seeded "chaos dials" that reproduce failures
  exactly. It is used by the chaos suite on every PR.
- **Stripe in test mode**: the real API, webhooks and balance transactions, with test cards and
  no real money. Its tests run nightly, not as a required PR check. The app refuses live keys.

## Consequences
- The project proves both a real integration and the failure handling.
- Two implementations to maintain; the interface has to fit both.
- Stripe test mode settles instantly, unlike live mode, so reconciliation must not depend on
  settlement timing (SPEC §7.8).
