# Build plan: Payment Reconciliation Service

Implements `SPEC.md` v1.1. Status: **approved, 2026-10-19.**

> **Update 2026-10-19:** George has handed all implementation and Git work to Claude. Where a
> step says "George writes", Claude now writes it, explains it in the PR, and George reads every
> PR so he can explain the design in interviews. He also keeps `docs/decisions/` current.

Each step below is **one branch and one PR** off `main`, built test-first. Claude commits,
pushes, opens the PR, reviews it and squash-merges it once CI is green (in background sessions,
which may not merge, Claude hands George a one-line merge command). Each phase ends with a tag and a short release note. We stop
for George's go-ahead before starting each new phase.

The order builds a **thin, correct core first** (money, states, ledger, against the mock), then
adds the real integrations (Stripe, Bank of Canada) on top of a core that is already tested.
That way every Stripe surprise is a provider problem, not a ledger bug.

## How each step runs

1. **Learn**: a short plain-words explanation of the idea (with a vault lesson linked or
   written), before any code.
2. **Tests first**: write the failing tests from the "Done when" list.
3. **Build**: make them pass. "Who writes it" says whether George writes the code (Claude
   reviews and explains) or Claude writes it (George reviews and must be able to explain it).
4. **Verify**: CI green, plus the step's manual check.
5. **PR**: template filled in, invariants touched listed. Claude reviews and merges; George
   reads it.
6. **Lesson**: write or update the vault lesson with the real commands and output.

Time estimates assume part-time work alongside the Lynked job: about 13 weeks in total.

## Before we start (George)

| When | What | Why |
|---|---|---|
| Phase 0 | `brew install --cask dotnet-sdk` (needs your Mac password) | .NET 10 SDK |
| Phase 2 | Free Stripe account (Canada) in test mode, plus `brew install stripe/stripe-cli/stripe` | Real provider |
| Phase 4 | AWS account with MFA on the root user and a Budgets alarm (e.g. $10) | Cloud deploy |
| Phase 5 | Anthropic API key with a monthly spend limit | AI triage |

---

## Phase 0 · Setup (½ week)

### 0.1 `chore/solution-skeleton`
**Who writes it:** Claude. George reviews.
- `global.json` pinning .NET SDK 10.0.x; `Directory.Build.props` (nullable on, warnings as
  errors, analyzers); `.editorconfig`.
- Projects: `Payments.Domain`, `Payments.Infrastructure`, `Payments.Api`, `Payments.Worker`,
  `MockProvider`; tests: `Payments.Domain.Tests`, `Payments.IntegrationTests`.
- `docker-compose.yml` with PostgreSQL 17; `GET /health` on the API that checks the database.
- `.github/workflows/ci.yml`: restore, build, `dotnet format --verify-no-changes`, test.
- Make the CI job a required status check on `main`.
- **Done when:**
  - `dotnet test` passes locally and in CI, including one Testcontainers test of `/health`.
  - A PR with a formatting error fails CI.
- **Lessons:** [[Solution Layout in .NET]], [[Testcontainers]].

---

## Phase 1 · Core payments on the mock (3½–4 weeks) → `v0.1.0`

### 1.1 `feat/money`
**Who writes it:** **George** (Claude reviews).
- `Money` (long minor units + `Currency` CAD/USD), arithmetic that refuses mixed currencies,
  no `double`/`decimal`, positive-amount guard.
- **Done when:** unit tests, plus CsCheck property tests (adding and then subtracting gives back
  the original; mixed currencies always throw).
- **Lessons:** update [[Money as Integer Cents]]; new [[Property-Based Testing]].

### 1.2 `feat/payment-state-machine`
**Who writes it:** **George**.
- Payment, Capture and Refund state machines as an explicit transition table (SPEC §7.2),
  domain events, and a `DomainException` for forbidden edges.
- **Done when:** every allowed edge has a test; a generated test proves every *other* edge
  throws (I6); capture ≤ authorised and refunds ≤ captured (I4) hold under CsCheck.
- **Lessons:** [[State Machines]].

### 1.3 `feat/ledger`
**Who writes it:** **George** writes the posting rules; Claude writes the EF Core mapping and
migrations.
- Journals and entries; posting rules for capture, refund and settlement (SPEC §7.3).
- PostgreSQL: deferred constraint trigger for balanced journals (I1); a trigger plus role
  permissions making entries and payment events append-only (I2).
- **Done when:**
  - Domain tests reproduce the SPEC §7.3 CAD table exactly.
  - Integration tests show that an unbalanced insert and an `UPDATE`/`DELETE` on entries are
    rejected by the database itself.
- **Lessons:** [[Double-Entry Bookkeeping]]; update [[Ledger vs Replay]].

### 1.4 `feat/mock-provider`
**Who writes it:** Claude. George reviews.
- MockProvider app: authorise, capture, void, refund and status endpoints; its own idempotency
  by key; chaos dials (SPEC §8) driven by a seed; `POST /admin/chaos`; OpenAPI document.
- **Done when:** the same seed gives the same failures twice in a row (test); each dial has a
  test showing its effect.
- **Lessons:** [[Deterministic Simulation]].

### 1.5 `feat/payments-api`
**Who writes it:** shared. George writes the endpoint handlers; Claude writes API-key auth and
the provider client.
- Merchants and API keys (SHA-256 hash, shown once, revocation); `IPaymentProvider` and
  `MockProviderClient` with timeouts and retries; authorise, capture, void and refund
  endpoints; ProblemDetails errors.
- **Done when:**
  - Integration tests cover the happy path and every forbidden transition (`409`).
  - A revoked key gets `401` on the very next request.
  - A log-scraping test finds no API key in the logs.
- **Lessons:** [[API Keys and Hashing]].

### 1.6 `feat/idempotency`
**Who writes it:** **George**, pairing with Claude.
- `idempotency_keys` table and middleware: replay on the same body, `422` on a different body,
  `409` while in flight; 24 h expiry; one transaction with the payment, events and journal;
  a key derived per attempt and forwarded to the provider.
- **Done when:** 50 concurrent identical requests give exactly one provider charge and 50
  identical responses (I3).
- **Lessons:** [[Idempotency Keys]].

### 1.7 `feat/unknown-resolver`
**Who writes it:** Claude. George reviews.
- Timeouts and ambiguous replies → `Unknown`; worker job using `FOR UPDATE SKIP LOCKED` and
  exponential backoff with jitter; resolves to Authorized or Declined from the provider's
  status.
- **Done when:** with `timeout_after_success_pct = 100`, every payment ends Authorized with one
  charge; two worker copies never resolve the same payment twice.
- **Lessons:** [[Unknown Outcomes]], [[Row Locking with SKIP LOCKED]].

### 1.8 `test/chaos-suite-v1`
**Who writes it:** Claude writes the runner; George writes two of the scenarios.
- Scenario runner plus a printed report; scenarios `timeout_after_success_then_retry`,
  `concurrent_same_key_x50`, `provider_down_5_min`, `refund_timeout`; a CI job that runs them.
- **Done when:** all scenarios pass in CI, with zero double charges and zero unbalanced journals.
- **Release:** tag **`v0.1.0`** with release notes.
- **Interview line:** "Here's the test that proves a timeout after a successful charge can't
  double-charge."

---

## Phase 2 · Stripe in test mode (2 weeks) → `v0.2.0`

### 2.1 `chore/stripe-setup`
**Who writes it:** Claude, with George doing the Stripe dashboard steps.
- George creates a **restricted** test key (only the permissions needed). It is stored with
  `dotnet user-secrets` locally and as a GitHub Actions secret.
- A startup check refuses `sk_live_`/`rk_live_` keys (I9); gitleaks added to CI.
- **Done when:** the app fails fast with a clear message given a fake live-format key (test).
- **Lessons:** [[Secrets in Local Development]].

### 2.2 `feat/stripe-provider`
**Who writes it:** shared. George writes the status mapping; Claude writes the client.
- `StripeProviderClient` on Stripe.net: PaymentIntents with `capture_method=manual`, capture,
  cancel, refund; `payment_id` in metadata; idempotency keys forwarded; status mapping (SPEC
  §7.8); a per-merchant provider setting.
- **Done when:** tests tagged `Stripe` pass in test mode for `pm_card_visa`,
  `pm_card_visa_chargeDeclined`, partial capture, partial refund and `pm_card_refundFail`.
  Retrying a request with the same key returns the original PaymentIntent.
- **Lessons:** [[Integrating a Third-Party Payments API]].

### 2.3 `feat/stripe-webhooks`
**Who writes it:** **George**, pairing with Claude.
- Webhook endpoint with signature verification; a `provider_events` table keyed by event ID
  for deduplication; handlers that are safe when events arrive out of order (they re-read
  the PaymentIntent rather than trusting event order).
- **Done when:**
  - A bad signature gets `400`.
  - Replaying the same event changes nothing.
  - The `webhook_duplicate_and_out_of_order` scenario passes with signed fixture events.
  - `stripe listen --forward-to` works locally (manual check, noted in the PR).
- **Lessons:** [[Webhooks: Signatures, Duplicates and Ordering]].

### 2.4 `ci/stripe-nightly`
**Who writes it:** Claude.
- A scheduled and manual workflow that runs the `Stripe`-tagged tests with the secret. It is
  not a required check.
- **Done when:** one green nightly run is linked in the release notes.
- **Release:** tag **`v0.2.0`**.
- **Interview line:** "The same core runs against Stripe and against my failure simulator,
  because the provider is behind one interface."

---

## Phase 3 · Reconciliation and FX (3½ weeks) → `v0.3.0`

### 3.1 `feat/fx-rates`
**Who writes it:** **George** writes the conversion and rate rule; Claude writes the Valet client.
- Bank of Canada Valet client for `FXUSDCAD`; `fx_rates` table; a daily worker fetch; rates
  scaled by 10,000; `round_half_even` once; the rule "latest rate on or before the capture
  date"; refuse USD captures if the rate is older than 3 business days; USD accepted by the API.
- **Done when:**
  - Contract tests run against recorded real responses, including the 2026-10-12 Thanksgiving
    gap.
  - The SPEC §7.3 USD example reproduces exactly.
  - Recomputing a USD journal gives the same cents (I8, property test).
  - The `fx_rate_missing_over_holiday` scenario passes.
- **Lessons:** [[Exchange Rates and Rounding]].

### 3.2 `feat/settlement-import`
**Who writes it:** Claude. George reviews.
- `ISettlementSource` with two implementations: the mock's daily CSV, and Stripe balance
  transactions (paged). Rows are validated, and any bad row rejects the batch. Batches are
  identified by content hash.
- **Done when:** importing the same batch twice changes nothing (I7); a CSV with one bad row is
  rejected whole with a clear error.
- **Lessons:** update [[Reconciling Overlapping Bank Exports]].

### 3.3 `feat/reconciliation`
**Who writes it:** **George** writes the matcher; Claude writes the run orchestration.
- A pure matching function; settlement, fee and FX-difference journals; every exception type
  from SPEC §7.6, including `FxOutOfTolerance`; the ageing window for `MissingInSettlement`.
- **Done when:**
  - `settlement_with_every_defect` raises exactly the expected exceptions, no more and no fewer.
  - I5 holds after every run.
  - A Stripe test-mode run reconciles with zero unexplained cents.
- **Lessons:** update [[Reconciliation]].

### 3.4 `feat/exceptions-workflow`
**Who writes it:** Claude. George reviews.
- Exception API: list, filter, open → investigating → resolved, with a required note, actor and
  time; correction journals (never edits).
- **Done when:** resolving without a note is rejected; the audit trail shows every change.

### 3.5 `feat/ops-console`
**Who writes it:** **George** (React + TypeScript is his primary stack).
- Vite + React + TypeScript pages: payments, ledger balances, FX rates, reconciliation runs,
  and exceptions with resolve.
- **Done when:** a Playwright test resolves an exception end to end.
- **Release:** tag **`v0.3.0`**.
- **Interview line:** "Reconciliation found a fee mismatch I planted, and the clearing account
  shows exactly which cents are unexplained."

---

## Phase 4 · Operations and AWS (2 weeks) → `v0.4.0`

### 4.1 `feat/otel`
**Who writes it:** Claude. George reviews.
- OpenTelemetry traces, metrics and logs; `grafana/otel-lgtm` in compose; the SPEC §11
  attributes and metrics.
- **Done when:** one payment shows a single trace across API → provider → database → worker.
- **Lessons:** [[OpenTelemetry]].

### 4.2 `feat/dashboards-and-runbook`
**Who writes it:** **George** writes the runbook; Claude writes the dashboards as code.
- Dashboards and the SPEC §11 alerts; `docs/runbook.md` with the four staged incidents.
- **Done when:** each incident is staged with a chaos dial, the alert fires, and the runbook
  steps find the cause.
- **Lessons:** [[Runbooks and Staged Incidents]].

### 4.3 `ci/security-scans`
**Who writes it:** Claude.
- CodeQL, dependency review, Dependabot, and gitleaks already in place from 2.1.
- **Done when:** the scans run on PRs, and a planted vulnerable package fails dependency review
  (demonstrated on a throwaway branch).

### 4.4 `infra/terraform-aws`
**Who writes it:** shared. George writes the networking and database modules with Claude.
- Terraform: VPC (public subnets only, no NAT gateway), ECS Fargate services, RDS PostgreSQL
  (small instance), S3 for settlement files, Secrets Manager, CloudWatch logs, a Budgets alarm;
  remote state in S3.
- **Done when:** `terraform apply` brings the demo up, the health check passes, and
  `terraform destroy` removes everything. The cost of one demo day is noted in the README.
- **Lessons:** [[Terraform]], [[ECS Fargate]].

### 4.5 `ci/deploy`
**Who writes it:** Claude.
- Image build and push, plus a manually triggered deploy workflow using GitHub OIDC (no
  long-lived AWS keys).
- **Done when:** a manual run deploys a tagged image.
- **Release:** tag **`v0.4.0`**.
- **Interview line:** "I can stage an outage, watch the alert fire, and follow the trace to the
  cause."

---

## Phase 5 · AI exception triage (2 weeks) → `v1.0.0`

### 5.1 `feat/ai-triage`
**Who writes it:** shared. George designs the tools and prompt; Claude writes the client.
- Six read-only tools (SPEC §10.1) on a `SELECT`-only database role; structured output;
  untrusted data passed as data; a log of every call; off when there's no key.
- **Done when:**
  - A test proves no tool can write.
  - A planted "ignore your instructions" line in a settlement row doesn't change the output
    format or call any extra tool.
  - With no key, the system runs unchanged.
- **Lessons:** [[LLM Tool Use with Guardrails]].

### 5.2 `feat/ai-triage-console`
**Who writes it:** **George**.
- Suggestion panel with Accept (prefills the note) and Reject (with a reason).
- **Done when:** a Playwright test covers both buttons.

### 5.3 `test/ai-eval`
**Who writes it:** **George** labels the set; Claude writes the runner.
- At least 30 seeded exceptions; accuracy per type, overall accuracy and calibration;
  `tools/Eval`; a weekly CI job.
- **Done when:** accuracy is ≥ 80 % and published in the README.
- **Release:** tag **`v1.0.0`**.
- **Lessons:** [[Evaluating LLM Features]].

---

## Polish · `docs/readme-demo` (½ week)
- README: what it is, the "what's real and what's simulated" box, an architecture diagram, a
  demo GIF, a "what this shows" list, how to run it (mock and Stripe), honest limitations, and
  test, mutation and eval numbers.
- `docs/decisions/`: one short record per big choice (why .NET, why a mock *and* Stripe, why
  balance transactions, the FX rule).
- Pin the repo on George's GitHub profile; add it to his CV and LinkedIn.

## Summary

| Phase | PRs | Weeks | Tag |
|---|---|---|---|
| 0 Setup | 1 | ½ | — |
| 1 Core payments | 8 | 3½–4 | v0.1.0 |
| 2 Stripe | 4 | 2 | v0.2.0 |
| 3 Reconciliation and FX | 5 | 3½ | v0.3.0 |
| 4 Operations and AWS | 5 | 2 | v0.4.0 |
| 5 AI triage | 3 | 2 | v1.0.0 |
| Polish | 1 | ½ | — |
| **Total** | **27** | **about 13** | |
