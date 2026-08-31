## Why

The engine, adapters, evidence pipeline, and analytics are real and deployed, but
nobody outside the repo has ever used ReleaseTwin as a paying customer. A codebase
walk (2026-08-31) found the remaining blockers are not in execution — they are in
the commercial surface: a stranger cannot bring a teammate, cannot reliably pay,
lands on an empty dashboard, and has no way to route a failed run or a shareable
proof to anyone. This change captures that assessment and scopes the minimum set
of features required before ReleaseTwin can be offered to a design partner for
money.

Three gates, and only the first is close:

```
GATE 1  Stranger reaches "first green run" alone         ~80% there
GATE 2  Stranger can pay without talking to us           blocked (billing not e2e)
GATE 3  Their team can live in it day-to-day and expand  largely missing
```

## What Changes

Phase 1 (this change) — the minimum bar to charge money:

- **Organization membership / teams.** `AppUser` is currently 1:1 with an
  `Organization` created at signup: no invites, no membership records, no roles.
  Introduce a membership model (a user belongs to one or more orgs), an invite
  flow (email link, accept on sign-in), and two roles (**admin**, **member**) —
  admin manages billing, tokens, members; member uses projects and views
  evidence. **BREAKING** for `account-provisioning`: signup no longer implies a
  brand-new org for every user.
- **Billing closed-loop verification.** The Polar (Merchant-of-Record) integration
  is ~40/43 tasks but has never run checkout → webhook → entitlement flip against
  the sandbox. Drive the sandbox e2e path per `docs/billing-sandbox-runbook.md`,
  fix what breaks, then flip `POLAR_UPGRADE_ENABLED` on and take reconciliation
  out of dry-run. (Tracked separately in the open `billing-integration` change —
  this proposal only records it as a hard prerequisite, not new scope.)
- **Onboarding activation.** A newly signed-up org sees an empty dashboard until a
  CLI run lands. Add: a seeded read-only sample project (example run history +
  flag-proof result + evidence drill-down) visible until the org's first real
  ingest, and a guided first-run panel (create project → copy token → run this
  command).
- **Run notifications (outbound).** Alerting today targets the *operator*, not the
  customer. Add per-project notification targets — Slack incoming webhook and/or a
  generic outbound webhook — fired on a failed run or a failed/ineligible flag
  proof, with a link back to the hosted run. Team-gated.
- **Shareable evidence links.** The evidence document is the product but is
  trapped behind login. Add per-run, revocable, read-only share links that render
  the already-redacted evidence view to an unauthenticated viewer. Team-gated.

Explicitly deferred (not in this change — revisit once 1–2 pilots are live and
their workflows dictate priority):

- CLI distribution (NuGet global tool, Homebrew, pinned Action version)
- `releasetwin init` scaffold + config-driven adapter selection (already have
  their own merged proposals; not blocking a first sale)
- Deeper PR integration (results as PR comment/check with evidence link)
- SSO/SAML, audit log, data residency
- Signed / tamper-evident evidence (cryptographic attestation)
- Non-REST adapter (DB / queue / gRPC) — scope to a specific design partner
- Hosted fixture store
- Private / on-prem hosted deployment

## Capabilities

### New Capabilities

- `org-membership`: users, organizations, and the many-to-many membership between
  them; invite issuance and acceptance; the admin/member role split and which
  operations each role may perform.
- `run-notifications`: per-project outbound notification targets (Slack webhook,
  generic webhook), the events that trigger them (run failure, flag-proof
  failure/ineligibility), payload shape, delivery/retry semantics, and Team-tier
  gating.
- `evidence-sharing`: per-run revocable read-only share links, the
  unauthenticated render path, what the shared view exposes (the redacted
  evidence document only) and excludes, expiry/revocation, and Team-tier gating.
- `onboarding-activation`: the seeded sample project shown to a new org until its
  first real ingest, its lifecycle, and the guided first-run panel contract.

### Modified Capabilities

- `account-provisioning`: signup provisions a user and, only when the user has no
  existing org membership, a default org — membership, not a fresh org per user,
  becomes the invariant. Token and project operations are scoped by membership +
  role rather than by a 1:1 user→org link.
- `plan-tier-gating`: add `run-notifications` and `evidence-sharing` as
  Team-gated entitlements routed through `IEntitlementService`.

## Impact

- **hosted API:** new `Membership`/`Invitation` entities + repositories; rework
  `ProvisioningService`, `CurrentOrganizationAccessor`, and every endpoint that
  assumes `AppUser.OrganizationId`; new endpoints for invites, members, roles,
  notification targets, and share links; new outbound-delivery worker/path;
  seeded-sample-project provisioning.
- **web/:** members & invites settings UI, accept-invite page, org switcher,
  notification-target settings, share-link controls on the run view, empty-state /
  guided first-run panel, unauthenticated shared-evidence route.
- **auth:** Clerk organization/invitation primitives vs. a ReleaseTwin-owned
  membership table (decision for design.md); web-session auth handler must resolve
  the active org from membership.
- **data model:** membership is a new access path over the single-table layout
  (GSI for user→orgs); `Project` PK stays `(OrganizationId, ProjectId)`.
- **entitlements / billing:** per-project pricing quantity must track projects,
  unaffected by member count in Phase 1 (seats are not a billing axis yet — note
  that decision); two new entitlement keys.
- **security:** share links are the first unauthenticated data path — threat-model
  the redaction guarantee and link entropy/expiry; outbound webhooks need SSRF
  protection on customer-supplied URLs.
- **docs:** `customer-pilot-guide.md` and the installation model update once teams
  and paid signup are real.
