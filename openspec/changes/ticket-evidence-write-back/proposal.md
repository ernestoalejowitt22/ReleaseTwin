## Why

A case's `oracle.locator` field already lets an author record which ticket a
test proves (e.g. "PROJ-456" or a Bitbucket issue number) — but that's just a
descriptive string today. Nothing in ReleaseTwin ever posts back to that
ticket. Evidence only reaches a PR/MR (`ci-pr-integration`'s GitHub comment,
GitLab MR widget) or a manually-shared link (`evidence-sharing`). For teams
that triage and close work in Jira, Azure Boards, GitHub Issues, or Bitbucket
issues — not in the PR itself — the run's pass/fail evidence never reaches the
place a reviewer actually looks to decide "is this ticket done." That's a real
gap in the audit trail this product is supposed to provide, and it's a
frequently-asked feature for teams whose review process is ticket-centric
rather than PR-centric.

## What Changes

- A run's evidence (verdict, links to the artifact/evidence bundle, pass/fail
  per case) gets posted as a comment on the ticket named by that case's
  `oracle.locator`, for the trackers ReleaseTwin's CI integrations already run
  in: GitHub Issues, Bitbucket issues, and Azure Boards work items. This is
  additive to (not a replacement for) the existing PR/MR comment.
- `oracle.locator` needs a documented, parseable convention per tracker (e.g.
  a bare `#123` / `PROJ-456`-style key resolved against the same repo/project
  the CI run is already in) so write-back can find the right ticket without
  new case-file fields. Locators that don't match the convention are skipped,
  not treated as errors — the field stays freeform for cases that don't want
  write-back.
- Ships as an opt-in step in the existing CI integration packages
  (`integrations/github-action`, the Bitbucket Pipe, the GitLab component's
  Azure Boards path if applicable) — not a core engine change. Each platform
  already has an integration boundary and a token with the right scope
  available in CI (`GITHUB_TOKEN` for Issues, an App password for Bitbucket, a
  PAT for Azure Boards); write-back reuses that rather than asking for new
  standing credentials beyond what each platform already needs.
- Explicitly deferred: Jira (no CI integration package targets it directly
  today — would need its own proposal), two-way sync (closing/transitioning
  the ticket, reading its state back into the run), and any tracker not
  already a first-class ReleaseTwin CI integration.

## Capabilities

### New Capabilities
- `ticket-evidence-write-back`: posting a run's evidence summary and links
  back to the source ticket identified by a case's `oracle.locator`, for
  GitHub Issues, Bitbucket issues, and Azure Boards work items; locator
  parsing convention; skip-not-fail behavior for unparseable or absent
  locators; reuse of each CI integration's existing credentials/scope.

### Modified Capabilities
- `ci-pr-integration`: the CLI's machine-readable run summary gains an
  optional per-case `oracleLocator` field (schema version bump), so a CI
  integration can resolve a write-back target without re-parsing case files.
  `core-execution`'s `oracle.locator` field itself is unchanged — this reads
  it, it doesn't redefine it.

## Impact

- New/extended code lives in the CI integration packages
  (`integrations/github-action`, `integrations/bitbucket-pipe`, and/or the
  GitLab component if Azure Boards write-back routes through it), not in
  `src/ReleaseTwin.Core` or the CLI.
- Docs: `docs/flag-proof.md`/`docs/ci.md` or a new `docs/ticket-evidence.md`
  documents the `oracle.locator` convention per tracker and the opt-in step.
- No new standing manual configuration beyond what each tracker's API already
  requires (e.g. Bitbucket App password scope, Azure PAT scope) — to be
  confirmed against each platform's real API during design, following this
  project's "verified, not just typed" standard (see `flag-vendor-cookbook`
  design.md for the precedent of dropping a vendor rather than shipping an
  unverified recipe).
