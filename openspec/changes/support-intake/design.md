## Context

See `proposal.md` — Why. Current state: `CONTACT_EMAIL`,
`SECURITY_CONTACT_EMAIL`, `LEGAL_CONTACT_EMAIL` in `web/src/lib/site.ts` all
resolve to the same personal Gmail. `SECURITY.md` and `CONTRIBUTING.md` are the
only process docs; both say "open a GitHub issue first" with no template. The
repo is private, so no external issues exist yet. `hosted/plans.json` is the
single source of truth for tier `support` strings; `web/src/lib/plans.ts` and
`hosted/.../PlanCatalog.cs` both parse it and both fail loudly if `support` is
missing. The pricing and features pages render `tier.support` as literal text.

`company-and-domain-launch` (in progress, 19/52) will introduce
`support@releasetwin.com` and split the three contact constants. This change
must not block on that.

## Goals / Non-Goals

**Goals:**
- One documented path in for every request type, discoverable from the places a
  user actually looks (repo root, "New issue" flow, README, pricing page).
- Tier support copy that is literally true.
- An operator runbook so triage is a habit, not an inbox pile.
- Everything shippable now, against the current Gmail, with a clean swap point
  for `support@` later.

**Non-Goals (design-level):**
- Any tooling that stores or tracks tickets outside GitHub Issues + email.
- Deciding the exact triage interval as a contractual promise — the runbook
  states a target, not an SLA.
- Touching `web/` component code. The copy change flows through data only.

## Decisions

### 1. GitHub Issues is the system of record for bugs; email for account/billing/legal

GitHub issue forms give structured intake for free, live where the code is, and
are public (good — dedupes reports, shows responsiveness). Account, billing, and
legal matters are private and often tied to an org identity, so they go to
email and are recorded by the operator per the runbook.

_Alternative rejected:_ stand up a helpdesk (Zendesk / Plain / HelpScout) now.
Monthly cost, a second inbox to monitor, and migration lock-in — all for zero
current customers. Revisit when inbound volume actually strains a weekly sweep.

_Alternative rejected:_ enable GitHub Discussions as the Q&A channel. More
surface to moderate on day one; the `config.yml` contact link can point at a
Discussions tab later without reworking anything.

### 2. Address indirection via a documented constant, not a hardcoded string

`SUPPORT.md` and `docs/support.md` refer to "the support address
(`support@releasetwin.com` once live; currently the address in
`web/src/lib/site.ts` `CONTACT_EMAIL`)". Issue-template `config.yml` contact
links use the current Gmail with a `TODO(company-and-domain-launch)` comment.
When that change swaps the site constants it also does a documented grep for
the Gmail across `SUPPORT.md`, `docs/support.md`, and `.github/` — added as a
task there, referenced from here.

_Alternative rejected:_ wait for `company-and-domain-launch` to finish first.
It has 33 open tasks on a different critical path; the intake gap is real now
and the swap is a five-minute find-and-replace.

### 3. Bug form routes severity to `SECURITY.md` scope

The bug form includes a required checkbox: "Could this cause a wrong case
verdict, or leak fixture/secret/evidence content?" If checked, the form's
intro text tells the reporter to stop and use the security advisory flow
instead. This makes the CLI-side redaction / false-verdict severity class from
`SECURITY.md` visible at the point of report rather than buried in a policy
file.

### 4. Tier copy values

| Tier | Old | New |
|------|-----|-----|
| Free | `Community / GitHub` | `Community · GitHub issues` |
| Team | `Email` | `Email · best-effort, ~2 business days` |
| Enterprise | `SLA + shared Slack` | `Priority email` |

"~2 business days" matches the "acknowledgement within a few days" language
already in `SECURITY.md` — one expectation across all docs. Enterprise keeps a
differentiator ("priority") without naming a channel or a guarantee that does
not exist. `docs/plan-catalog`-style consumers only require the field to be a
non-empty string, so no parser changes.

### 5. `go-public-sequence` gets a pointer, not a merge

This change adds one checklist line to that change's "announcement readiness"
step ("`SUPPORT.md` + issue templates live and verified — see
`support-intake`"). The two stay separate: this one can merge and archive on
its own timeline; `go-public-sequence` just gains a dependency check.

## Risks / Trade-offs

- **Stale Gmail left in `.github/config.yml` after the domain lands** → the
  swap task is added to `company-and-domain-launch` *and* listed as an
  open question here; a grep in that change's verification catches it.
- **Public issue templates invite noise / spam once the repo is public** →
  `config.yml` disables blank issues and the forms require structured fields;
  if abuse appears, GitHub's interaction limits are a later, separate lever.
- **"~2 business days" reads as a commitment a solo maintainer can miss** →
  wording is "best-effort" and the runbook frames it as a target; no
  contractual SLA text anywhere. Acceptable for pre-pilot; revisit if a Team
  customer signs.
- **Enterprise prospects may still ask for the Slack channel** → sales
  conversation, handled case by case; nothing in writing promises it, which is
  the point.

## Migration Plan

Pure additive docs + config + one data edit. No deploy step beyond the normal
`web` build (which re-renders the pricing page from `plans.json`) and the
`hosted` test suite. Rollback = revert the PR; no state, no data migration.
Verification per `CLAUDE.md`: `npm run build` + `npx eslint` in `web/`,
`dotnet build` + `dotnet test` for the plan-catalog tests, and
`openspec validate support-intake --strict`.

## Open Questions

- Exact triage-sweep interval to write into `docs/support.md` (2 vs 3 vs 5
  business days). Does not affect specs, approach, or task breakdown — the
  runbook has a placeholder the user sets before merge.
- Whether `SUPPORT.md` should also link a future `/docs/support` marketing
  page. Deferred; the repo-root file is the required artifact, a marketing
  page is a later nicety.
