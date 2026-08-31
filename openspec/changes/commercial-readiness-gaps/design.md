## Context

See `proposal.md` — Why. The hosted API (`hosted/ReleaseTwin.Hosted.Api`) is a
JSON-only .NET service on a Lambda Function URL with a DynamoDB single-table
store; `web/` is the BFF Next.js frontend. Auth is Clerk for web sessions and
opaque API tokens for ingest.

Constraints that shape this design:

- **`AppUser.OrganizationId` is a scalar** and is read by `ProvisioningService`,
  `CurrentOrganizationAccessor`, and effectively every endpoint. `Project`'s
  primary key is `(OrganizationId, ProjectId)` by deliberate design and does not
  change.
- **Single-table DynamoDB**, overloaded GSIs, per the project convention — no new
  tables.
- **CI-only deploy** (Terraform via GitHub Actions OIDC). Any new infra
  (an outbound-delivery queue, a share-link route) is Terraform in the existing
  split-layer layout.
- The **evidence redaction guarantee** is that the CLI strips secrets before
  upload and the hosted side never holds raw content. Share links must not
  weaken this — they can only re-expose what is already stored redacted.
- Billing (Polar MoR) is mid-flight in the separate `billing-integration` change;
  this design must not fork its data model. Per-project pricing quantity =
  project count, and **member count is not a billing axis** in Phase 1.

## Goals / Non-Goals

**Goals:**

- A membership model that supports N users per org and a user in N orgs, without
  a data migration that requires downtime.
- Role checks centralized so endpoints ask one authorizer, not scattered `if`s.
- Outbound delivery that cannot slow or break the ingest path.
- A share-link surface that is provably limited to one run's redacted evidence.

**Non-Goals:**

- Seat-based billing or usage caps per member.
- Project-scoped roles — Phase 1 roles are org-scoped only (D10 preserves the
  seam to add project scoping later without reworking callers).
- More than the fixed `admin` / `member` / `viewer` set (D9), or custom
  (org-defined) permission bundles.
- SSO/SAML, SCIM provisioning, audit log (deferred in the proposal).
- Cross-org identity federation beyond "same Clerk user in multiple orgs".
- Notification channels beyond Slack webhook + generic webhook (no email, no
  PagerDuty, no native Slack app in Phase 1).

## Decisions

### D1: ReleaseTwin owns the membership table; Clerk stays identity-only

Clerk offers Organizations + Invitations. We do **not** adopt them as the source
of truth. Membership, roles, and invitations live in the ReleaseTwin single
table; Clerk remains "who is this human" only.

- **Why:** entitlements, billing status, plan tier, and project scoping already
  hang off the ReleaseTwin `Organization`. Splitting org membership into Clerk
  would put the authorization graph in two systems that must be kept in sync, and
  would couple our tenancy model to a vendor primitive we can't query alongside
  our own data. It also keeps the door open to non-Clerk auth later (the
  `account-provisioning` spec deliberately avoids presupposing a specific
  provider).
- **Alternative rejected:** Clerk Organizations as source of truth. Cleaner
  invite UI out of the box, but every entitlement check would need a Clerk
  round-trip or a synced shadow copy, and webhook lag becomes an authorization
  correctness problem.
- **Cost accepted:** we build the invite email + accept flow ourselves.

### D1a: How provisioning recognises an invited user (resolved during apply)

`GetOrCreateUserAsync` runs in `OnTokenValidated` from the Clerk JWT alone, before
any endpoint sees the invite token — so it needs another signal to skip minting a
throwaway org for someone who is only signing up to join an existing org.

- **Chosen:** the `/invitations/<token>` web page forwards the token as an
  `X-Invite-Token` header on its API calls. Provisioning looks the invitation up
  by that token; if it is acceptable and its email matches the JWT `email` claim
  (or the JWT carries no email), the user is created with **no** organization
  (`OrganizationId = Guid.Empty`). The clean path wants a Clerk JWT template that
  includes `email`.
- **Reconcile fallback:** when the header is absent (user signed up elsewhere,
  then navigated to the invite), the accept endpoint deletes the user's
  auto-created org iff it is provably empty — zero projects and the user is its
  sole member — and clears their membership in it.
- **Alternative rejected:** a by-email GSI on invitations so provisioning can scan
  for a pending invite. More infra for a narrow window the reconcile path already
  covers.

### D2: Single-table layout for membership

New item types on the existing table:

```
PK                     SK                      type          notable attrs
ORG#<orgId>            MEMBER#<userId>          Membership     role, createdAt
ORG#<orgId>            INVITE#<token>           Invitation     email, role, expiresAt, state
```

New GSI (or reuse an existing overloaded one) keyed for the reverse lookup
"orgs for a user":

```
GSI: PK = USER#<userId>, SK = ORG#<orgId>   → membership rows by user
```

- **Why:** the two hot queries are "members of org X" (query PK=`ORG#x`,
  SK begins_with `MEMBER#`) and "orgs for user U" (GSI query). Both are single
  partition. Invitations by token are a direct get on
  `PK=ORG#<orgId>, SK=INVITE#<token>` — but the accept flow only has the token,
  not the org, so the token itself encodes the orgId (`<orgId>.<random>`), or a
  thin `INVITE#<token>` → orgId pointer item is added. Chosen: **encode orgId in
  the token** — no extra item, and the token is already single-use + expiring.
- **Alternative rejected:** a separate `memberships` table. Violates the
  single-table convention and adds a second thing to back up / migrate.

### D3: Migration is additive and lazy — no backfill job

- Add `Membership` items. Keep `AppUser.OrganizationId` populated (write-through)
  for one release as a fallback read path.
- On first load of an existing user with no `Membership` item, synthesize one
  (`role = admin`, `orgId = AppUser.OrganizationId`) and persist it. This is the
  read-repair pattern already used for the legacy `"Paid"` → `Team` tier.
- `CurrentOrganizationAccessor` gains an "active org" concept: read from a signed
  session claim / cookie set at login (defaulting to the user's sole membership,
  or last-used); fall back to `AppUser.OrganizationId` only while the compat
  window is open.
- After one release with write-through + read-repair, a later change drops
  `AppUser.OrganizationId`.

- **Why:** the existing user base is tiny and every existing user is a solo org
  admin — the synthesized membership is exactly right for all of them. A batch
  migration would be more code and more risk for zero additional correctness.
- **This is the BREAKING item** flagged in the proposal: the invariant "one
  signup = one fresh org" is gone. Handled entirely by D3's provisioning-time
  branch (has pending/accepted invite → join; else → create).

### D4: Central authorizer, not scattered checks

One `IOrganizationAccessGuard.Require(orgId, user, Capability)` consulted by every
org-scoped endpoint, returning the resolved membership+role or throwing a
`ForbiddenException`. `Capability` is a small enum
(`ManageBilling`, `ManageTokens`, `ManageMembers`, `ManageNotifications`,
`UseProjects`, `ViewEvidence`). Role→capability is a static table (three roles as
of D9: `admin`, `member`, `viewer`).

- **Why:** the current code scatters `AppUser.OrganizationId` equality implicitly.
  Introducing roles multiplies that surface. A single guard is testable in
  isolation and makes "what can a member do" one file to read. Mirrors the
  existing `AdminOperators` allowlist pattern and `IEntitlementService.For(org)`.
- **Alternative rejected:** ASP.NET policy handlers per capability. More
  ceremony, and the org id comes from the route/body not the principal, which
  fights the policy model.

### D5: Notifications and share links reuse `IEntitlementService`

Two new keys in `hosted/plans.json` entitlements (`runNotifications: bool`,
`evidenceSharing: bool`), granted Team+. Enforcement calls
`entitlements.For(org).RunNotifications` etc. — same shape as evidence
ingest/config gating today. `EntitlementRequiredException` (code
`entitlement-required` + key) is already the established failure mode.

- **Why:** zero new gating mechanism; consistent error contract; downgrade
  behavior (D-tier loses key → feature stops) falls out for free.

### D6: Outbound delivery is a queue + worker, off the ingest path

Ingest writes the run, then enqueues a `NotificationRequested` message
(SQS, added in `hosted/terraform/`). A Lambda consumer resolves the project's
enabled targets, checks the entitlement at send time, POSTs with a short timeout,
retries with backoff (SQS redrive → DLQ), and records last-outcome per target on
the target item.

- **Why:** ingest latency and reliability are load-bearing for a CI gate. A
  customer's broken Slack webhook must never turn a green run red or slow it.
  Async + DLQ is the standard shape and there's already Lambda+Terraform
  plumbing.
- **Alternative rejected:** fire-and-forget `Task.Run` inside the ingest handler.
  Lambda freezes the execution environment after the response — background tasks
  are not reliably allowed to finish.
- **SSRF:** the worker resolves the target hostname and refuses
  private/loopback/link-local ranges at send time (not just at save time — DNS
  can change). HTTPS only. No redirects followed.

### D7: Share links are a separate unauthenticated route with a narrow projection

`GET /share/<token>` on the web app (not the authenticated dashboard tree) →
BFF calls a dedicated hosted endpoint `GET /shared-runs/<token>` that returns
**only** a `SharedEvidenceView` DTO: the run's redacted evidence document +
result/classification/hashes. No org id, no project list, no navigation data. The
token is high-entropy (≥128 bits), stored hashed (`PK=RUN#<runId>,
SK=SHARE#<hash>`), with `expiresAt` and `state`. Revocation flips `state`.
Retention purge of a run deletes its `SHARE#` items.

- **Why:** putting the render behind its own route and its own DTO makes the
  "can't reach anything else" requirement a property of the code shape, not of
  careful template writing. The DTO is the security boundary and is unit-testable
  ("does this type carry anything beyond evidence?").
- **Alternative rejected:** a `?share=token` query param on the normal run page
  with conditional hiding of chrome. One missed `{#if}` leaks the dashboard;
  also, per the global rule, sensitive tokens shouldn't ride in query strings and
  the auth'd page pulls sibling data.
- Entitlement is checked **on link creation and on every resolve** — a downgraded
  org's links return 403 without being deleted (spec requirement).

### D8: Seeded sample project is virtual, not a real row

The sample project + its runs/evidence are served from a static fixture baked
into the API, rendered for any org with `hasIngestedRealRun == false`, under a
reserved project id that ingest and token endpoints reject. It is never written
to the org's partition.

- **Why:** a real seeded row would need cleanup logic, would risk counting toward
  quotas, could be mutated, and would bloat every existing org's partition. A
  virtual projection disappears the instant `hasIngestedRealRun` flips and leaves
  no trace.
- **`hasIngestedRealRun`** is a boolean on `Organization`, set on first
  successful ingest (idempotent write).

## Risks / Trade-offs

- **Compat window bugs** → D3 keeps `AppUser.OrganizationId` write-through for a
  release and read-repairs on load; the drop is a separate later change gated on
  telemetry showing no fallback reads.
- **Active-org confusion** (user in 2 orgs acts on the wrong one) → active org is
  an explicit signed session value, shown in the UI header at all times, and
  every mutating response echoes the org it acted on.
- **Invite token in email = bearer capability** → single-use, short expiry,
  revocable, role fixed at issue time, and accepting only grants the named role
  (never admin unless invited as admin).
- **Share link forwarded/leaked** → mitigated by expiry + revocation + one-run
  scope + no sensitive content by construction; documented as "treat like a
  read-only link to that evidence". Not mitigated: viewer identity is unknown
  (acceptable — same as any unguessable share URL; can add view logging later).
- **SSRF via webhook URL** → send-time IP-range check + HTTPS-only + no redirects
  + short timeout; DNS-rebinding window is small and the payload carries no
  secrets even if it hits an internal host.
- **Notification noise** → Phase 1 only fires on failure/ineligible; per-target
  enable/disable; no digesting yet (deferred).
- **Polar quantity coupling** → explicitly documented: Phase 1 quantity =
  projects only; adding members never changes the invoice. Revisit if seat
  pricing is ever introduced.

## Migration Plan

1. Ship membership items + GSI + `IOrganizationAccessGuard` with write-through to
   `AppUser.OrganizationId` and load-time read-repair. No behavior change for
   existing solo orgs.
2. Ship active-org session claim + org switcher (no-op for single-membership
   users).
3. Ship invites, roles enforcement, entitlement keys.
4. Ship notifications (SQS + worker + Terraform) and share links (route + DTO +
   purge hook).
5. Ship seeded sample + guided panel.
6. **Later, separate change:** drop `AppUser.OrganizationId` once telemetry shows
   zero fallback reads.

**Rollback:** steps 1–5 are additive; feature-flag notifications, share links,
and the sample projection behind the existing OpenFeature seam so each can be
disabled without redeploy. Membership read-repair is safe to leave on
permanently.

## Open Questions

- Slack: incoming-webhook URL only in Phase 1, or also accept a bot token for
  channel selection? (Leaning webhook-only; does not affect specs or task
  breakdown.)
- Share-link default expiry value (7d? 30d?) — a config constant, not a design
  decision.
- Whether the guided first-run panel's CLI command should offer a Docker variant
  alongside the dotnet variant — copy detail, decide during implementation.
