## 1. Plan catalog

- [x] 1.1 `hosted/plans.json` — the three-tier catalog (Free / Team / Enterprise) with
      full entitlement sets, price metadata (`placeholder: true` on Team/Enterprise), and
      support strings, per the proposal.
- [x] 1.2 `hosted/ReleaseTwin.Hosted.Api/Plans/PlanCatalog.cs` — loads `plans.json` as an
      embedded resource (`..\plans.json` linked into `Plans\`); `PlanTierDefinition` +
      `Entitlements` records; shape validation (`Load()`) fails startup on a
      malformed/incomplete catalog or the wrong tier set/order.
- [x] 1.3 `hosted/ReleaseTwin.Hosted.Api/Plans/EntitlementService.cs` — `For(PlanTier)` /
      `For(Organization?)`; unknown stored tier → `free` + `LogWarning`. Registered as a
      singleton in `Program.cs`; catalog loaded once at startup.
- [x] 1.4 `PlanCatalogTests` (7) + `EntitlementServiceTests` (4): catalog loads; exactly
      three tiers in order; every tier complete; Free/Team/Enterprise entitlement values;
      placeholder flags; unknown tier degrades to `free`.

## 2. Three-tier PlanTier

- [x] 2.1 `PlanTier` enum → `Free`, `Team`, `Enterprise` (dropped `Paid`).
- [x] 2.2 `OrganizationRepository.ParsePlanTier` — `"Paid"` → `Team` (read-repair; rewritten
      on next `SetPlanTierAsync`/put), unknown → `Free`, null → `Free`; `ToItem` serializes
      the new names.
- [x] 2.3 `PlanTierGatingTests.PreviouslyPaidTierReadsAsTeam` covers the read-repair.

## 3. Route every gate through EntitlementService

- [x] 3.1 `ProvisioningService.CreateProjectAsync` — cap from `entitlements.MaxProjects`
      (null = unlimited); message names the tier + limit.
- [x] 3.2 `EvidenceIngestService.StoreAsync` + both `EvidenceConfigEndpoints` handlers →
      `entitlements.EvidenceViewer`.
- [x] 3.3 `EvidenceConfigEndpoints` PUT retention bound → `1..(MaxEvidenceRetentionDays ?? 365)`;
      GET reports the tier's `maxRetentionDays`; error names the tier limit.
- [x] 3.4 `ProjectSecretService.SetAsync` → `entitlements.ProjectSecrets`.
- [x] 3.5 `DashboardService` — `evidenceEntitled` → `entitlements.EvidenceViewer`;
      `DashboardView` gains an `Entitlements` field so the UI shows/hides features;
      `web/` `types.ts` + `dashboard/page.tsx` updated to read it.
- [x] 3.6 `PaidTierRequiredException` → `EntitlementRequiredException` (carries `Entitlement`);
      endpoints return `{ error: "entitlement-required", entitlement }` + 403;
      `web/` action error checks updated (`paid-tier-required` → `entitlement-required`).
- [x] 3.7 Updated `PlanTierGatingTests`, `EvidenceStoreTests`, `EvidenceIngestApiTests`,
      `EvidenceConfigApiTests`, `ProjectSecretServiceTests`, `ProjectSecretFetchApiTests`,
      `UsageMeteringTests`, `ProvisioningServiceTests`, `DashboardServiceTests`,
      `StalenessDigestServiceTests`, `ConnectionFlowTests` (via `TestEntitlements` helper).

## 4. GET /plans + upgrade

- [x] 4.1 `PlansEndpoints` — `GET /plans`, unauthenticated, `Cache-Control: public, max-age=300`,
      catalog verbatim, no caller data. `PlansEndpointTests` (2).
- [x] 4.2 `ProvisioningService.SetTierAsync(orgId, tier)` is the single mutation point;
      `UpgradeToTeamAsync` is the self-serve wrapper (→ `Team`, no payment). The
      `/api/dashboard/upgrade` endpoint calls `UpgradeToTeamAsync` — `Enterprise` is
      unreachable from the customer path (no endpoint accepts a tier parameter).
- [x] 4.3 Operator path to set `Enterprise` — an actual endpoint, not a manual DynamoDB edit:
      `PUT /api/admin/organizations/{id}/tier` ([`AdminEndpoints.cs`](../../../hosted/ReleaseTwin.Hosted.Api/Endpoints/AdminEndpoints.cs)),
      ClerkJwt-authenticated + gated on [`AdminOperators`](../../../hosted/ReleaseTwin.Hosted.Api/Services/AdminOperators.cs)
      (an allowlist of Clerk user ids from `Admin:OperatorUserIds`). Not in the allowlist ⇒ 404
      (indistinguishable from no route). Empty allowlist ⇒ closed. Terraform: `admin_operator_user_ids`
      variable → `Admin__OperatorUserIds` env; `deploy-hosted.yml` passes it from the
      `ADMIN_OPERATOR_USER_IDS` repo variable.
- [x] 4.5 `AdminOperatorsTests` (3) cover the allowlist parsing/matching. The HTTP auth path is
      not integration-tested — the test suite has no ClerkJwt-forging harness (same gap every
      other web-session endpoint has); the operator check is a one-line `AdminOperators.IsOperator`
      call over unit-tested parsing.
- [x] 4.4 `PlanTierGatingTests`: `/plans` shape; self-serve upgrade → `Team` not
      `Enterprise`; operator `SetTierAsync` → `Enterprise`; Free 2nd-project rejected;
      upgrade lifts the cap.

## 5. web/ loader (file only — page rewrite is a separate change)

- [x] 5.1 `web/src/lib/plans.ts` — typed catalog loaded via `import "../../../hosted/plans.json"`
      (direct relative import; `next build` + `tsc` both resolve it — no copy step or alias
      needed). `validateCatalog()` runs at module load. Selectors: `tierById`,
      `lowestTierWith`, `ENTITLEMENT_KEYS`.
- [x] 5.2 The drift guard is `validateCatalog()` at import time — it throws (failing
      `next build`) on any shape/order/type drift. It becomes build-enforced once
      `marketing-site-dynamic-content` imports `plans.ts` into the pages; there is no unit
      runner in `web/` to assert it in isolation, and adding one is out of scope here.

## 6. Validation

- [x] 6.1 `openspec validate plan-catalog-and-entitlements --strict` passes.
- [x] 6.2 `dotnet test ReleaseTwin.sln` green (engine unaffected: 12 + 97 + 13 + …);
      `dotnet test hosted/…` — **121 passed** (was 102; +19 new).
- [x] 6.3 `web/`: `npx next build` + `npx eslint` + `npx tsc --noEmit` all green.

## 7. No manual steps

- The `"Paid"` → `Team` migration is handled entirely by the read-repair in 2.2 (a legacy
  row reads as `Team` and is rewritten on its next save). Pre-pilot there are zero paid
  orgs; there is nothing to back-fill. No `aws dynamodb` step.
- Granting `Enterprise` is the admin endpoint in 4.3, not a data-store edit.
- **Standing config (one-time, same pattern as `CLERK_DOMAIN`):** set the
  `ADMIN_OPERATOR_USER_IDS` GitHub repo variable to the operator's Clerk user id(s) so the
  admin tier endpoint has an allowlist. Unavoidable — the API cannot otherwise know which
  authenticated user is an operator. Left empty, the admin surface stays closed (safe default).

## Decisions locked (from proposal Open Questions)

- [x] D1 `plans.json` is the source; `PlanCatalog.Load()` shape-check in C#, `validateCatalog()` in TS.
- [x] D2 `GET /plans` is a pure catalog, no caller tier.
- [x] D3 Retention downgrade: clamp on next write (PUT re-validates against the current
      tier ceiling), never retroactive purge. No downgrade-time mutation.
- [x] D4 `Enterprise` is operator-set only.
