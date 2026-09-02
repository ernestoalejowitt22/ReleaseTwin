## Why

The hosted platform is deployed, a production Clerk instance is wired, billing
is merged, and the marketing site is live — but nothing is public. Self-serve
sign-up is not linked or announced anywhere, both repos (`ReleaseTwin` and
`NAHA`) are private, and it is unverified whether the auto-deploy stack
(`table_prefix=releasetwin-dev-`) is what a real customer should land on.

These steps have accumulated as scattered "still to do" notes across memory and
`docs/`. They are not engineering features — they are an ordered operational
checklist with real external dependencies (DNS, vendor dashboards) and a
sequencing hazard: flipping a repo public before the licensing counsel review
(from `company-and-domain-launch`) lands.

This change is the single tracked place for that sequence. It is deliberately
downstream of `company-and-domain-launch` (domain + the ToS/licensing counsel review) and does **not**
block a first paid design-partner pilot, which can run on the dev stack with a
hand-sent invite.

## What Changes

- **Prod-stack decision** — decide and record whether first customers stay on
  the `releasetwin-dev-` prefixed stack or a dedicated prod stack is cut via
  `workflow_dispatch` with a prod prefix. Capture the DynamoDB-prefix migration
  cost if deferred.
- **Repo visibility flip** — make `ReleaseTwin` and `NAHA` public once the
  licensing review (from `company-and-domain-launch`) is done. (The cached
  pre-rewrite-SHA concern is closed — NOT PURSUED, decision 2026-09-01: both
  repos private, 0 forks, no fork network; residual risk accepted. A pre-flip
  history grep stays as a sanity check.)
- **Open self-serve sign-up** — link sign-up from the marketing site, verify
  the funnel end to end (sign-up → org provisioned → first project →
  entitlements), and confirm the pricing page CTA points at it.
- **Announcement readiness** — a short pre-flight list (README polish, repo
  description/topics, `SECURITY.md` contact on the company domain) so the repo
  is presentable the moment it flips.

## Capabilities

No spec-level behavior changes. Self-serve sign-up, provisioning, and
entitlements are already specified and built (`onboarding-activation`,
`account-provisioning`, `plan-tier-gating`); this change only links, verifies,
and exposes them. `skip_specs: true`.

## Impact

- **repo settings:** visibility, description, topics for `ReleaseTwin` and `NAHA`.
- **web/:** sign-up link/CTA wiring on the marketing site (small); no new route.
- **terraform / CI:** possibly a `workflow_dispatch` prod-stack apply with a new
  `table_prefix` — decision first, work only if chosen.
- **external:** DNS only if a prod subdomain is added.
- **docs:** record the prod-stack decision and the go-public runbook in `docs/`.
- **no** change to the engine, adapters, execution path, or any hosted API contract.

## Explicitly deferred

- Product Hunt / Show HN / marketplace launches — a marketing act, separate.
- `/blog` and ongoing content.
- Announcing NAHA as anything other than the ReleaseTwin demo target.
