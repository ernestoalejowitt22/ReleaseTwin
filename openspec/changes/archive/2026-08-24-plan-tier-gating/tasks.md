## 1. Data model

- [x] 1.1 Add a `PlanTier` enum (`Free`, `Paid`) and field to `Organization`, defaulting to `Free`.
- [x] 1.2 Update `OrganizationRepository.ToItem`/`ToOrganization` to include `PlanTier`; update `ProvisioningService.GetOrCreateUserAsync`'s organization construction to set `PlanTier = Free` explicitly. (Organization construction lives in `ProvisioningService`, not `UserRepository` — `UserRepository.CreateWithOrganizationAsync` just persists what it's given.)
- [x] 1.3 Add `SetPlanTierAsync(Guid organizationId, PlanTier tier)` to `IOrganizationRepository`/`OrganizationRepository` (read, mutate, re-put — same pattern as `ApiTokenRepository.RevokeAsync`).

## 2. Project-limit enforcement

- [x] 2.1 Add a `ProjectLimitExceededException`.
- [x] 2.2 In `ProvisioningService.CreateProjectAsync`, check the organization's `PlanTier`; if `Free` and it already has ≥ 1 project (`IProjectRepository.ListByOrganizationAsync`), throw `ProjectLimitExceededException` instead of creating.
- [x] 2.3 In `DashboardEndpoints`'s `POST /projects`, catch `ProjectLimitExceededException` and return `403 Forbidden` with `{ "error": "free-tier-project-limit" }`.

## 3. Upgrade action

- [x] 3.1 Add `UpgradeOrganizationAsync(Guid organizationId)` to `ProvisioningService`, calling `SetPlanTierAsync(organizationId, PlanTier.Paid)`.
- [x] 3.2 Add `POST /api/dashboard/upgrade` to `DashboardEndpoints`, resolving the organization via `CurrentOrganizationAccessor`, no request body.

## 4. Dashboard read path

- [x] 4.1 Add `PlanTier` to `DashboardView` (via `DashboardService`, reading the organization once per `GetDashboardViewAsync` call). Also fixed two pre-existing tests (`TokenIsScopedToItsOwnProject`, `ReportsAcrossMultipleProjectsInOneOrgAreSummed`) that created 2 projects under one Free-tier org — now upgrade first, since that's the real new constraint, not a bug. 37/37 tests pass.

## 5. Frontend

- [x] 5.1 Add `planTier` to `web/src/lib/types.ts`'s `DashboardView`.
- [x] 5.2 Add an `upgradeOrganization` server action in `web/src/app/dashboard/actions.ts`.
- [x] 5.3 Render the current tier and, when Free, an "Upgrade" button in `web/src/app/dashboard/page.tsx`, near the existing usage-summary card.
- [x] 5.4 Handle the `403`/`free-tier-project-limit` response from project creation with a clear message pointing at the Upgrade control, rather than a generic error. Also fixed a real Next.js gotcha along the way: kept the success-path `redirect()` outside the `try/catch` scoped to the API call, since `redirect()` throws internally and would otherwise be caught by the same handler. Also added a global `JsonStringEnumConverter` in `Program.cs` — `PlanTier` is the first enum in the dashboard JSON contract and would otherwise serialize as a raw integer. `npx tsc --noEmit` clean.

## 6. Tests

- [x] 6.1 Unit tests (`PlanTierGatingTests.cs`): new organizations default to `Free`; a `Free` org's first project succeeds; a `Free` org's second project throws `ProjectLimitExceededException`; a `Paid` org's Nth project always succeeds; upgrading a `Free` org at its limit immediately allows another project. 42/42 tests pass.
- [x] 6.2/6.3 Scope note: this codebase has no `ClerkJwt` test-authentication double (confirmed — every existing `/api/dashboard` HTTP test only covers unauthenticated/wrong-scheme *rejection*, e.g. `DashboardHttpTests`, `SchemeIsolationTests`; no test ever authenticates *as* a dashboard user over real HTTP). Building one just for these two endpoints would be disproportionate new test infrastructure for this change, and risks destabilizing the existing scheme-isolation tests. Verified instead via the real, live-Clerk-authenticated Cypress e2e flow (task 7.2) — a stronger check than a synthetic auth double, since it exercises the actual `POST /projects` 403 path and `POST /upgrade` through a real browser session.

## 7. Verification

- [x] 7.1 Run the full hosted API test suite and confirm all pass — 42/42 pass.
- [x] 7.2 Ran the full stack for real: `docker compose up -d` (fresh DynamoDB Local) + hosted API + `web/` dev server + the extended Cypress e2e spec, live Clerk sign-in. Verified the complete real sequence: first project succeeds on Free → second project rejected with the `free-tier-project-limit` banner (screenshot: `02b-project-limit-rejected.png`) → Upgrade click flips the badge to "Paid plan" and removes the Upgrade control → second project now succeeds (screenshot: `02d-second-project-after-upgrade.png`). This also caught and fixed a real bug: `PlanTier` would have serialized as a raw integer without a `JsonStringEnumConverter` (added to `Program.cs`).
