## Why

The user has decided the onboarding model is: execution stays in the customer's own infra (CLI, local or CI — matching docs/installation-model.md's existing "CI runner is the right first default" reasoning), but results surface on a hosted dashboard, and the whole path from stranger to paying customer must work with zero sales conversations. Nothing hosted exists yet — no accounts, no server, no database, no billing.

design.md captured the whole shape, including monetization, and proposed staging the build in two parts (D7). The user chose to split now: **this change scopes only Stage 1** — prove a real customer can self-serve from signup to seeing their own uploaded results, free-only, with zero sales conversation. Billing (Stage 2) is deliberately excluded here and will be its own change once Stage 1 has real self-serve usage to monetize — building Stripe integration before anyone has self-served past signup would be solving a problem that doesn't exist yet.

This is still, even scoped to Stage 1 alone, the largest change in the project's history: every prior change (Phases 1-4) was local library/CLI work. This one introduces a real hosted service and an account model.

## What Changes

- **Self-serve account provisioning**: a customer signs up (email or GitHub OAuth), creates an organization/project, and receives an API token — no human interaction required.
- **Ingest API**: an authenticated hosted endpoint that accepts uploaded `CaseReport` and `FlagProofResult` data from the CLI, via a stable ingest contract decoupled from `ReleaseTwin.Core`'s internal types (design.md D1). Only report metadata is ever uploaded — case ID, oracle reference, fixture *hash* (never content), pass/fail, classification, cleanup status, timing. No fixture content, response bodies, or secrets cross the wire; this was already true of the existing report shape from Phase 1, not a new privacy feature.
- **CLI upload capability**: the CLI, after executing cases locally/in CI, optionally uploads results to the ingest API using a token supplied via environment variable — an extension of `cli-runner`, not a new execution mode. The CLI remains fully usable with no token configured.
- **Hosted dashboard**: a minimal web UI showing run history, pass/fail trends, and flag-proof outcomes over time, scoped to the signed-in customer's organization.

**Explicitly excluded from this change** (deferred to a future change, per design.md D7): billing, Stripe integration, paid tiers, usage enforcement. Stage 1 is free-only — there is no plan to enforce yet.

## Capabilities

### New Capabilities
- `account-provisioning`: self-serve signup, organization/project creation, API token issuance and revocation.
- `ingest-api`: authenticated hosted endpoint accepting uploaded case/flag-proof reports, scoped to an organization.
- `dashboard`: hosted web UI for viewing uploaded run history and trends.

### Modified Capabilities
- `cli-runner`: gains an optional upload step after case execution, authenticated via an API token environment variable.

## Impact

- New hosted service: a backend API, a database, and a web dashboard — none of which exist today. No billing/payment infrastructure in this change.
- `ReleaseTwin.Cli` gains an outbound upload dependency (optional — the CLI must remain fully usable with no token configured, per the existing "runs on their infra" design).
- No impact to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, or any adapter — this is purely additive at the CLI/hosted boundary.
- Introduces real operational responsibilities that didn't exist before: uptime, data retention/deletion, security posture for a multi-tenant service. Payment processing is explicitly deferred, which meaningfully reduces this change's compliance surface (no tax/invoicing/PCI-adjacent concerns yet).
