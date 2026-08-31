## Why

The hosted platform can move an organization to Team or Enterprise (`ProvisioningService.SetTierAsync`), but there is no way to collect payment: `UpgradeToTeamAsync` is an explicit payment-free placeholder, and paid customers today would be invoiced by hand and flipped with the operator admin endpoint. To open the self-serve funnel to paying customers we need a real billing flow. It is sequenced now (Phase F of the self-serve funnel plan) because the first design-partner conversations are imminent and the manual path does not scale past one or two accounts.

We use a **Merchant of Record (Polar)** rather than raw Stripe: Polar is Stripe-backed, remits sales tax / VAT globally, and absorbs chargebacks and card-failure dunning — removing the entire tax-compliance and payment-retry burden from a solo operator for roughly 1–2% of revenue over raw Stripe once Stripe Tax + Stripe Billing are added in.

## What Changes

- **New `billing` capability**: a Polar checkout link from the dashboard for the Free → Team upgrade; a signed, idempotent `POST /api/billing/webhook` that maps Polar subscription lifecycle events to a tier + billing status and calls the existing `SetTierAsync` seam; a nightly reconciliation job (following the `EvidencePurge` scheduled-Lambda pattern) that corrects drift between Polar's subscription quantity and the org's actual project count.
- **BREAKING (internal contract, not yet public): plan catalog gains per-cadence pricing.** `hosted/plans.json` `price` changes from a single `{ amount, unit, placeholder }` to a list of `{ interval, amount, unit, placeholder }` where `interval` is a closed vocabulary (`monthly`, `annual`). `validateCatalog()` in `web/src/lib/plans.ts`, `PlanCatalog.cs`, both shape tests, and every pricing/features surface update. Team defaults to monthly (~$59/project/mo) with an annual option (~$49/project/mo, "save 17%"); Enterprise likewise.
- Discounts, promo codes, and founding-customer price locks are handled entirely in Polar — no discount engine, checkout param, or coupon table on our side. The webhook trusts the amount Polar reports, never the catalog.
- **Modified `plan-tier-gating`**: an organization gains a **billing status** (`active`, `past_due`, `canceled`) distinct from its tier. Entitlements degrade on `past_due` / `canceled` even while the tier still reads Team, per a grace policy. When an org's tier drops below its project count (downgrade / cancellation), the excess projects become **read-only** — visible on the dashboard, evidence ingest blocked, with a banner — rather than deleted or hidden.
- **Modified `plan-tier-gating`**: project creation and deletion **synchronise the Polar subscription quantity** (per-project pricing). A quantity increase that Polar rejects (declined card, API error) **fails the project creation closed** with a message pointing to the Polar customer portal.
- **Modified `account-provisioning`**: `Organization` gains `BillingStatus`, `BillingCadence`, `PolarCustomerId`, `PolarSubscriptionId`. The customer portal is a redirect to Polar's hosted portal — no portal UI is built.
- Enterprise stays operator-set via the existing admin endpoint — deliberately not self-serve.

## Capabilities

### New Capabilities
- `billing`: Polar (Merchant of Record) integration — upgrade checkout, the signed idempotent subscription-lifecycle webhook, subscription-quantity synchronisation for per-project pricing, billing status and its grace policy, and the nightly reconciliation job. Owns everything between the payment provider and the `SetTierAsync` / entitlement seams.

### Modified Capabilities
- `plan-tier-gating`: adds billing status as a second axis alongside tier (grace degradation on `past_due` / `canceled`); adds read-only enforcement for projects in excess of the current tier's limit after a downgrade; makes project create/delete drive Polar subscription quantity and fail closed when the quantity increase is rejected. Restates the self-serve Free → Team requirement as a real paid checkout instead of the payment-free placeholder.
- `plan-catalog`: price metadata becomes per-cadence (`monthly` / `annual`) instead of a single amount; `GET /plans` and the catalog validation reflect the new shape.
- `account-provisioning`: `Organization` carries Polar customer/subscription identifiers, billing status, and billing cadence.

## Impact

- **Code**: new `hosted/ReleaseTwin.Hosted.Api/Billing/` (webhook endpoint, Polar client, event dedupe store, reconciliation job); `ProvisioningService` (quantity sync in `CreateProjectAsync` + project delete, billing-status-aware paths); `EntitlementService` (billing-status degradation, read-only-project decision); `OrganizationRepository` + `Organization` entity (new attributes, single-table); `PlanCatalog.cs` + `web/src/lib/plans.ts` + shape tests + `hosted/plans.json` (per-cadence price); dashboard upgrade UI + pricing/features pages (`web/`).
- **New DynamoDB item**: `ProcessedBillingEvent` (PK `EVENT#<polarEventId>`, TTL ~30d) for webhook idempotency — single-table, overloaded.
- **External dependency**: Polar org, product(s) with monthly + annual prices, API token, webhook signing secret. New scheduled job in the deploy stack.
- **Secrets / config** (credential-preflight in design.md): `POLAR_API_TOKEN` + `POLAR_WEBHOOK_SECRET` in SSM; `POLAR_PRODUCT_ID` / price identifiers as repo vars; webhook URL registered in the Polar dashboard (standing manual step — no code path to create it).
- **Docs**: `docs/company-ops.md` / continuity docs already assume a MoR; pricing page copy (`web/`) updates for dual cadence.
- **Not affected**: the AGPL CLI and Apache-2.0 GitHub Action stay payment-agnostic — entitlement enforcement is entirely server-side.
