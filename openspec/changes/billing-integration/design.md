## Context

See `proposal.md` for motivation. Relevant current state:

- `ProvisioningService.SetTierAsync(orgId, tier)` is the single tier-mutation point; `UpgradeToTeamAsync` currently sets Team with no payment. `EntitlementService.For(org)` is the single entitlement-resolution point — every gate routes through it.
- `hosted/plans.json` is embedded in the API and imported by `web/src/lib/plans.ts`; a shape test on each side fails the build on drift. `price` is one `{ amount, unit, placeholder }`.
- `Organization` is a single DynamoDB item; `OrganizationRepository` hand-maps attributes. `PlanTier` is stored as a string with `ParsePlanTier` read-repair.
- `UsageCounter` and the `EvidencePurge` Lambda establish the patterns for per-org counters and scheduled jobs (Terraform-in-CI, OIDC, split-layer).
- Auth is a Clerk JWT resolved to an `Organization`; `AdminOperators` allowlist gates operator-only endpoints.
- `CreateProjectAsync` already reads the org + (when capped) the project list before creating.

## Goals / Non-Goals

**Goals:**

- A customer upgrades Free → Team by paying through Polar's hosted checkout; the org is on Team within seconds of payment, driven only by the webhook.
- Per-project pricing: Polar subscription quantity tracks the org's live project count, kept correct by synchronous updates plus a nightly reconciliation backstop.
- Card failure / cancellation degrades entitlements on a defined grace schedule without data loss; projects in excess of a downgraded tier become read-only, never deleted.
- The webhook is safe to receive any event any number of times, in any order Polar chooses to retry.
- No sales-tax, VAT, invoicing, or dunning code on our side.

**Non-Goals:**

- Usage/metered billing (billing on case-report volume) — tiers stay flat per project.
- Self-serve Enterprise — stays operator-set via the existing admin endpoint.
- In-app invoice history, receipts, or a billing-details UI — all delegated to Polar's hosted customer portal.
- Proactive dunning email from us — Polar owns customer payment-failure email.
- Multi-currency display on the marketing site (Polar handles buyer-side currency at checkout).
- A discount / coupon / price-lock engine — Polar owns discounts natively (D9).
- Migrating existing hand-invoiced/operator-set orgs into Polar — they keep their tier; billing status defaults to `active` with no Polar IDs and is treated as "externally managed" (reconciliation and quantity sync skip orgs with no `PolarSubscriptionId`).

## Decisions

### D1: Merchant of Record (Polar), not raw Stripe

Polar is the seller of record, remits tax/VAT globally, and absorbs chargebacks and card-failure retries. Fee ≈ 4% + 40¢; on a 3-project Team org (~$147/mo) that is ~$6.30/mo.

- **Alternative — raw Stripe + Stripe Tax + Stripe Billing**: effective rate lands ~4.2–4.5% once Tax (0.5%) and Billing (0.5–0.8%) are added, *plus* we become responsible for sales-tax nexus registration, filing, audit risk, and chargeback disputes. Bad trade for a solo operator. Rejected.
- **Alternative — Lemon Squeezy / Paddle**: equivalent MoR model, ~5% + 50¢. Lemon Squeezy is the fallback if Polar proves immature; the integration seam (webhook → `SetTierAsync`) is provider-shaped, not Polar-specific, so switching later is contained to `Billing/`.

### D2: The webhook is the only writer of billing-driven tier changes

`checkout`/dashboard code never sets the tier directly — it creates a Polar checkout session and redirects. Tier and billing status move only when the webhook processes a Polar event. This keeps one causal path and makes the flow correct even if the user closes the browser before the redirect back.

- **Alternative — set tier optimistically on redirect-back, reconcile via webhook**: two writers, redirect-back is unreliable (mobile, closed tab), and a failed payment that still redirects would wrongly grant Team. Rejected.

### D3: Idempotency via a `ProcessedBillingEvent` item, processed synchronously before 200

On receipt: verify signature → check `EVENT#<polarEventId>` exists (return 200, no-op if so) → process (map event, `SetTierAsync`, quantity/status writes) → write `ProcessedBillingEvent` with ~30d TTL → return 200. Any failure before the final write returns non-2xx and Polar retries with backoff (~3 days). Processing is idempotent by construction (all writes are "set to state X", not "increment"), so a retry that partially succeeded last time is safe.

- **Alternative — 200 immediately, enqueue for async processing**: adds SQS + a consumer + our own DLQ/retry for a webhook volume measured in single digits per day. Synchronous is simpler and Polar's retry *is* our retry. Revisit only if processing work grows. Rejected for Phase 1.
- Dedupe store is a plain single-table item (PK `EVENT#<id>`, SK `EVENT#<id>`), TTL attribute for expiry. 30d comfortably exceeds Polar's retry window.

### D4: Billing status is a second axis; entitlements are `tier ∧ status`

`Organization.BillingStatus ∈ { active, past_due, canceled }`, independent of `PlanTier`. `EntitlementService.For(org)` starts from the tier's catalog entitlements, then applies a **status modifier**:

| Status | Effect on a Team/Enterprise org |
|---|---|
| `active` | full tier entitlements |
| `past_due` | full entitlements for a **14-day grace window** (from the event timestamp), then treated as `canceled` |
| `canceled` | entitlements drop to **Free**; projects beyond Free's `maxProjects` become read-only (D5) |

Grace window end is computed from a `BillingStatusSince` timestamp, not a scheduled job — the entitlement resolver checks `now > since + 14d`. Polar's own retry schedule (~7 days) fits inside the window, so a recoverable card failure never actually degrades anything.

- **Alternative — collapse status into tier (`past_due` → immediately Free)**: punishes a customer for a transient card expiry mid-month; contradicts the "cancel anytime, keep what you paid for" posture. Rejected.

### D5: Downgrade with excess projects → read-only, enforced in the entitlement layer

When `projectCount > tier.maxProjects` (only reachable after a downgrade/cancel, since `CreateProjectAsync` blocks it going up), the **oldest N projects up to the limit stay writable**; the rest are read-only: dashboard-visible, evidence ingest returns `entitlement-required`, no new cases accepted. No project is deleted or hidden — the evidence is the product. A banner names the state and links to the portal / upgrade.

- Ordering by creation time (oldest-writable) is deterministic and stable across reconciliation runs. The org can re-upgrade to restore all projects, or delete projects to get under the limit.
- **Alternative — block the whole org (all projects read-only) on downgrade**: simpler rule, worse experience, and punishes Free-tier-legitimate usage. Rejected.
- **Alternative — let the customer pick which projects stay active**: a UI and a stored selection for an edge case that resolves itself on re-upgrade or delete. Deferred (Open Question 1).

### D6: Per-project quantity — synchronous sync + nightly reconciliation

`CreateProjectAsync`: after the entitlement check, if the org has a `PolarSubscriptionId`, call Polar to set quantity = `currentProjectCount + 1`. On success, create the project. On Polar rejection (declined card for the proration charge, API error), **abort the creation** and surface a message pointing at the Polar portal. Project delete: set quantity = `currentProjectCount - 1` (best-effort; a failure here is logged, not fatal — never block a delete on billing, and reconciliation will catch it).

The **nightly reconciliation job** (scheduled Lambda, `EvidencePurge` pattern) lists orgs with a `PolarSubscriptionId`, compares Polar quantity to actual project count, and corrects Polar to match actual. It also re-evaluates read-only state. This is the backstop for a missed webhook, a delete-time failure, or a race.

- **Alternative — nightly-only (bill in arrears, no synchronous coupling)**: `CreateProjectAsync` stays payment-free but a customer can provision 50 projects and be billed later. Acceptable for a tiny cohort but gives away revenue and diverges from "sync-on-change" as chosen. Rejected as the primary mechanism; the nightly job is kept purely as reconciliation.
- **Alternative — Polar usage/metered billing**: report project-count as usage, let Polar prorate. Cleaner in theory but couples us to Polar's metering model and complicates the flat-per-project story on the pricing page. Deferred with the broader metered-billing non-goal.

### D7: Per-cadence catalog price — dynamic shape, closed vocabulary

`plans.json` `price` changes from a single object to a **list** of `{ interval, amount, unit, placeholder }`. `interval` is validated at load against a fixed vocabulary (`monthly`, `annual`; extensible to `quarterly` etc. without a schema change). Rationale: monthly (~$59, default) is the low-friction entry; annual (~$49, "save 17%") is the committed, price-lockable option for design partners. Monthly is the default cadence in the upgrade flow because mid-cycle proration on a monthly plan is small (~$30 for a project added mid-month) versus a startling multi-hundred-dollar charge on an annual plan.

- The list is dynamic (adding a cadence = a `plans.json` entry + a Polar price, no code) but the `interval` vocabulary is closed — the webhook must map each interval to an `Organization.BillingCadence` value, so an unknown interval fails catalog validation rather than flowing through as an opaque string.
- Each cadence is a distinct Polar price on one product; the webhook payload carries which price was purchased → stored as `Organization.BillingCadence` for display ("renews monthly" / "renews 12 Jan 2027"). Tier mapping ignores cadence (both → Team).
- **Alternative — annual only** (as the marketing copy over-emphasised): loses every customer unwilling to commit 12 months to an unproven solo product at first contact. Rejected.
- **Alternative — fully open-ended interval string**: moves the coupling into code (the webhook mapping) instead of removing it, and defeats the build-time shape check. Rejected.
- Build-time shape checks on both `plans.json` consumers are updated together in one task so the catalog can never half-migrate.

### D9: Discounts and price locks live entirely in Polar

We build no discount engine. Promo/launch codes are created in the Polar dashboard and entered at Polar's hosted checkout. Founding-customer price locks are a per-subscription custom price or permanent discount in Polar. The webhook reads the **actual** charged amount and quantity from the Polar payload and never derives what was charged from the catalog, so discounted and locked-in customers need no special handling.

- **Code implication, minor**: the dashboard's plan line must not assert a catalog price to a customer who may be discounted — it shows the tier and cadence and links to the Polar portal for the authoritative amount, rather than rendering `~$49/project/mo`.
- **Alternative — our own coupon table + checkout param**: rebuilds a solved MoR feature, adds a PII/abuse surface. Rejected.

### D8: Customer portal = redirect to Polar

The dashboard "Manage billing" action creates a Polar customer-portal session and redirects. No payment-method UI, no invoice list, no address form on our side — all PCI / PII surface stays with the MoR.

## Risks / Trade-offs

- **Synchronous webhook processing blocks the response on our DynamoDB writes** → writes are 2–3 single-item puts; if latency ever matters, D3's async alternative is a contained change behind the same endpoint.
- **A project created in the gap between a card failing and the webhook arriving** gets a quantity bump Polar may reject → `CreateProjectAsync` fails closed with a clear message; the customer fixes the card and retries. Acceptable.
- **Polar immaturity** (younger than Stripe/Paddle) → the `Billing/` seam is provider-shaped; Lemon Squeezy is a pre-vetted fallback. Do not leak Polar types past `Billing/`.
- **Grace window abuse** (let card fail, keep Team 14 days, re-add card, repeat) → low value to the abuser, visible in Polar, and a manual operator downgrade is always available. Not worth engineering against in Phase 1.
- **Clock skew on grace-window math** → compute from Polar's event timestamp, not local `now`, for the `since` value; only the expiry check uses local `now` (a few minutes' skew on a 14-day window is irrelevant).
- **Reconciliation job corrects Polar to match our project count** — if our data is wrong (orphaned project rows), it would mis-bill → the job logs every correction and can run in dry-run mode first.
- **Existing hand-invoiced orgs have no Polar IDs** → explicitly excluded from quantity sync and reconciliation; billing status `active`. Documented, not migrated.

## Migration Plan

1. Add the new `Organization` attributes with safe defaults (`BillingStatus=active`, `BillingCadence=null`, Polar IDs `null`). `OrganizationRepository` read path tolerates their absence (same pattern as `ParsePlanTier`). No backfill.
2. Ship the per-cadence `plans.json` + both shape-check updates + `PlanCatalog.cs` + `web/` consumers in one deploy — the build fails if any half is missed.
3. Deploy the webhook endpoint (unauthenticated route, signature-gated) and register the URL + secret in Polar. Endpoint is live but no org has a subscription yet — no-op.
4. Deploy the reconciliation Lambda (dry-run mode initially).
5. Enable the dashboard upgrade button once a real Polar checkout has been tested end-to-end against Polar's sandbox. Implemented as `Polar__UpgradeEnabled` (`POLAR_UPGRADE_ENABLED` repo var, default `false`): `PolarOptions.IsConfigured` gates the webhook (so Polar can be registered and events flow at step 3), and `IsUpgradeEnabled = IsConfigured && UpgradeEnabled` additionally gates the `/api/dashboard/upgrade` + `/billing-portal` endpoints and the dashboard button. Walkthrough: `docs/billing-sandbox-runbook.md`.
6. Flip reconciliation out of dry-run after one clean nightly cycle.

**Rollback**: disable the upgrade button (config); the webhook endpoint can stay (idempotent, no-op without subscriptions). Orgs already on Team via Polar keep their tier — `SetTierAsync` state persists independently. Reconciliation Lambda can be disabled via its schedule.

## Open Questions

1. **Should the customer choose which projects stay writable after a downgrade**, rather than oldest-first (D5)? Deferrable — oldest-first is a complete, deterministic rule; a selection UI is additive and doesn't change the webhook, catalog, or entitlement-axis design.
2. **Exact placeholder prices** ($59/$49 monthly/annual for Team; Enterprise cadence pricing). Set from the first cohort; `placeholder: true` already signals this and changing the numbers is a `plans.json` edit only.
3. **Annual cancel semantics** — end-of-period (no refund) vs prorated refund. Leaning end-of-period ("cancel anytime" = stop renewing); Polar supports both and it's a checkout/portal config, not a code decision.
