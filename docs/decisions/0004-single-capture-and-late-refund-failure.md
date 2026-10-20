# 0004. One capture per payment; late refund failure moves the status back

Date: 2026-10-19 · Status: Accepted

## Context
Two rules were left open by SPEC §7.2:
1. Can a payment be captured several times (multicapture), or once?
2. Stripe's `pm_card_refundFail` test card makes a refund succeed and *then* fail. The
   first-draft diagram had no edge out of `Refunded`.

## Decision
1. **One capture per payment.** It can be for less than the authorized amount, and the rest is
   released. This matches Stripe's default, so both providers behave the same. A capture that
   *failed* can be retried with a new capture; one that is pending or unknown blocks new
   captures (and blocks voiding).
2. **The payment's refund status is derived from its refunds.** When a refund fails after
   succeeding, the payment moves back. Three edges are added: `PartiallyRefunded → Captured`,
   `Refunded → PartiallyRefunded` and `Refunded → Captured`. Refunds that are pending or unknown
   reserve their amount, so the total of refunds that might still succeed never exceeds the
   capture (I4).

## Consequences
- No multicapture (e.g. split shipments). It can be added later, as a new decision.
- The ledger (step 1.3) must post a reversing journal when a refund fails late. It never edits
  the original.
