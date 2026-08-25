## Context

See `proposal.md` - Why. Relevant existing shape:

- `Organization` (`Data/Entities/Organization.cs`) currently has `Id`, `Name`, `CreatedAt` — no tier concept.
- `OrganizationRepository.ToItem`/`ToOrganization` (`Data/Repositories/OrganizationRepository.cs`) is the single mapping point for the org's DynamoDB item; `IOrganizationRepository` today only exposes `GetAsync`, no write path (organizations are only ever created as part of `UserRepository.CreateWithOrganizationAsync`'s transaction).
- `ProvisioningService.CreateProjectAsync(organizationId, name)` (`Services/ProvisioningService.cs`) creates a project unconditionally today — no check against the organization at all.
- `DashboardEndpoints.cs`'s `POST /api/dashboard/projects` calls `CreateProjectAsync` and returns `201 Created`; there's no existing "rejected, here's why" response shape for this endpoint to extend.
- `IProjectRepository.ListByOrganizationAsync` already returns every project in an org — the data needed to count them for the limit check already exists, no new query pattern needed.

## Goals / Non-Goals

**Goals:**
- Free-tier organizations are capped at 1 project; Paid are unlimited.
- Upgrading is instant and self-serve, with zero payment collection.
- The rejection when hitting the limit is explicit and actionable, not a generic 4xx.

**Non-Goals:**
- Any real payment/Stripe integration — `PlanTier` is settable directly, no purchase flow.
- Downgrade (Paid → Free) — not requested; a Paid organization with >1 project downgrading would need a defined behavior (delete projects? keep them grandfathered?) that hasn't been decided, so it's out of scope until asked for.
- Gating anything other than project count (usage volume, GitHub connections, seats) — per proposal.md's explicit rejection of those alternatives.
- A visible "Free vs Paid" pricing/feature comparison page — this change is the mechanism, not the marketing.

## Decisions

**`PlanTier` as a simple enum on `Organization`, not a separate entity.** One organization has exactly one tier at a time — no history, no multiple concurrent plans — so a field is sufficient; a separate `Subscription`-style entity would be over-modeling for something with no billing periods or payment state yet.

**Limit check happens in `ProvisioningService.CreateProjectAsync`, reading the org and its current project count before creating.** `CreateProjectAsync` becomes: `GetAsync(organizationId)` → if `PlanTier == Free`, `ListByOrganizationAsync(organizationId).Count` → if already ≥ 1, throw a typed `ProjectLimitExceededException` (new) instead of creating. This is two extra reads on an already low-frequency operation (project creation, not ingest) — acceptable cost for correctness, and avoids inventing a maintained counter that could drift from the actual project list `ListByOrganizationAsync` already returns.

**`DashboardEndpoints`'s `POST /projects` catches `ProjectLimitExceededException` and returns `403 Forbidden` with a body naming the reason** (`{ "error": "free-tier-project-limit" }`), rather than a generic 400/500 — the frontend needs to distinguish "you hit the limit" from "the name was invalid" to show the right message and, per the dashboard spec, the correct upgrade prompt.

**`IOrganizationRepository` gains `SetPlanTierAsync(organizationId, tier)`.** Implemented as `GetAsync` + re-`PutItemAsync` with the tier attribute changed, reusing `OrganizationRepository.ToItem`/`ToOrganization` (now including `PlanTier`) — no new write pattern, same shape as `ApiTokenRepository.RevokeAsync`'s "read, mutate, re-put" approach already used elsewhere in this codebase.

**Upgrade endpoint: `POST /api/dashboard/upgrade`, no request body, acts on the caller's own organization** (resolved via `CurrentOrganizationAccessor`, same as every other dashboard endpoint) — there's nothing to upgrade *to* (only one paid tier exists) and nothing to pay, so no parameters are needed.

**Frontend: tier + conditional Upgrade button rendered in the existing "Usage this month" card's area, not a new page.** Keeps plan-tier information co-located with the other organization-wide (not project-scoped) summary already established by `usage-metering`.

## Risks / Trade-offs

- [Two extra reads (`GetAsync` + `ListByOrganizationAsync`) on every project creation, even for Paid orgs where the limit never applies] → Accepted; project creation is low-frequency, and short-circuiting on `PlanTier == Free` before the count read keeps the Paid-tier cost to one read.
- [No downgrade path exists — an organization that upgrades can never go back through this UI] → Accepted per Non-Goals; the ambiguity about what happens to extra projects on downgrade needs a real decision this change doesn't make.
- [`PlanTier` being self-serve-settable with no payment gate means anyone can flip it] → Accepted; this is explicitly a placeholder for a future real paid flow (per proposal.md's Why), not a security boundary — no revenue is actually at stake yet.

## Open Questions

(none — all decisions needed to write specs/tasks were resolved above or in the preceding conversation)
