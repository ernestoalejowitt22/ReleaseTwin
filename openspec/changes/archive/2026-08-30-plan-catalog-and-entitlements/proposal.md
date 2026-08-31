## Why

Entitlements today are a binary `PlanTier { Free, Paid }` enum with the tier compared
inline at each gate (`organization.PlanTier == PlanTier.Paid` in `EvidenceIngestService`,
`EvidenceConfigEndpoints`, `ProjectSecretService`, `DashboardService`; a hardcoded
one-project cap in `ProvisioningService`). Two problems:

1. **The marketing site advertises three tiers** (`Free` / `Team` / `Enterprise` in
   `web/src/app/(marketing)/pricing/page.tsx`) that the backend cannot represent. The
   pricing table and the enforced reality have no shared source of truth and already
   disagree.
2. **Limits are not tier-derived.** Evidence retention is a per-project 1–365 window that
   any Paid org can set to the maximum; there is no per-tier ceiling. "Free = 30 days,
   Team = 12 months" is only true by accident (Free simply cannot open the control).

`docs/` (moved to the private planning repo) names per-project pricing with a Free /
Team / Enterprise split. This change builds the backbone every later commercial feature
needs: one catalog, one entitlement resolver, real tiers. **Stripe / payment collection
is explicitly out of scope** — the self-serve upgrade stays payment-free, with a clean
seam for a billing webhook to call later.

## What Changes

- **`hosted/plans.json`** — the single plan catalog, an embedded resource for
  `ReleaseTwin.Hosted.Api` and a JSON module import for `web/`. One entry per tier:

  ```jsonc
  {
    "tiers": [
      {
        "id": "free",
        "name": "Free",
        "price": { "amount": 0, "unit": "forever", "placeholder": false },
        "entitlements": {
          "maxProjects": 1,
          "evidenceViewer": false,
          "maxEvidenceRetentionDays": 30,
          "customRedactionRules": false,
          "projectSecrets": false,
          "trendAnalytics": false,
          "releaseRollup": false,
          "ciIntegration": true,
          "sso": false,
          "auditLog": false
        },
        "support": "Community / GitHub"
      },
      { "id": "team", "price": { "amount": 49, "unit": "project/month", "placeholder": true }, "entitlements": { "maxProjects": null, "evidenceViewer": true, "maxEvidenceRetentionDays": 365, "customRedactionRules": true, "projectSecrets": true, "trendAnalytics": true, "releaseRollup": true, "ciIntegration": true, "sso": false, "auditLog": false }, "support": "Email" },
      { "id": "enterprise", "price": { "amount": 99, "unit": "project/month", "placeholder": true }, "entitlements": { "maxProjects": null, "evidenceViewer": true, "maxEvidenceRetentionDays": null, "customRedactionRules": true, "projectSecrets": true, "trendAnalytics": true, "releaseRollup": true, "sso": true, "auditLog": true }, "support": "SLA + shared Slack" }
    ]
  }
  ```

  `maxProjects: null` / `maxEvidenceRetentionDays: null` mean "no limit / custom". A
  `placeholder: true` price renders with the existing "early-access placeholder" caveat.

- **`PlanTier` enum** gains `Team` and `Enterprise`. `Free` unchanged. Existing `Paid`
  rows migrate to `Team` (see design.md for the migration; `Enum.Parse` currently
  round-trips the string attribute, so a one-time backfill of the `"Paid"` string to
  `"Team"` is all that is needed).

- **`EntitlementService`** — resolves an `Organization` (via its `PlanTier`) to the
  catalog's entitlement set. Every existing gate switches from `== PlanTier.Paid` to an
  entitlement lookup:
  - `ProvisioningService` project cap → `entitlements.MaxProjects` (null = unlimited)
  - `EvidenceIngestService` / `EvidenceConfigEndpoints` evidence gate → `entitlements.EvidenceViewer`
  - `ProjectSecretService` → `entitlements.ProjectSecrets`
  - `DashboardService` `evidenceEntitled` → `entitlements.EvidenceViewer`

- **Retention ceiling becomes tier-derived.** `EvidenceConfigEndpoints` validates the
  requested `retentionDays` against `entitlements.MaxEvidenceRetentionDays` (falling back
  to `Project.MaxEvidenceRetentionDays = 365` when the tier is "custom"), not a single
  global constant. A tier downgrade clamps any project above the new ceiling on next
  write; existing stored values are not retroactively purged (design.md).

- **`GET /plans`** — unauthenticated, returns the catalog as-is. The dashboard and the
  marketing site both render from it (marketing site consumes the file directly at build
  time; `GET /plans` is for the dashboard's live upgrade UI).

- **Self-serve upgrade** (`ProvisioningService.UpgradeToPaidAsync`) becomes
  `SetTierAsync(orgId, targetTier)` accepting `team`. `enterprise` is **not** self-serve —
  it is set by an operator (a documented manual step) or, later, by a sales flow. Still
  **no payment collected**; the `PILOT_FREE_UPGRADE`-style behavior is the default.

## Capabilities

### Added Capabilities

- `plan-catalog`: a single declarative catalog of tiers and their entitlements, served at
  `GET /plans` and consumed by both the API (for enforcement) and the marketing site (for
  display); an `EntitlementService` that resolves an organization to its entitlement set.

### Modified Capabilities

- `plan-tier-gating`: three tiers (`Free`, `Team`, `Enterprise`) instead of `Free` /
  `Paid`; every limit is read from the catalog via `EntitlementService`, not compared
  inline; self-serve upgrade targets `Team`, `Enterprise` is operator-set.
- `evidence-store`: the evidence-storage gate and the retention-window ceiling are
  entitlement-derived; a tier's `maxEvidenceRetentionDays` bounds what a project may set.

## Impact

- `hosted/ReleaseTwin.Hosted.Api/` — new `Plans/PlanCatalog.cs` + `Plans/EntitlementService.cs`,
  `plans.json` embedded resource, `GET /plans` endpoint, `PlanTier` enum + repository
  parse/serialize, and the four gate call-sites above. `ProvisioningService` signature
  change (`UpgradeToPaidAsync` → `SetTierAsync`).
- `hosted/ReleaseTwin.Hosted.Api.Tests/` — `PlanTierGatingTests`, `EvidenceIngestApiTests`,
  `EvidenceConfig` tests, `ProjectSecretFetchApiTests`, `UsageMeteringTests`,
  `ProvisioningServiceTests` all touch the tier model; new `PlanCatalogTests` +
  `EntitlementServiceTests`.
- `web/src/lib/plans.ts` — thin typed loader over `hosted/plans.json` (import + zod-style
  validation). Pricing page rewrite is **change `marketing-site-dynamic-content`**, not
  this one; this change only lands the file and the loader so the two can proceed in
  parallel.
- **No change** to `ReleaseTwin.Core`, adapters, the CLI, or the ingest wire contract.
- DynamoDB: single-table unchanged. `PlanTier` stays one string attribute on the
  `Organization` item. One-time data backfill (`"Paid"` → `"Team"`) — see design.md.

## Open Questions

- Catalog format: JSON (proposed, cross-language, no parser to write) vs a `.NET` static
  class as source with a generated JSON for `web/`. Proposed: **`plans.json` is the
  source**, both sides load it; a schema test in each language fails the build if the
  file drifts from the expected shape.
- Should `GET /plans` also report the caller's *current* tier when authenticated?
  Proposed: **no** — the dashboard already gets that from its session/bootstrap call;
  `/plans` stays a pure catalog and cacheable.
- Downgrade handling for retention: clamp-on-next-write (proposed) vs immediate re-purge
  vs block the downgrade. Proposed: **clamp on next write**, never retroactively delete
  evidence a customer already has — a downgrade shouldn't destroy data.
- `enterprise` self-serve: proposed **operator-set only** for now. Revisit when a sales
  motion exists.
