## Context

See proposal.md — Why. This is an operational sequence, not a feature. The only
real decision is the prod-stack question; the rest is ordering around two
external dependencies (a GitHub Support ticket, licensing review from
`company-and-domain-launch`).

## Goals / Non-Goals

**Goals:**
- One tracked, ordered checklist for making the platform public.
- A recorded decision on the prod stack with its deferred-migration cost.
- No repo flips public before cached pre-rewrite SHAs are confirmed expired.

**Non-Goals:**
- Marketing launch (Show HN / Product Hunt / marketplaces).
- Any change to sign-up, provisioning, or entitlement behavior.
- Cutting a prod stack unconditionally — that is the decision, not a given.

## Decisions

### Pilot on the `releasetwin-dev-` stack; cut a prod stack only at customer #2
The auto-deploy stack already carries the alerting, evidence-purge, and
evidence infra. A first paid pilot does not need isolation from a dev prefix
that has no other tenants. Cutting a dedicated prod stack now adds a
`workflow_dispatch` apply path and a second environment to keep in sync for no
present benefit. **Alternative rejected:** cut prod now — cleaner story, but the
DynamoDB single-table prefix rename is a data migration that is *cheapest at
zero/low volume*, so deferring it costs little and we may skip it entirely if
the dev-prefixed stack proves fine. Record the migration steps so the option
stays open.

### Repo flip is gated on an explicit GitHub Support confirmation
History was rewritten and force-pushed on both repos (2026-08-30); GitHub caches
pre-rewrite SHAs until GC. A public repo with a reachable cached SHA re-exposes
purged prior-vendor history. The flip task is blocked until Support confirms
expiry in writing — not a timer.

### Sign-up exposure is last
Linking sign-up from the marketing site is the point of no return for
"anyone can create an account." It comes after the funnel is verified end to
end and after billing is in production (`company-and-domain-launch` §7), so the
first self-serve user hits a working upgrade path.

## Risks / Trade-offs

- **Dev-prefix stack turns out to need prod isolation later (compliance ask).** →
  Migration steps are recorded in this change; low-volume rename is bounded work.
- **Repo flipped before licensing review completes.** → Ordered after
  `company-and-domain-launch` §6.7 (legal review); listed as an explicit
  prerequisite task.
- **NAHA public exposes the demo target's internals.** → NAHA tree is already
  purged of prior-vendor refs; treat its flip as independent and optional — the
  ReleaseTwin demo only needs the Vercel Preview, not a public repo.

## Migration Plan

Deferred prod-stack cut (if ever chosen): `workflow_dispatch` the hosted deploy
with a new `table_prefix`; export/import the DynamoDB single table; re-point
`Api__PublicUrl` / repo vars; retire the dev-prefixed tables after a soak.

## Open Questions

- Does NAHA go public at all, or stay private as just the demo target? (Does not
  block the ReleaseTwin sequence.)
