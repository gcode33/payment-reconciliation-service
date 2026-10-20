# Payment Reconciliation Service

A simulated payment-processing and reconciliation service: payments go to a mock card
provider, every cent is recorded in a double-entry ledger, and the provider's daily
settlement file is checked against that ledger so every gap is explained.

**Everything is simulated.** No real payments, card numbers or banks are involved.

Status: spec in review. See [`docs/SPEC.md`](docs/SPEC.md).
