# Spec: Payment Reconciliation Service

Status: **v1 draft, 2026-10-19. Awaiting George's review.** Nothing is built until this is
approved. Open decisions are listed in §14.

## 1. In plain words

A coffee shop's card machine asks a card company "can this customer pay $5?". The shop writes
every sale in a notebook it never erases (a **ledger**). Days later the card company pays the
shop, minus a fee, and sends a statement. The owner checks the notebook against the statement
line by line (**reconciliation**).

This project is the software in the middle. It talks to a **pretend** card company, keeps the
notebook honest, and checks the statement every day. The hard and interesting part is what
happens when things go wrong: dropped connections, "maybe" answers, missing or wrong lines on
the statement. The system must never charge twice, never lose a cent, and explain every gap.
An AI assistant (Claude) helps a person investigate gaps but can never change the books.

## 2. Goal

A portfolio project that shows, end to end, the production habits Toronto bank and fintech
teams hire for: correctness with money, safe retries, failure handling, auditability,
observability, automated testing and cloud deployment, plus a carefully limited AI feature.

**Success, measured:**
- `docker compose up` starts the full system and a seeded demo in under 5 minutes on a clean
  machine, following only the README.
- The chaos test suite (§9.3) passes in CI on every PR: under injected timeouts, duplicates and
  provider errors there are **zero** double charges and **zero** unbalanced journals.
- Every reconciliation run ends with the provider clearing account at zero, or every non-zero
  cent attached to an open exception (invariant I5).
- The AI triage assistant reaches **≥ 80 %** correct cause classification on the labelled
  evaluation set (§10.3), and the number is published in the README.
- A recruiter can understand the project from the README in 2 minutes (diagram, demo GIF,
  "what this shows" list).

## 3. Non-goals (explicit)

- No real payments, card numbers, banks or payment providers. Cards are fake tokens such as
  `tok_visa_ok`; nothing is in scope for PCI DSS.
- Not a certified or production payment product, and the README says so.
- One currency (CAD), one country, one demo merchant in v1.
- No customer-facing checkout UI. The "customer" is a load generator script.
- No real authentication provider. Merchants use API keys (§7.4); the ops console is
  single-user.
- No Kubernetes. Docker Compose locally, AWS ECS in the cloud.
- The AI assistant never writes to the ledger, never resolves exceptions on its own and never
  sees secrets.

## 4. Users and constraints

- **Merchant** (simulated): calls the API to authorise, capture, void and refund payments.
- **Operations analyst** (George, during the demo): reviews reconciliation exceptions in the
  ops console, resolves them with a note, and asks the AI assistant for help.
- **Recruiter or interviewer**: reads the README, watches the demo, maybe runs it.
- Runs on macOS and Linux with Docker. Needs the .NET 10 SDK (`brew install --cask dotnet-sdk`;
  not yet installed on George's Mac) and Node ≥ 22 for the console.
- Cloud cost: a demo deployment is created and destroyed on demand (`terraform apply` /
  `terraform destroy`), with an AWS Budgets alarm. Nothing runs in the cloud permanently.

## 5. Feature scope

### v1

| # | Feature | What it shows |
|---|---|---|
| F1 | **Payments API**: authorise, capture (full or partial), void, refund (partial, repeatable). | REST design, a state machine |
| F2 | **Idempotency keys**: every write request needs an `Idempotency-Key` header. Same key and same body replays the stored response; same key with a different body returns `422`; a request still in flight returns `409`. | Safe retries |
| F3 | **Mock provider**: a separate service acting as the card company, with seeded "chaos dials" (§8). | Testing against failure |
| F4 | **Unknown-outcome resolution**: a timeout or ambiguous reply puts the payment in `Unknown`; a background resolver asks the provider for the real result, with backoff, until it is known. | Distributed-systems judgment |
| F5 | **Double-entry ledger**: every money movement is a balanced, append-only journal in integer cents. | Accounting correctness |
| F6 | **Settlement import**: the provider publishes a daily settlement CSV; import is idempotent (by file hash), validates every row and rejects bad files whole. | Data integrity |
| F7 | **Reconciliation**: match settlement rows to captures and refunds; post settlement journals; create typed exceptions (§7.6). | The core business problem |
| F8 | **Exceptions workflow**: open → investigating → resolved, with a required resolution note, the person who resolved it and a timestamp; full audit trail. | Auditability |
| F9 | **Ops console**: a small React + TypeScript page listing payments, ledger balances, reconciliation runs and exceptions. | Full-stack delivery |
| F10 | **Observability**: OpenTelemetry traces, metrics and logs into Grafana; dashboards, alerts and a runbook for staged incidents. | Operating software |
| F11 | **CI/CD**: GitHub Actions running build, tests, chaos suite, security scans and an image build; Terraform deploys to AWS. | DevOps, AWS |
| F12 | **AI exception triage**: Claude reads an exception's facts through read-only tools and proposes a likely cause and next step; a person accepts or rejects it. | Applied LLM work with guardrails |

### v2 (only after v1 is shipped)

- Signed merchant webhooks (`payment.captured`, …) through a transactional outbox, with retries
  and duplicate delivery.
- A read-only MCP server exposing ledger and exception queries to Claude Code.
- Multi-currency, with FX treated as its own ledger account.
- Mutation-testing score badge (Stryker.NET) published in the README.

## 6. Architecture

```
                  ┌──────────────────────── docker compose / AWS ECS ───────────────────────┐
  load-generator ─┼─▶ Payments.Api ──HTTP (+ Idempotency-Key)──▶ MockProvider               │
  (fake merchant) │     │   ▲                                     │  chaos dials, seeded     │
                  │     │   │ ops console (React)                 │  daily settlement CSV    │
                  │     ▼   │                                     ▼                          │
                  │   PostgreSQL ◀──── Payments.Worker ◀──── settlement files (local / S3)    │
                  │   (ledger,          · unknown-outcome resolver                           │
                  │    payments,        · settlement import + reconciliation                 │
                  │    exceptions)      · AI triage (Claude API, read-only tools)            │
                  │                                                                         │
                  │   OpenTelemetry Collector ──▶ Grafana (traces, metrics, logs, alerts)   │
                  └─────────────────────────────────────────────────────────────────────────┘
```

- **Payments.Domain**: pure C#, no I/O. The state machine, ledger posting rules, money type and
  reconciliation matcher live here and get most of the unit and property tests.
- **Payments.Infrastructure**: EF Core + Npgsql, the provider HTTP client (with Polly-style
  retries and timeouts), the settlement file reader.
- **Payments.Api**: ASP.NET Core minimal APIs, API-key auth, idempotency middleware,
  ProblemDetails errors (RFC 9457).
- **Payments.Worker**: a .NET worker service for background jobs. A Postgres row lock
  (`FOR UPDATE SKIP LOCKED`) ensures that running two copies never processes the same item
  twice.
- **MockProvider**: a separate ASP.NET Core app, so network failures are real network failures.
- **ops-console**: React + TypeScript + Vite, read-mostly.

### Stack

| Concern | Choice | Why |
|---|---|---|
| Language | C# on .NET 10 (LTS) | Toronto bank and fintech backends lean .NET/Java; George has .NET experience to deepen |
| Database | PostgreSQL 17 | Transactions, constraints and row locking do real work here |
| ORM | EF Core 10 + Npgsql; raw SQL for ledger checks | Industry standard; the SQL stays visible where it matters |
| Tests | xUnit, Testcontainers, CsCheck (property tests), Stryker.NET | Real database in tests; invariants checked on random inputs |
| Front end | React + TypeScript + Vite | Keeps George's primary stack visible |
| Telemetry | OpenTelemetry → `grafana/otel-lgtm` (local), Grafana Cloud free tier or self-hosted (cloud) | Vendor-neutral and standard |
| CI | GitHub Actions | Free for public repos; visible to recruiters |
| Cloud | AWS: ECS Fargate, RDS PostgreSQL, S3, Secrets Manager, CloudWatch; Terraform | Closes the AWS gap; a common bank-adjacent stack |
| AI | Claude API (Sonnet for triage, Haiku for cheap eval runs) | Model IDs confirmed against current docs at build time |

## 7. Domain model

### 7.1 Money
`Money` is a value type holding a `long` count of **cents** and a currency (CAD only). No
`double` or `decimal` arithmetic on amounts anywhere. Amounts must be > 0 on the way in. See
the vault lesson *Money as Integer Cents*.

### 7.2 Payment state machine

```
             authorise
  (new) ───────────────▶ Authorizing ──ok──▶ Authorized ──capture──▶ Captured ──refund──▶ PartiallyRefunded ──▶ Refunded
                              │  │                │                      │                     │
                       declined│  │timeout/       └──void──▶ Voided       └──refund (full)──────┴──▶ Refunded
                              ▼  │ambiguous
                         Declined ▼
                               Unknown ──resolver──▶ Authorized | Declined
```

- Only these edges are allowed; any other transition throws a domain error and is a `409` at
  the API.
- Captures and refunds have their own small state machines (`Pending → Succeeded | Failed |
  Unknown`), so a refund that times out is also resolved rather than guessed.
- Every transition is stored in `payment_events` (append-only) with time, actor and
  correlation ID.

### 7.3 Ledger

Accounts (v1): `provider_clearing`, `merchant_payable`, `bank_cash`, `processing_fees`.

| Event | Debit | Credit |
|---|---|---|
| Capture $10.00 | provider_clearing 1000 | merchant_payable 1000 |
| Refund $4.00 | merchant_payable 400 | provider_clearing 400 |
| Settlement: provider pays net $5.41 after a $0.59 fee on the above | bank_cash 541, processing_fees 59 | provider_clearing 600 |

Authorisations do not post to the ledger (no money has moved yet).

### 7.4 Merchants and API keys
API keys are random 32-byte secrets shown once; only a SHA-256 hash is stored. Keys can be
revoked, and a revoked key fails immediately (test required). Each request is scoped to the
key's merchant.

### 7.5 Idempotency
Table `idempotency_keys (merchant_id, key, request_hash, state, response_status,
response_body, created_at, expires_at)`, unique on `(merchant_id, key)`. Keys expire after
24 h. The payment row, its events and its ledger journal commit in **one transaction** with the
idempotency record. The same key is also forwarded to the provider, so provider-side retries
are safe too.

### 7.6 Reconciliation exceptions

| Type | Meaning |
|---|---|
| `MissingInSettlement` | We captured it; the provider's file doesn't list it. May be late, so it is only raised after an ageing window (default 2 settlement days). |
| `MissingInLedger` | The provider settled something we have no record of. |
| `AmountMismatch` | Both sides have it, but the amounts differ. |
| `FeeMismatch` | The fee differs from the agreed fee schedule. |
| `DuplicateInSettlement` | The same provider reference appears twice. |
| `StatusMismatch` | For example, we have it as voided and the provider settled it. |

Each exception keeps its evidence (row IDs on both sides) and an audit trail. Resolving one
never edits past journals: corrections are new, balancing journals (vault: *Ledger vs Replay*).

## 8. Mock provider and chaos dials

All dials are set per run through config or `POST /admin/chaos`, and are driven by a **seed** so
any failure can be replayed exactly.

| Dial | Effect |
|---|---|
| `latency_ms` | Added delay (fixed or random range) |
| `timeout_after_success_pct` | Processes the charge, then drops the connection: the classic double-charge trap |
| `error_5xx_pct` | Returns 500/503 without processing |
| `decline_pct` | Declines the card (also forced by tokens like `tok_visa_decline`) |
| `ambiguous_pct` | Returns 200 with `status: "pending_review"` |
| `settlement_drop_pct` | Leaves a captured row out of the settlement file |
| `settlement_amount_skew_pct` | Changes an amount by a few cents |
| `settlement_duplicate_pct` | Repeats a row |
| `settlement_late_pct` | Moves a row to the next day's file |
| `settlement_orphan_count` | Adds rows for charges we never made |

## 9. Testing strategy

### 9.1 Invariants (each has an automated test)
- **I1** Every journal balances: total debits = total credits. Also enforced in the database by
  a deferred constraint trigger.
- **I2** Ledger entries and payment events are never updated or deleted (a database trigger
  rejects it, and the app role lacks the permission).
- **I3** One idempotency key leads to at most one provider charge, even with 50 concurrent
  identical requests.
- **I4** Refunds never exceed the captured amount; captures never exceed the authorised amount.
- **I5** After reconciliation of day D, `provider_clearing` for D is zero, or every non-zero
  cent is linked to an open exception.
- **I6** Every state change follows an allowed edge (§7.2).
- **I7** Importing the same settlement file twice changes nothing.

### 9.2 Layers
- **Unit / property** (Domain): state-machine edges, posting rules, matcher; CsCheck generates
  random sequences of operations and checks I1, I4 and I6 after each step.
- **Integration**: API + real PostgreSQL (Testcontainers) + real MockProvider container.
- **Contract**: the provider client against the MockProvider's OpenAPI schema.
- **End-to-end**: `docker compose` up, run the load generator, run reconciliation, assert I5.
- **Mutation**: Stryker.NET on `Payments.Domain`, with a target score of ≥ 80 %.

### 9.3 Chaos suite (from project idea #28)
Named, seeded scenarios that run in CI. For example:
`timeout_after_success_then_retry`, `concurrent_same_key_x50`, `provider_down_5_min`,
`settlement_with_every_defect`, `duplicate_settlement_file`, `refund_timeout`. Each one prints
a short report: requests sent, provider charges, journals, balance check, exceptions raised.

## 10. AI exception triage (F12)

### 10.1 What it does
For one open exception, the assistant receives the exception and can call **read-only tools**:
`get_payment`, `get_payment_events`, `get_journals`, `get_settlement_rows`,
`get_provider_status` (asks the MockProvider). It returns a structured suggestion:
`{ likely_cause, confidence, evidence[], suggested_action }`. The ops console shows it next to
the exception with "Accept" (copied into the resolution note, still needing a person to
resolve) or "Reject" (with a reason).

### 10.2 Guardrails
- Tools are read-only at the code level (no write tool exists) and use a database role with
  `SELECT` only.
- Settlement file contents are untrusted input; they are passed as data, never as instructions.
- Every call is logged with a prompt hash, model, token counts, latency and the suggestion, so
  an auditor can see what the AI said and what the person decided.
- With no API key the feature is off and the rest of the system works unchanged.

### 10.3 Evaluation
A labelled set of ≥ 30 seeded exceptions (cause known because the chaos dial created it).
`dotnet run --project tools/Eval` reports accuracy per exception type and overall, and
calibration (are "high confidence" answers right more often?). The eval runs on demand and in
a weekly scheduled CI job, not on every PR (cost). Results go in the README.

## 11. Observability

- Traces span API → provider → database → worker, with `payment_id` and `idempotency_key` as
  attributes.
- Metrics: payments by outcome, provider latency p50/p95/p99, `Unknown` count and age,
  resolver retries, reconciliation run duration, open exceptions by type.
- Structured JSON logs, with no card tokens or API keys in any log (a test checks this).
- Alerts: `Unknown` older than 15 min, provider error rate > 5 % over 5 min, any I5 violation,
  reconciliation run failed.
- `docs/runbook.md` covers three staged incidents (provider outage, slow database, bad
  settlement file): symptom, dashboard, trace, root cause, fix.

## 12. Delivery: Git workflow and milestones

### 12.1 Workflow
- `main` is protected: changes arrive only through pull requests, with CI green and at least one
  review (George's). No direct pushes, no force-pushes.
- Branch names: `feat/…`, `fix/…`, `test/…`, `docs/…`, `chore/…`, `infra/…`.
- Commits follow Conventional Commits (`feat(ledger): post capture journals`).
- One PR = one reviewable slice (aim for < 400 changed lines, tests included). The PR template
  asks: what, why, how it was tested, screenshots or trace links.
- Claude opens PRs; **George reviews and merges** (squash merge), then deletes the branch.
- Each phase ends with a tag (`v0.1.0` …) and a short release note.

### 12.2 Milestones (planned PRs)

| Phase | PRs (branches) | Done when |
|---|---|---|
| **0. Setup** | `docs/spec` (this), `chore/solution-skeleton` (solution, projects, compose with Postgres, CI build+test, PR template, branch protection) | CI green on an empty test |
| **1. Core payments** (~3–4 wks) | `feat/money-and-state-machine`, `feat/ledger`, `feat/mock-provider`, `feat/payments-api-idempotency`, `feat/unknown-resolver`, `test/chaos-suite-v1` | I1–I4, I6 pass; chaos scenarios for timeouts and concurrency pass → **v0.1.0** |
| **2. Reconciliation** (~2–3 wks) | `feat/settlement-import`, `feat/reconciliation-matcher`, `feat/exceptions-workflow`, `feat/ops-console` | I5, I7 pass; `settlement_with_every_defect` raises exactly the expected exceptions → **v0.2.0** |
| **3. Operations** (~2 wks) | `feat/otel`, `docs/runbook`, `infra/terraform-aws`, `ci/security-scans` (CodeQL, dependency review, gitleaks), `ci/deploy` | Dashboards and alerts work locally; demo deploys to AWS and tears down cleanly → **v0.3.0** |
| **4. AI triage** (~2 wks) | `feat/ai-triage-tools`, `feat/ai-triage-console`, `test/ai-eval` | Eval ≥ 80 %, guardrail tests pass → **v1.0.0** |
| **Polish** | `docs/readme-demo` | README diagram, demo GIF, "what this shows", honest limitations |

## 13. Risks

| Risk | Mitigation |
|---|---|
| Scope creep: payments can grow forever | Non-goals in §3; v2 items wait until v1.0.0 ships |
| AWS bill | Destroy after each demo; Budgets alarm; small instance sizes; no NAT gateway |
| New to .NET 10 / EF Core details | Domain is pure C#; learn EF Core in phase 1 with tests as a safety net |
| AI cost | Haiku for eval runs; eval not on every PR; feature off without a key |
| "AI wrote it" doubts in interviews | George writes or rewrites the domain layer himself and keeps a decisions log in `docs/decisions/` |

## 14. Open decisions for George

1. **Repo name and visibility**: working name `payment-reconciliation-service`, public on
   `github.com/gcode33` (recommended, since recruiters need to see it).
2. **Test framework**: xUnit (recommended, most common in new .NET code) or NUnit (which
   George has used before).
3. **Ops console**: React + TypeScript (recommended) or Blazor.
4. **Licence**: MIT (recommended for a portfolio) or none.
