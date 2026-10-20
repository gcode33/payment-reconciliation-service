# Payment Reconciliation Service

A simulated payment-processing and reconciliation service: payments go to a mock card
provider, every cent is recorded in a double-entry ledger, and the provider's daily
settlement file is checked against that ledger so every gap is explained.

**Everything is simulated.** No real payments, card numbers or banks are involved.

Status: spec in review. See [`docs/SPEC.md`](docs/SPEC.md).

## Run it locally

Needs Docker and the .NET 10 SDK.

```bash
docker compose up -d --wait            # PostgreSQL 17 on localhost:5432
dotnet run --project src/Payments.Api  # API on http://localhost:5180
curl http://localhost:5180/health      # "Healthy" (200), or "Unhealthy" (503) if the database is down
dotnet test                            # unit and integration tests (Testcontainers starts its own Postgres)
```

## Project layout

| Path | What it is |
|---|---|
| `src/Payments.Domain` | Pure business rules: money, payment states, ledger postings, matching. No I/O. |
| `src/Payments.Infrastructure` | Database access, provider clients, settlement readers |
| `src/Payments.Api` | HTTP API for merchants and the ops console |
| `src/Payments.Worker` | Background jobs: unknown-outcome resolver, settlement import, reconciliation |
| `src/MockProvider` | Simulated card company with chaos dials (port 5190) |
| `tests/` | Domain unit tests and integration tests |
| `docs/` | Spec and build plan |
