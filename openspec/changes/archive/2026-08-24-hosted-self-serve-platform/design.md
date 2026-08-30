## Context

See proposal.md - Why. This design works out the full shape: what the hosted service is made of, what crosses the trust boundary between customer infra and hosted infra, how self-serve billing actually functions, and — because this is the single largest commitment in the project so far — how to stage delivery so it doesn't repeat the "build distribution before validating demand" mistake is a known failure mode.

```
┌─────────────────────────────┐        ┌──────────────────────────────────────┐
│      CUSTOMER'S INFRA         │        │           HOSTED PLATFORM             │
│                               │        │                                        │
│  ReleaseTwin.Cli              │        │  ┌────────────┐   ┌─────────────────┐  │
│  ├─ CaseExecutor (unchanged)  │        │  │ ingest-api │──▶│   database        │  │
│  ├─ real fixtures, secrets,   │        │  └────────────┘   │  (reports,        │  │
│  │  response bodies stay      │  HTTPS │        ▲          │   orgs, tokens,   │  │
│  │  here, never uploaded      │───────▶│        │          │   subscriptions)  │  │
│  └─ optional upload step      │ bearer │  ┌────────────┐   └─────────────────┘  │
│     (report metadata only)    │  token │  │ dashboard  │◀──────────┘            │
│                               │        │  │ (web UI)   │                        │
└─────────────────────────────┘        │  └────────────┘                        │
                                         │        ▲                               │
                                         │        │ web session (OAuth/magic link)│
                                         │  ┌────────────┐   ┌─────────────────┐  │
                                         │  │ account    │──▶│ billing (Stripe)  │  │
                                         │  │ provisioning│   └─────────────────┘  │
                                         │  └────────────┘                        │
                                         └──────────────────────────────────────┘
```

## Goals / Non-Goals

**Goals:**
- A customer goes from "never heard of this" to "seeing their own real test results on a dashboard" with zero human interaction on either side.
- Nothing sensitive (fixture content, response bodies, secrets) ever leaves the customer's own infra — the hosted side only ever sees report metadata, which was already the shape of `CaseReport`/`FlagProofResult` before this change.
- Self-serve billing that doesn't require a sales conversation to convert free → paid.
- A staged build order that produces a working, demonstrable loop before any billing complexity is added.

**Non-Goals (this change is Stage 1 only; billing is Stage 2, a separate future change — see D7):**
- Hosted execution (already ruled out — "runs on their infra" was the explicit decision).
- Enterprise features (SSO, audit logs, custom retention, private deployment) — these come after paid demand is demonstrated, not before.
- A second adapter, external-check connector, or any change to `ReleaseTwin.Core`/`ReleaseTwin.AdapterSdk` — this change is purely additive at the CLI/hosted boundary.

## Decisions

### D1: Ingest API has its own contract, not a direct wire-exposure of Core types
The hosted API defines its own DTOs for uploaded reports, structurally similar to `CaseReport`/`FlagProofResult` but versioned independently. Alternative considered: serialize `ReleaseTwin.Core` types directly over the wire — rejected, because the hosted API is a public network contract that many customers' CLI versions will call against simultaneously; it must not break every time `PrerequisiteResult` or similar internals evolve (as they already have, twice, in this project's history). The CLI's upload step maps `CaseReport`/`FlagProofResult` into the stable ingest contract at the boundary.

### D2: Backend and dashboard stay in .NET
Backend: ASP.NET Core minimal API. Dashboard: server-rendered .NET (Razor Pages or Blazor Server), not a separate JS/TS frontend stack. Alternative considered: a modern JS framework for the dashboard — rejected for now given this is a single-person effort; introducing a second language/tooling chain for an MVP dashboard adds real maintenance cost without a demonstrated need for it. Revisit only if the dashboard's needs outgrow server-rendered pages (e.g. needs real-time updates, complex client state).

### D3: Two distinct auth mechanisms, not one
- **API tokens** (long-lived, per-project, bearer auth): what the CLI uses to upload reports. No human session involved.
- **Web session auth** (GitHub OAuth or email magic link): what a human uses to view the dashboard.

These are different trust domains and are kept structurally separate — an API token should never be usable to log into the dashboard UI, and a web session should never be embeddable in a CI pipeline.

### D4: Database is PostgreSQL
Relational, mainstream, well-supported alongside Stripe integrations, and JSON column support if report metadata needs flexible fields later. No exotic infra choice needed for what is fundamentally: organizations, projects, tokens, uploaded reports, subscriptions.

### D5: Billing — Stripe Checkout + Billing, metering uploaded case executions
Free tier: limited monthly uploaded runs, one project, short retention (exact numbers are hypotheses, same status as every other pricing hypothesis here — validate, don't assume). Paid tiers: more volume, longer retention, more seats. Metering unit is **uploaded case executions**, with a paired known-bad/known-good flag-proof run counted as one "release proof," deliberately never metering per-assertion, per-retry, or per-screenshot (creates an incentive to under-test). Plan enforcement is webhook-driven off Stripe subscription events, not built as a custom billing engine.

### D6: What crosses the wire is a trust feature, not just a privacy nicety
Only hashes, identifiers, pass/fail, and classification are ever uploaded — never fixture content, response bodies, or secrets. This isn't new work; it falls out of `CaseReport`'s existing shape from Phase 1. It should be stated plainly in the dashboard/marketing copy as a real trust advantage ("your test data never leaves your infrastructure"), not buried as an implementation detail.

### D7: Staged delivery — prove the loop before adding billing
Given this is the largest commitment in the project's history, and given the standing risk of building distribution before validating demand:

- **Stage 1** (this change's likely first implementation slice): `account-provisioning` + `ingest-api` + minimal `dashboard`, free-only, no billing at all. Goal: prove a real customer can self-serve from signup to seeing their own uploaded results, with zero sales conversation. This is also the thing the landing page's "get in touch" can eventually be replaced with, or supplemented by, a "try it yourself" flow.
- **Stage 2**: `billing`, once Stage 1 has real (even if small) self-serve usage to monetize. Building Stripe integration before anyone has self-served past signup would be solving a problem that doesn't exist yet.

This staging is a recommendation, not yet a locked task boundary — tasks.md, when written, should reflect it explicitly so Stage 2 isn't silently bundled into "finish everything."

## Risks / Trade-offs

- **[Risk] A multi-tenant hosted service is real, ongoing operational and security responsibility for a single-person project** (auth bugs, tenant data isolation, uptime). → Mitigation: Stage 1 scope stays minimal; use managed infra (managed Postgres, a managed auth/OAuth provider) rather than self-hosting security-critical pieces; the "nothing sensitive is ever uploaded" design (D6) shrinks the blast radius of most plausible bugs.
- **[Risk] Billing/tax compliance (international tax, invoicing) is genuine complexity.** → Mitigation: deferred to Stage 2, and handled through Stripe's own tax/invoicing features rather than custom-built.
- **[Risk] Building any of this before a design partner has confirmed the value proposition risks the classic "build distribution before validating demand" mistake.** → Mitigation: Stage 1 is intentionally the smallest thing that proves the self-serve loop works — not a commercial launch, not a marketing push, just the free path becoming real instead of aspirational.

## Open Questions

- Hosting provider (Azure vs. AWS vs. a simpler PaaS like Fly.io/Render) — operational choice, doesn't change this design; resolve when Stage 1 implementation starts.
- Email provider for magic-link auth (if chosen over/alongside GitHub OAuth) — same, deferrable.
- Exact free-tier numbers (run limit, retention days) — hypotheses to test, not architectural decisions; can change without touching the design.
