# Spec: Payment Reconciliation Service

Status: **v1.1, approved by George 2026-10-19.** v1.1 adds Stripe test mode as a real provider
and live Bank of Canada exchange rates. The build order is in `docs/PLAN.md`. Decisions are in
§14.

## 1. In plain words

A coffee shop's card machine asks a card company "can this customer pay $5?". The shop writes
every sale in a notebook it never erases (a **ledger**). Days later the card company pays the
shop, minus a fee, and sends a statement. The owner checks the notebook against the statement
line by line (**reconciliation**).

This project is the software in the middle. It talks to a card company, keeps the notebook
honest, and checks the statement every day. The hard and interesting part is what happens when
things go wrong: dropped connections, "maybe" answers, missing or wrong lines on the statement.
The system must never charge twice, never lose a cent, and explain every gap. An AI assistant
(Claude) helps a person investigate gaps but can never change the books.

**What's real and what's simulated.** The card company is **Stripe, used through its real API
in test mode** (real endpoints, webhooks and reports; test cards; no real money). Exchange rates
for US-dollar payments are **live daily rates from the Bank of Canada**. A second, simulated
card company with "chaos dials" exists because Stripe can't be made to fail on demand, and
proving the failure cases is the point.

## 2. Goal

A portfolio project that shows, end to end, the production habits Toronto bank and fintech
teams hire for: correctness with money, safe retries, failure handling, auditability,
observability, automated testing, cloud deployment and a real provider integration, plus a
carefully limited AI feature.

**Success, measured:**
- `docker compose up` starts the full system and a seeded demo against the mock provider in
  under 5 minutes on a clean machine, following only the README. No accounts or keys needed.
- With a Stripe test key set, the same demo runs against Stripe: authorise → capture → refund →
  reconcile against Stripe's own balance transactions, with zero unexplained cents.
- The chaos test suite (§9.3) passes in CI on every PR: under injected timeouts, duplicates and
  provider errors there are **zero** double charges and **zero** unbalanced journals.
- Every reconciliation run ends with the provider clearing account at zero, or every non-zero
  cent attached to an open exception (invariant I5).
- Every USD payment is booked in CAD at a stored Bank of Canada rate, and recomputing it gives
  the same cents (invariant I8).
- The AI triage assistant reaches **≥ 80 %** correct cause classification on the labelled
  evaluation set (§10.3), and the number is published in the README.
- A recruiter can understand the project from the README in 2 minutes (diagram, demo GIF,
  "what this shows" list).

## 3. Non-goals (explicit)

- No real money, real card numbers or real customers. Stripe is used **only in test mode**: the
  app refuses to start if given a live key (`sk_live_…`/`rk_live_…`). Cards are Stripe test
  payment methods (e.g. `pm_card_visa`) or mock tokens (e.g. `tok_visa_ok`). Nothing is in
  scope for PCI DSS: no card numbers ever reach this system.
- Not a certified or production payment product, and the README says so.
- Two currencies: payments in **CAD or USD**; the books are kept in **CAD**. One country, one
  demo merchant in v1.
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
- Runs on macOS and Linux with Docker. Needs the .NET 10 SDK and Node ≥ 22 for the console.
- Optional: a free Stripe account (test mode only) and the Stripe CLI for forwarding webhooks
  locally. Keys live in `dotnet user-secrets` or a git-ignored `.env`, never in the repo.
- Internet access is needed for Stripe and the Bank of Canada. With neither, the system runs
  against the mock provider and a seeded rate table.
- Cloud cost: a demo deployment is created and destroyed on demand (`terraform apply` /
  `terraform destroy`), with an AWS Budgets alarm. Nothing runs in the cloud permanently.

## 5. Feature scope

### v1

| # | Feature | What it shows |
|---|---|---|
| F1 | **Payments API**: authorise, capture (full or partial), void, refund (partial, repeatable), in CAD or USD. | REST design, a state machine |
| F2 | **Idempotency keys**: every write request needs an `Idempotency-Key` header. Same key and same body replays the stored response; same key with a different body returns `422`; a request still in flight returns `409`. | Safe retries |
| F3 | **Provider abstraction**: one `IPaymentProvider` interface with two implementations, Mock and Stripe, chosen per merchant by config. | Clean boundaries |
| F4 | **Mock provider**: a separate service acting as a card company, with seeded "chaos dials" (§8). | Testing against failure |
| F5 | **Stripe provider (test mode)**: PaymentIntents with manual capture, refunds, idempotency keys forwarded to Stripe, and status mapping (§7.8). | Real third-party integration |
| F6 | **Stripe webhooks**: signature-verified, deduplicated by event ID, processed out of order safely; used to learn outcomes faster, never as the only source of truth. | Event handling, security |
| F7 | **Unknown-outcome resolution**: a timeout or ambiguous reply puts the payment in `Unknown`; a background resolver asks the provider for the real result, with backoff, until it is known. | Distributed-systems judgment |
| F8 | **Double-entry ledger**: every money movement is a balanced, append-only journal in CAD integer cents. | Accounting correctness |
| F9 | **Live FX rates**: daily USD/CAD from the Bank of Canada Valet API, stored with source and date; a fixed rule picks the rate for each payment (§7.7). | Working with a real external data feed |
| F10 | **Settlement import**: from the mock provider's daily CSV **or** Stripe's balance transactions; idempotent; every row validated; a bad batch is rejected whole. | Data integrity |
| F11 | **Reconciliation**: match settlement rows to captures and refunds; post settlement, fee and FX-difference journals; create typed exceptions (§7.6). | The core business problem |
| F12 | **Exceptions workflow**: open → investigating → resolved, with a required resolution note, the person who resolved it and a timestamp; full audit trail. | Auditability |
| F13 | **Ops console**: a small React + TypeScript page listing payments, ledger balances, FX rates, reconciliation runs and exceptions. | Full-stack delivery |
| F14 | **Observability**: OpenTelemetry traces, metrics and logs into Grafana; dashboards, alerts and a runbook for staged incidents. | Operating software |
| F15 | **CI/CD**: GitHub Actions running build, tests, chaos suite, security scans and an image build; Terraform deploys to AWS. | DevOps, AWS |
| F16 | **AI exception triage**: Claude reads an exception's facts through read-only tools and proposes a likely cause and next step; a person accepts or rejects it. | Applied LLM work with guardrails |

### v2 (only after v1 is shipped)

- Signed outbound merchant webhooks (`payment.captured`, …) through a transactional outbox.
- A read-only MCP server exposing ledger and exception queries to Claude Code.
- Disputes (chargebacks) from Stripe's dispute test cards, with their own ledger postings.
- More currencies.
- Mutation-testing score badge (Stryker.NET) published in the README.

## 6. Architecture

```
                 ┌─────────────────────────── docker compose / AWS ECS ───────────────────────────┐
 load-generator ─┼─▶ Payments.Api ──IPaymentProvider──┬──▶ MockProvider (chaos dials, daily CSV)   │
 (fake merchant) │     │   ▲   ▲                      └──▶ Stripe API (test mode) ─── internet      │
                 │     │   │   └── Stripe webhooks (signed) ◀───────────────────────┘               │
                 │     │   │ ops console (React)                                                    │
                 │     ▼   │                                                                        │
                 │   PostgreSQL ◀──── Payments.Worker                                               │
                 │   (ledger,          · unknown-outcome resolver                                   │
                 │    payments,        · settlement import (mock CSV / Stripe balance transactions)  │
                 │    fx_rates,        · reconciliation                                             │
                 │    exceptions)      · FX rate fetch ──▶ Bank of Canada Valet API ── internet     │
                 │                     · AI triage (Claude API, read-only tools)                    │
                 │                                                                                  │
                 │   OpenTelemetry Collector ──▶ Grafana (traces, metrics, logs, alerts)            │
                 └──────────────────────────────────────────────────────────────────────────────────┘
```

- **Payments.Domain**: pure C#, no I/O. The state machine, ledger posting rules, money and FX
  types and the reconciliation matcher live here and get most of the unit and property tests.
- **Payments.Infrastructure**: EF Core + Npgsql; provider clients (`MockProviderClient`,
  `StripeProviderClient` on the official Stripe.net SDK) behind `IPaymentProvider`, with retries
  and timeouts; settlement sources behind `ISettlementSource`; the Bank of Canada client.
- **Payments.Api**: ASP.NET Core minimal APIs, API-key auth, idempotency middleware, the Stripe
  webhook endpoint, ProblemDetails errors (RFC 9457).
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
| Provider | Stripe API in test mode via Stripe.net; Stripe CLI for local webhooks | A real, widely used payments API |
| FX data | Bank of Canada Valet API (`/valet/observations/FXUSDCAD/json`), no key needed | Official, free, live Canadian data |
| Tests | xUnit, Testcontainers, CsCheck (property tests), Stryker.NET | Real database in tests; invariants checked on random inputs |
| Front end | React + TypeScript + Vite | Keeps George's primary stack visible |
| Telemetry | OpenTelemetry → `grafana/otel-lgtm` (local), Grafana Cloud free tier or self-hosted (cloud) | Vendor-neutral and standard |
| CI | GitHub Actions | Free for public repos; visible to recruiters |
| Cloud | AWS: ECS Fargate, RDS PostgreSQL, S3, Secrets Manager, CloudWatch; Terraform | Closes the AWS gap; a common bank-adjacent stack |
| AI | Claude API (Sonnet for triage, Haiku for cheap eval runs) | Model IDs confirmed against current docs at build time |

## 7. Domain model

### 7.1 Money
`Money` is a value type holding a `long` count of **minor units** (cents) and a currency (`CAD`
or `USD`). No `double` or `decimal` arithmetic on amounts anywhere; adding money of different
currencies is a compile-time or domain error. Amounts must be > 0 on the way in. See the vault
lesson *Money as Integer Cents*.

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

- One capture per payment, for up to the authorised amount; the rest is released. A failed
  capture can be retried (docs/decisions/0004).
- A refund that fails after succeeding moves the payment back: `PartiallyRefunded → Captured`,
  `Refunded → PartiallyRefunded` and `Refunded → Captured` (docs/decisions/0004).
- Only these edges are allowed; any other transition throws a domain error and is a `409` at
  the API.
- Captures and refunds have their own small state machines (`Pending → Succeeded | Failed |
  Unknown`), so a refund that times out is also resolved rather than guessed. A refund can also
  go `Succeeded → Failed` later (Stripe's `pm_card_refundFail` test card does this); that posts a
  reversing journal.
- Every transition is stored in `payment_events` (append-only) with time, actor and
  correlation ID.

### 7.3 Ledger

All journals are in **CAD cents**. Accounts (v1): `provider_clearing`, `merchant_payable`,
`bank_cash`, `processing_fees`, `fx_gain_loss`.

**CAD example**

| Event | Debit | Credit |
|---|---|---|
| Capture $10.00 | provider_clearing 1000 | merchant_payable 1000 |
| Refund $4.00 | merchant_payable 400 | provider_clearing 400 |
| Settlement: provider pays net $5.41 after a $0.59 fee on the above | bank_cash 541, processing_fees 59 | provider_clearing 600 |

**USD example** (figures illustrative): capture US$10.00 when the Bank of Canada rate is
1.4097 CAD per USD, so it is booked at CA$14.10. The provider later converts at its own rate,
pays CA$13.20 and charges a CA$0.70 fee.

| Event | Debit | Credit |
|---|---|---|
| Capture US$10.00 at 1.4097 | provider_clearing 1410 | merchant_payable 1410 |
| Settlement | bank_cash 1320, processing_fees 70, fx_gain_loss 20 | provider_clearing 1410 |

Authorisations do not post to the ledger (no money has moved yet).

### 7.4 Merchants and API keys
API keys are random 32-byte secrets shown once; only a SHA-256 hash is stored. Keys can be
revoked, and a revoked key fails immediately (test required). Each request is scoped to the
key's merchant. Each merchant is configured with one provider (`mock` or `stripe`).

### 7.5 Idempotency
Table `idempotency_keys (merchant_id, key, request_hash, state, response_status,
response_body, created_at, expires_at)`, unique on `(merchant_id, key)`. Keys expire after
24 h. The payment row, its events and its ledger journal commit in **one transaction** with the
idempotency record. A per-attempt key derived from it is forwarded to the provider. Stripe
stores the first response for a key for at least 24 h and rejects reuse with different
parameters, so a retry after a timeout returns the original result instead of charging again.

### 7.6 Reconciliation exceptions

| Type | Meaning |
|---|---|
| `MissingInSettlement` | We captured it; the provider doesn't list it. May be late, so it is only raised after an ageing window (default 2 settlement days). |
| `MissingInLedger` | The provider settled something we have no record of. |
| `AmountMismatch` | Both sides have it, but the amounts differ (in the original currency). |
| `FeeMismatch` | The fee differs from the agreed fee schedule. |
| `FxOutOfTolerance` | The provider's CAD amount differs from ours at the Bank of Canada rate by more than the configured tolerance. Small differences post to `fx_gain_loss`; large ones are exceptions. |
| `DuplicateInSettlement` | The same provider reference appears twice. |
| `StatusMismatch` | For example, we have it as voided and the provider settled it, or a refund later failed. |

Each exception keeps its evidence (row IDs on both sides) and an audit trail. Resolving one
never edits past journals: corrections are new, balancing journals (vault: *Ledger vs Replay*).

### 7.7 FX rates
- The worker fetches `FXUSDCAD` from the Bank of Canada Valet API each business day and stores
  `(date, rate, source, fetched_at)` in `fx_rates`. Rates are published on **business days
  only** (no weekends or holidays) as a daily average in CAD per USD with 4 decimals.
- Rates are stored as integers scaled by 10,000 (1.4097 → 14097), so conversion is exact:
  `cad_cents = round_half_even(usd_cents × rate_scaled / 10,000)`, rounded **once**.
- **Rule:** a payment uses the latest published rate on or before its capture date. The
  journal records the rate, rate date and source.
- If no rate is newer than 3 business days, USD captures are refused with a clear error, and an
  alert fires. Mock mode uses a seeded rate table.

### 7.8 Stripe mapping
- Authorise = create a PaymentIntent with `capture_method=manual` and `confirm=true`; capture,
  void (cancel) and refund map to the matching Stripe calls.
- Status mapping: `requires_capture` → Authorized; `succeeded` → Captured; `canceled` → Voided;
  `requires_payment_method` after a failed confirm → Declined; `processing` or no response →
  Unknown.
- Every PaymentIntent carries our `payment_id` in `metadata`, so the resolver can always find
  the provider's record.
- Settlement source: Stripe **balance transactions** (each with amount, fee, net, exchange rate
  and `available_on`). Test mode settles instantly rather than over days; the spec does not rely
  on settlement timing, and the differences are documented in the README.

## 8. Mock provider and chaos dials

All dials are set per run through config or `POST /admin/chaos`, and are driven by a **seed** so
any failure can be replayed exactly. Stripe scenarios use Stripe's test cards instead
(`pm_card_visa`, `pm_card_visa_chargeDeclined`, `pm_card_pendingRefund`, `pm_card_refundFail`, …).

| Dial | Effect |
|---|---|
| `latency_ms` | Added delay (fixed or random range) |
| `timeout_after_success_pct` | Processes the charge, then drops the connection: the classic double-charge trap |
| `error_5xx_pct` | Returns 500/503 without processing |
| `decline_pct` | Declines the card (also forced by tokens like `tok_visa_decline`) |
| `ambiguous_pct` | Returns 200 with `status: "pending_review"` |
| `fx_spread_bps` | The rate the mock converts USD at, relative to the Bank of Canada rate |
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
- **I5** After reconciliation of day D, the `provider_clearing` balance of the journals linked to
  D's captures, refunds and settlement rows is zero, or every non-zero cent is linked to an
  open exception.
- **I6** Every state change follows an allowed edge (§7.2).
- **I7** Importing the same settlement batch twice changes nothing.
- **I8** Every USD journal stores its rate, rate date and source, and recomputing the CAD amount
  from them gives the same cents.
- **I9** The app refuses to start with a Stripe live key, and no secret appears in any log.

### 9.2 Layers
- **Unit / property** (Domain): state-machine edges, posting rules, FX conversion, matcher;
  CsCheck generates random sequences of operations and checks I1, I4, I6 and I8 after each step.
- **Integration**: API + real PostgreSQL (Testcontainers) + real MockProvider container.
- **Contract**: the mock client against the MockProvider's OpenAPI schema; the Bank of Canada
  client against recorded real responses (including a holiday gap).
- **Stripe integration** (tagged `Stripe`): the full flow against Stripe test mode. Runs nightly
  and on demand with a repository secret, not as a required PR check, so Stripe outages can't
  block merges.
- **End-to-end**: `docker compose` up, run the load generator, run reconciliation, assert I5.
- **Mutation**: Stryker.NET on `Payments.Domain`, with a target score of ≥ 80 %.

### 9.3 Chaos suite (from project idea #28)
Named, seeded scenarios that run in CI against the mock. For example:
`timeout_after_success_then_retry`, `concurrent_same_key_x50`, `provider_down_5_min`,
`settlement_with_every_defect`, `duplicate_settlement_file`, `refund_timeout`,
`fx_rate_missing_over_holiday`, `webhook_duplicate_and_out_of_order`. Each one prints a short
report: requests sent, provider charges, journals, balance check, exceptions raised.

## 10. AI exception triage (F16)

### 10.1 What it does
For one open exception, the assistant receives the exception and can call **read-only tools**:
`get_payment`, `get_payment_events`, `get_journals`, `get_settlement_rows`, `get_fx_rate`,
`get_provider_status` (asks the payment's provider). It returns a structured suggestion:
`{ likely_cause, confidence, evidence[], suggested_action }`. The ops console shows it next to
the exception with "Accept" (copied into the resolution note, still needing a person to
resolve) or "Reject" (with a reason).

### 10.2 Guardrails
- Tools are read-only at the code level (no write tool exists) and use a database role with
  `SELECT` only.
- Settlement data and provider responses are untrusted input; they are passed as data, never as
  instructions.
- Every call is logged with a prompt hash, model, token counts, latency and the suggestion, so
  an auditor can see what the AI said and what the person decided.
- With no API key the feature is off and the rest of the system works unchanged.

### 10.3 Evaluation
A labelled set of ≥ 30 seeded exceptions (cause known because the chaos dial created it).
`dotnet run --project tools/Eval` reports accuracy per exception type and overall, and
calibration (are "high confidence" answers right more often?). The eval runs on demand and in
a weekly scheduled CI job, not on every PR (cost). Results go in the README.

## 11. Observability

- Traces span API → provider → database → worker, with `payment_id`, `provider` and
  `idempotency_key` as attributes.
- Metrics: payments by outcome and provider, provider latency p50/p95/p99, `Unknown` count and
  age, resolver retries, webhook lag and duplicates, FX rate age, reconciliation run duration,
  open exceptions by type.
- Structured JSON logs, with no card data, API keys or Stripe secrets in any log (a test checks
  this).
- Alerts: `Unknown` older than 15 min, provider error rate > 5 % over 5 min, FX rate older than
  3 business days, any I5 violation, reconciliation run failed.
- `docs/runbook.md` covers four staged incidents (provider outage, slow database, bad
  settlement file, FX feed down): symptom, dashboard, trace, root cause, fix.

## 12. Delivery: Git workflow and milestones

### 12.1 Workflow
- `main` is protected: changes arrive only through pull requests, with CI green (once CI exists,
  from phase 0). No direct pushes, no force-pushes. PRs are opened from George's account, and
  GitHub doesn't let an author approve their own PR, so the rule requires a PR but not a formal
  approval.
- Branch names: `feat/…`, `fix/…`, `test/…`, `docs/…`, `chore/…`, `infra/…`, `ci/…`.
- Commits follow Conventional Commits (`feat(ledger): post capture journals`).
- One PR = one reviewable slice (aim for < 400 changed lines, tests included). The PR template
  asks: what, why, how it was tested, screenshots or trace links.
- **Claude does the Git work**: commits, pushes, opens each PR, reviews it (a review comment
  with findings) and squash-merges it once CI is green; the branch is deleted automatically.
  In background sessions, which may not merge, Claude stops at a green, reviewed PR and gives
  George the one-line merge command. George reads every PR so he can explain it.
- Each phase ends with a tag (`v0.1.0` …) and a short release note.

### 12.2 Milestones

Phases and their PRs are broken down step by step in `docs/PLAN.md`. In summary:

| Phase | Done when |
|---|---|
| **0. Setup** | Solution skeleton, compose with Postgres, CI required on `main`; CI green |
| **1. Core payments** | Mock provider, ledger, API with idempotency, Unknown resolver; I1–I4, I6 pass → **v0.1.0** |
| **2. Stripe** | Stripe provider and webhooks; full flow passes in Stripe test mode → **v0.2.0** |
| **3. Reconciliation and FX** | Bank of Canada rates, settlement import from both sources, matcher, exceptions, ops console; I5, I7, I8 pass → **v0.3.0** |
| **4. Operations** | OTel, runbook, security scans, Terraform deploy and teardown → **v0.4.0** |
| **5. AI triage** | Eval ≥ 80 %, guardrail tests pass → **v1.0.0** |

## 13. Risks

| Risk | Mitigation |
|---|---|
| Scope creep: payments can grow forever | Non-goals in §3; v2 items wait until v1.0.0 ships |
| A Stripe live key used by mistake | Startup check refuses `sk_live_`/`rk_live_`; restricted test keys; gitleaks in CI |
| Stripe test mode differs from live (instant settlement, payouts) | Reconcile against balance transactions, not payout timing; differences documented |
| Stripe or Bank of Canada down | Neither is on the PR-blocking path; mock mode and seeded rates work offline; FX staleness alert |
| AWS bill | Destroy after each demo; Budgets alarm; small instance sizes; no NAT gateway |
| New to .NET 10 / EF Core details | Domain is pure C#; learn EF Core in phase 1 with tests as a safety net |
| AI cost | Haiku for eval runs; eval not on every PR; feature off without a key |
| "AI wrote it" doubts in interviews | George writes or rewrites the domain layer himself and keeps a decisions log in `docs/decisions/` |

## 14. Decisions (George, 2026-10-19)

1. **Repo**: `payment-reconciliation-service`, public on `github.com/gcode33`.
2. **Test framework**: xUnit.
3. **Ops console**: React + TypeScript.
4. **Licence**: MIT.
5. **Real integrations**: Stripe in test mode as a second provider (mock kept for chaos
   testing), and live USD/CAD rates from the Bank of Canada Valet API.
6. **Spec approved** (v1.1).
