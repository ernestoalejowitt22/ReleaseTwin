## Why

Every way a user can currently ask for help — bug, security report, billing
question, sales enquiry — lands in one personal Gmail with no template, no
triage rhythm, and no routing. The moment `go-public-sequence` makes the repo
public and links self-serve sign-up, strangers get a bare "New issue" button
and three `mailto:` links backed by nothing. Separately, the pricing page sells
an Enterprise "SLA + shared Slack" that does not exist and will not for the
foreseeable future. This change puts a lightweight, honest intake process in
place before the first outside user arrives, without buying a helpdesk.

## What Changes

- **`SUPPORT.md` at the repo root** — the single routing doc GitHub surfaces in
  the issue-creation flow: bug → GitHub issue; security → advisory (link to
  `SECURITY.md`); billing / account / legal → `support@` (today: `CONTACT_EMAIL`);
  sales / pilot → same address with a subject hint. States the "acknowledgement
  in a few days, no formal SLA" expectation already in `SECURITY.md`.
- **GitHub issue templates** — `.github/ISSUE_TEMPLATE/bug_report.yml` and
  `feature_request.yml` (issue forms), plus `config.yml` that turns off blank
  issues and adds contact links for security and email. Bug form asks for
  ReleaseTwin version / surface (CLI, adapter, hosted, web), repro, expected vs
  actual, and a "does this affect a case verdict or evidence redaction?"
  severity prompt that maps to `SECURITY.md` scope.
- **Honest tier support copy** — `hosted/plans.json` `support` strings become
  truthful and drop the shared-Slack promise:
  - Free → `Community · GitHub issues`
  - Team → `Email · best-effort, ~2 business days`
  - Enterprise → `Priority email` (a contractual SLA is added back only if and
    when one is actually signed)
  The pricing page and features page already render `tier.support` verbatim, so
  no `web/` code edit is needed — but the rendered output must be re-verified.
- **`docs/support.md`** — the operator-side runbook: label scheme
  (`bug`, `triage`, `needs-info`, `wontfix`, `security`, `question`), the review
  cadence (e.g. triage sweep every N business days), how a billing/account email
  is handled and where it is recorded, and when something escalates from an
  issue to a direct email thread.
- **Cross-reference from `go-public-sequence`** — its "announcement readiness"
  step gains a checklist item pointing at this change so the templates and
  `SUPPORT.md` are confirmed live before the visibility flip.
- **README pointer** — a one-line "Support" section linking `SUPPORT.md`.

## Capabilities

No spec-level behavior changes. `plan-catalog` already requires "a support
description" per tier; changing the description text is data, not a requirement
change. The hosted API, CLI, adapters, execution path, and all product
behavior are untouched. Everything here is docs, repo configuration, and one
data-file copy edit. `skip_specs: true`.

### New Capabilities

_None._

### Modified Capabilities

_None._

## Impact

- **repo root:** new `SUPPORT.md`; one-line README "Support" section.
- **`.github/`:** new `ISSUE_TEMPLATE/bug_report.yml`, `feature_request.yml`,
  `config.yml`. If issue templates should also cover the `NAHA` repo, that is a
  separate, mirrored change — out of scope here.
- **`hosted/plans.json`:** three `support` string values. Covered by
  `PlanCatalogTests` / `plans.ts` shape checks — must still pass, and the
  pricing/features pages re-verified in a running build.
- **`docs/`:** new `docs/support.md`.
- **`openspec/changes/go-public-sequence/`:** one added checklist line
  (coordinate with that change's owner state; do not silently edit its tasks).
- **external:** none — no new mailbox until `company-and-domain-launch` lands
  `support@`; no helpdesk tool, no vendor sign-up.
- **no** change to the engine, adapters, hosted API contract, or web app code.

## Explicitly deferred

- A helpdesk / shared-inbox tool (Zendesk, Plain, HelpScout, etc.) — no volume
  to justify the cost or the second inbox to watch.
- Contractual response-time SLAs and any Enterprise Slack Connect channel.
- A public status page, an in-app support widget, GitHub Discussions / a
  community forum.
- Support-request analytics or any automation.
- Mirroring the issue templates into the `NAHA` repo.
