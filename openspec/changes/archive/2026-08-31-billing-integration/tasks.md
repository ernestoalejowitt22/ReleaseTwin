# Tasks

## 1. Credential / config preflight (do first, per CLAUDE.md)

- [x] 1.1 Enumerate everything Polar needs and how to verify each: Polar org + product with monthly & annual prices; `POLAR_API_TOKEN` and `POLAR_WEBHOOK_SECRET` (SSM); price/product identifiers as repo vars (`POLAR_TEAM_PRODUCT_ID`, `POLAR_TEAM_PRICE_MONTHLY`, `POLAR_TEAM_PRICE_ANNUAL`, Enterprise equivalents if used); webhook URL registered in the Polar dashboard. Write the list into `openspec/changes/billing-integration/preflight.md` with a one-line verify command per item and flag the standing manual steps (Polar dashboard actions have no code path).
- [x] 1.2 Add the SSM params + repo vars to the Terraform split-layer config and the deploy workflow env, following the `CLERK_DOMAIN` / `ADMIN_OPERATOR_USER_IDS` pattern. Empty/absent config = billing surface closed (safe default).

## 2. Per-cadence plan catalog (ship as one unit)

- [x] 2.1 `hosted/plans.json`: change each tier's `price` from a single object to a list of `{ interval, amount, unit, placeholder }`; Team = `monthly` (~59) + `annual` (~49), Enterprise = same shape, Free = single `monthly` 0. Keep `placeholder: true` on paid amounts.
- [x] 2.2 `hosted/ReleaseTwin.Hosted.Api/Plans/PlanCatalog.cs`: parse the price list, validate `interval` against a closed enum (`Monthly`, `Annual`), fail startup on an unknown interval or empty list. Update `PlanTierDefinition` / price types.
- [x] 2.3 `web/src/lib/plans.ts`: update `PlanPrice` type + `validateCatalog()` for the list shape and interval vocabulary; update `formatPrice()` and callers.
- [x] 2.4 Update both shape-check tests (`PlanCatalogTests.cs` and the web-side catalog test) for the new shape; a half-migrated catalog must fail the build.
- [x] 2.5 Update pricing + features pages (`web/src/app/(marketing)/pricing/page.tsx`, `features/page.tsx`, homepage feature section) to render monthly/annual with a "save 17%" annual toggle; monthly shown by default.
- [x] 2.6 `npm run build` + `npx eslint` (web), `dotnet build` + `dotnet test` (hosted) green.

## 3. Organization billing fields

- [x] 3.1 `Organization` entity: add `BillingStatus` (`Active` | `PastDue` | `Canceled`), `BillingStatusSince` (DateTimeOffset), `BillingCadence` (`Monthly` | `Annual` | null), `PolarCustomerId` (string?), `PolarSubscriptionId` (string?).
- [x] 3.2 `OrganizationRepository`: map the new attributes on write; tolerate their absence on read with defaults (`Active`, `since = CreatedAt`, nulls) — same read-repair discipline as `ParsePlanTier`. No backfill.
- [x] 3.3 `SetBillingAsync(orgId, status, since, cadence, customerId, subscriptionId)` repository method (single write). Tests for round-trip + legacy-row read.

## 4. Entitlement resolution: tier ∧ billing status

- [x] 4.1 `EntitlementService.For(org)`: after resolving tier entitlements, apply the status modifier — `PastDue` within 14d of `BillingStatusSince` = full; past 14d or `Canceled` = Free entitlements. Compute expiry against `DateTimeOffset.UtcNow`; `since` comes from the event.
- [x] 4.2 `EntitlementServiceTests`: grace-window boundary cases (just inside, just outside), recovery to `Active`, `Canceled` immediate drop, Free/operator-Enterprise unaffected.

## 5. Read-only projects after downgrade

- [x] 5.1 Add a way to resolve, for an org, which projects are writable vs read-only: oldest-by-creation up to `tier.maxProjects` are writable, the rest read-only. `maxProjects == null` ⇒ all writable. Centralise this so ingest and dashboard agree.
- [x] 5.2 Ingest path: a read-only project rejects new case/flag-proof reports with `entitlement-required` (reuse `EntitlementRequiredException` + error code), not a generic error.
- [x] 5.3 Dashboard view + `web/` types: mark read-only projects, surface a banner naming the state with an upgrade / portal link. Read-only projects stay listed with their evidence.
- [x] 5.4 Tests: 3-projects-on-Team → downgrade to Free → oldest writable, other two reject ingest, all three visible; re-upgrade restores all.

## 6. Polar client (provider-shaped, no Polar types leak past `Billing/`)

- [x] 6.1 `hosted/ReleaseTwin.Hosted.Api/Billing/`: `IPolarClient` with `CreateCheckoutSession(orgId, tier, cadence)`, `CreatePortalSession(customerId)`, `SetSubscriptionQuantity(subscriptionId, quantity)`. Concrete HTTP impl + typed options bound from config.
- [x] 6.2 Fake `IPolarClient` for tests (records calls, scriptable failures).

## 7. Subscription webhook

- [x] 7.1 `POST /api/billing/webhook` — unauthenticated route, signature-verified against `POLAR_WEBHOOK_SECRET`; invalid/missing signature ⇒ reject, no state change.
- [x] 7.2 `ProcessedBillingEvent` single-table item (PK/SK `EVENT#<id>`, TTL ~30d). Check-before-process; duplicate delivery ⇒ 200 no-op.
- [x] 7.3 Event → intent mapping: subscription active/paid ⇒ tier Team + status Active + store customer/subscription id + cadence; past_due ⇒ status PastDue with `since` from event; canceled/unpaid ⇒ status Canceled. Unknown event types ⇒ 200 no-op (still recorded).
- [x] 7.4 Processing order: apply state via `SetTierAsync` + `SetBillingAsync`, then write `ProcessedBillingEvent`, then 200. Any failure before the final write ⇒ non-2xx, not recorded, safe to redeliver (all writes are idempotent "set state X").
- [x] 7.5 Tests: activation, past_due, cancel, recovery, duplicate delivery, bad signature, unknown event, failure-then-redelivery.

## 8. Upgrade + portal flow (dashboard)

- [x] 8.1 `POST /api/dashboard/upgrade` (replace the payment-free `UpgradeToTeamAsync` path): takes a cadence, calls `IPolarClient.CreateCheckoutSession`, returns the checkout URL. Tier is NOT changed here.
- [x] 8.2 `POST /api/dashboard/billing-portal`: returns a Polar portal URL for the org's `PolarCustomerId`; 400 if unlinked.
- [x] 8.3 `web/` dashboard: upgrade button → cadence choice → redirect to checkout URL; "Manage billing" → portal redirect. For a paying org, show tier + cadence + portal link instead of a catalog price.
- [x] 8.4 Keep `PUT /api/admin/organizations/{id}/tier` (operator Enterprise) untouched; confirm it sets no Polar linkage.
- [x] 8.5 Cypress/e2e or endpoint tests for the upgrade + portal endpoints (checkout URL returned, tier unchanged until webhook).

## 9. Quantity sync on project create/delete

- [x] 9.1 `ProvisioningService.CreateProjectAsync`: after the entitlement check, if `PolarSubscriptionId` is set, call `SetSubscriptionQuantity(current + 1)` BEFORE creating; on failure throw an `EntitlementRequiredException`-style error naming the payment problem + portal link, create nothing.
- [x] 9.2 Project delete path: best-effort `SetSubscriptionQuantity(current - 1)`; log + swallow failures, never block the delete.
- [x] 9.3 Orgs with no `PolarSubscriptionId` skip all billing calls.
- [x] 9.4 Tests: paid org create bumps-then-creates; Polar rejection ⇒ no project + clear error; delete lowers quantity; delete with Polar failure still deletes; operator-Enterprise org untouched.

## 10. Reconciliation job

- [x] 10.1 Scheduled Lambda (`EvidencePurge` pattern, Terraform-in-CI): for each org with `PolarSubscriptionId`, compare Polar quantity to actual project count, set Polar to actual, log every correction. Re-evaluate read-only project state.
- [x] 10.2 Dry-run mode (log intended corrections, make no calls) controlled by config; ship enabled in dry-run.
- [x] 10.3 Tests: drift corrected toward actual; unlinked orgs skipped; dry-run makes no calls.
- [x] 10.4 Add the schedule to the deploy stack.

## 11. Docs

- [x] 11.1 Pricing page copy for dual cadence; `docs/` billing/continuity references updated to name Polar as the MoR.
- [x] 11.2 `docs/` operator note: how a subscription maps to tier/quantity, how to reconcile manually, the grace-window policy.

## 12. Verification & rollout

- [x] 12.1 Full suite: `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln`; `npm run build` + `npx eslint`; `openspec validate billing-integration --strict`. Report actual test counts.
- [ ] 12.2 End-to-end against Polar sandbox: checkout → webhook → Team; add project → quantity bump; cancel → grace → Free; verify read-only projects render with evidence.
- [ ] 12.3 Rollout per design.md Migration Plan: fields+catalog deploy → webhook endpoint + Polar registration → reconciliation (dry-run) → enable upgrade button after sandbox pass → reconciliation out of dry-run after one clean cycle.
- [ ] 12.4 **Needs the user to run this:** create the Polar org/product/prices, set SSM secrets, register the webhook URL, run the sandbox checkout. Leave checked-off only once done.
