# Billing operator note (Polar)

How the hosted platform's billing works, for whoever operates it. Implemented in
`billing-integration`; code lives under `hosted/ReleaseTwin.Hosted.Api/Billing/`.

## Model

Polar is the **Merchant of Record**. It owns the checkout, the customer portal, invoices, sales
tax / VAT remittance, card-failure retries, and discounts. The hosted platform never sees card or
billing-address data and derives no charged amount from its own catalog — it trusts what Polar
reports.

- **One Polar product = the Team tier**, with two prices: monthly (default) and annual (`save 17%`).
- **Per-project pricing**: the subscription *quantity* tracks the org's live project count.
- **Enterprise stays operator-set** via `PUT /api/admin/organizations/{id}/tier` — it never
  touches Polar and has no subscription.

## Configuration

All under the `Polar` config section (env vars use `__` for `:`), wired in
`hosted/terraform/billing.tf` + `lambda.tf` and passed by `deploy-hosted.yml`:

| Key | Source | Notes |
|---|---|---|
| `Polar__ApiToken` | `POLAR_API_TOKEN` secret | organization access token |
| `Polar__WebhookSecret` | `POLAR_WEBHOOK_SECRET` secret | Standard Webhooks signing secret |
| `Polar__ApiBaseUrl` | `POLAR_API_BASE_URL` var | `https://sandbox-api.polar.sh` for the sandbox |
| `Polar__PriceIds__Team__Monthly` | `POLAR_TEAM_PRICE_MONTHLY` var | |
| `Polar__PriceIds__Team__Annual` | `POLAR_TEAM_PRICE_ANNUAL` var | |
| `Polar__CheckoutSuccessUrl` / `CheckoutCancelUrl` / `PortalReturnUrl` | repo vars | dashboard URLs |
| `Polar__ReconciliationDryRun` | `POLAR_RECONCILIATION_DRY_RUN` var | `true` until one clean nightly cycle |
| `Polar__UpgradeEnabled` | `POLAR_UPGRADE_ENABLED` var | `false` until a sandbox checkout is verified — see below |

`IsConfigured` (webhook live) needs `ApiToken` + `WebhookSecret` + at least one price id.
`IsUpgradeEnabled` (customer-facing upgrade / portal buttons) additionally needs
`Polar__UpgradeEnabled=true` — so you can register the webhook with Polar and let events flow
while the dashboard button stays hidden, then flip the button on once a real checkout has been
verified end to end.

**Empty `ApiToken` / `WebhookSecret` / price ids ⇒ `PolarOptions.IsConfigured` is false**: the
webhook returns 503, the upgrade button degrades gracefully, and no billing calls are made. This
is the safe default for any environment without Polar.

Standing manual steps with no code path (do these in the Polar dashboard):
create the organization, the product, and the two prices; register the webhook URL
(`<function-url>/api/billing/webhook`) and copy its signing secret; create promo codes and
founding-customer price locks directly in Polar.

## Subscription → tier / quantity mapping

The webhook (`POST /api/billing/webhook`, unauthenticated, signature-gated) is the **only** writer
of billing-driven tier and status changes. `BillingEventProcessor` maps events:

| Polar event / status | Effect |
|---|---|
| `subscription.active` / `created` / `uncanceled`, `order.paid`, or `updated`→`active` | tier → **Team**, billing status → **Active**, store customer id + subscription id + cadence |
| `subscription.past_due`, or `updated`→`past_due` | billing status → **PastDue**, `since` = event timestamp (tier untouched) |
| `subscription.canceled` / `revoked`, or `updated`→`canceled`/`unpaid` | billing status → **Canceled** (tier untouched) |
| anything else | recorded, no-op |

Idempotency: each delivery is deduped by its `webhook-id` via a `ProcessedBillingEvent` item
(single-table `EVENT#<id>`, DynamoDB TTL ~30d). A duplicate is a 200 no-op; a delivery that fails
before the record step returns non-2xx and Polar redelivers (all state writes are "set to state X",
so a partial retry is safe).

Quantity: `ProvisioningService.CreateProjectAsync` raises the Polar quantity **before** creating a
project and fails the creation closed if Polar rejects the increase (declined proration charge);
`DeleteProjectAsync` lowers it best-effort and never blocks the delete.

## Grace-window policy

Entitlements are `tier ∧ billing status` (`EntitlementService.For(Organization)`):

- **Active** → full tier entitlements.
- **PastDue** → full tier entitlements for **14 days** from `BillingStatusSince` (the event
  timestamp), then Free entitlements. Polar's own retry schedule (~7 days) fits inside the window,
  so a recoverable card failure never actually degrades anything. Recovery to Active restores full
  access immediately, no re-provisioning.
- **Canceled** → Free entitlements immediately.

The stored tier is never downgraded — only effective entitlements change — so a recovery event
restores everything by flipping the status back.

When an org's effective project cap drops below its project count, the **oldest projects up to the
cap stay writable** and the rest become read-only (`ProjectWritabilityService`): still listed on
the dashboard with all their evidence, but evidence ingest returns `entitlement-required`. No
project is ever deleted or hidden. Re-upgrading or deleting projects restores the rest.

## Reconciling manually

The nightly `BillingReconciliation` Lambda (`RELEASETWIN_LAMBDA_TASK=BillingReconciliation`,
`hosted/terraform/billing.tf`) is the backstop for a missed webhook or a delete-time failure. For
every org with a `PolarSubscriptionId` it compares the Polar quantity to the actual project count
and, unless `ReconciliationDryRun` is true, sets Polar to the actual count. Every correction is
logged (`billing_reconciliation_correction org=… polar_quantity=… actual=…`).

To reconcile one org by hand: count its projects, then in the Polar dashboard set the
subscription's quantity to match. To force a tier change out of band (e.g. a refund dispute), use
the operator admin tier endpoint — but note the webhook will overwrite billing status on the next
Polar event, so fix the underlying subscription in Polar too.

## Rollout / rollback

Per `openspec/changes/billing-integration/design.md` Migration Plan: deploy fields + catalog →
set the Polar secrets + price ids and register the webhook in Polar (`IsConfigured` — webhook
live, `POLAR_UPGRADE_ENABLED` still `false`) → reconciliation runs in dry-run →
**`POLAR_UPGRADE_ENABLED=true`** after a sandbox checkout passes end to end → flip
`POLAR_RECONCILIATION_DRY_RUN=false` after one clean nightly cycle.

Rollback = set `POLAR_UPGRADE_ENABLED=false` (buttons vanish, webhook stays live and idempotent),
or clear the `Polar__*` config entirely (webhook then returns 503). Orgs already on Team keep
their tier either way.

**Step-by-step sandbox walkthrough: `docs/billing-sandbox-runbook.md`.**
