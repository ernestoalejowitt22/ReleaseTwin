# Design

## Catalog as data, not code

`hosted/plans.json` is the source of truth. Rejected alternative: a C# static class with
a build step emitting JSON for `web/`. That keeps types close to enforcement but makes the
marketing repo depend on a generated artifact and a running generator; it also invites the
enforcement copy and the display copy to diverge silently — the exact failure this change
exists to remove. A single hand-authored JSON file with a shape-check test on each side
(C# + TS) is the smaller, drift-proof option.

`web/` may be built without the API reachable, so the marketing site imports the file
directly (`import plans from "../../hosted/plans.json"` via a path alias, or a copied
build input — decide in `marketing-site-dynamic-content`). `GET /plans` exists for the
dashboard's live UI only.

## PlanTier migration

The `Organization` item stores `PlanTier` as `Attrs.S(org.PlanTier.ToString())` and reads
it with `Enum.Parse<PlanTier>(v.S)`. Adding enum members is backward compatible for
`Free`. The only stored value that stops parsing is `"Paid"`.

Migration: a one-shot script (or a lazy read-repair) that rewrites every `Organization`
item whose `PlanTier == "Paid"` to `"Team"`. Given the current customer count (pre-pilot,
effectively zero paid orgs), this is a **manual `aws dynamodb scan` + `update-item` loop**
run once from CI or locally — listed as a "Needs the user to run this" task. A lazy
read-repair (`"Paid"` → treat as `Team`, rewrite on next save) is included as defensive
code so a missed row degrades gracefully instead of throwing.

## EntitlementService shape

```csharp
public sealed record Entitlements(
    int? MaxProjects,
    bool EvidenceViewer,
    int? MaxEvidenceRetentionDays,
    bool CustomRedactionRules,
    bool ProjectSecrets,
    bool TrendAnalytics,
    bool ReleaseRollup,
    bool CiIntegration,
    bool Sso,
    bool AuditLog);

public interface IEntitlementService
{
    Entitlements For(PlanTier tier);            // pure, catalog lookup
    Entitlements For(Organization org) => For(org.PlanTier);
}
```

`null` numeric entitlements mean "unlimited / custom". Callers treat `MaxProjects is null`
as no cap and `MaxEvidenceRetentionDays is null` as "bounded only by
`Project.MaxEvidenceRetentionDays`".

The catalog is loaded once at startup from the embedded resource and validated against a
static expected-shape check; a malformed or incomplete `plans.json` fails app startup
(and the build, via a test), never silently yields an empty entitlement set.

## Gate call-site changes (mechanical)

| Call site | Before | After |
|---|---|---|
| `ProvisioningService` project create | count >= 1 && tier == Free | `max is not null && count >= max` |
| `EvidenceIngestService:73` | `tier != Paid` | `!ent.EvidenceViewer` |
| `EvidenceConfigEndpoints:75` | `tier != Paid` | `!ent.EvidenceViewer` |
| `EvidenceConfigEndpoints` retention bound | `1..365` const | `1..(ent.MaxEvidenceRetentionDays ?? 365)` |
| `ProjectSecretService:34` | `tier == Free` | `!ent.ProjectSecrets` |
| `DashboardService:99` | `tier == Paid` | `ent.EvidenceViewer` |

`PaidTierRequiredException` is renamed `EntitlementRequiredException` carrying the missing
entitlement name; the HTTP mapping (402/403 + message) is unchanged in shape.

## Out of scope (seams left)

- **Stripe / billing.** `SetTierAsync` is the single mutation point; a future billing
  webhook calls it. No `StripeCustomerId` field is added now.
- **SSO / audit-log implementation.** The entitlements exist and render "contact us" on
  the marketing site; no auth or logging code is written here.
- **Per-seat / RBAC.** Not modeled.
