## Context

See proposal.md - Why. `integrations/github-action/render.mjs` already talks
to `api.github.com` directly via `fetch` with `GITHUB_TOKEN` (no SDK, no
`actions/github-script`) to post the PR comment and check run — the same
pattern extends naturally to posting on an Issue. The Bitbucket Pipe
(`integrations/bitbucket-pipe`) currently only runs the CLI container and
emits JUnit/summary output; it has no render/API step today, so ticket
write-back is genuinely new code there, not an extension of an existing
comment-poster. Azure Boards has no ReleaseTwin CI integration at all yet
(the closest is the raw `docs/ci.md` Azure Pipelines snippet) — there is
nothing to extend there either.

## Goals / Non-Goals

**Goals:**
- Reuse each platform's existing CI credential (`GITHUB_TOKEN`, a Bitbucket
  App password, an Azure PAT) — no new secret category introduced by this
  change.
- Keep the locator-parsing convention dumb and platform-native (an issue
  number, a work-item ID) rather than inventing a ReleaseTwin-specific syntax.
- Ship GitHub Issues first (extends existing, verified code) before Bitbucket
  or Azure, so each vendor's real API is confirmed against a live
  repo/project before its recipe ships — same standard as
  `flag-vendor-cookbook`.

**Non-Goals:**
- No generic "ticket tracker" abstraction/interface in `src/ReleaseTwin.Core`
  or `ReleaseTwin.AdapterSdk`. This is CI-integration glue, same layer as the
  existing PR-comment renderers, not an engine capability.
- No two-way sync (reading ticket state, transitioning/closing it).
- No Jira support in this change (no existing ReleaseTwin CI integration
  targets it; would need its own proposal to decide which auth model — cloud
  OAuth vs. API token vs. on-prem PAT — to support first).

## Decisions

**Locator convention: platform-native key, resolved against the CI-scoped
repo/project — not a new case-file field.** `oracle.locator` stays a plain
string. For GitHub, a locator matching `^#\d+$` resolves to that issue number
in `github.repository` (the same repo the Action is already running in). For
Bitbucket, the equivalent is the pipe's own `BITBUCKET_REPO_SLUG`/workspace.
Azure Boards work items are numeric but not `#`-prefixed by convention
anywhere else in this repo's docs — this needs one real decision during
implementation (a distinguishing prefix like `AB#123`, which is actually
Azure Boards' own native syntax for linking work items from commits/PRs, so
it doubles as a convention users may already know). Alternative rejected: a
structured `oracle: {tracker: ..., id: ...}` object in the case schema — this
would touch `core-execution`'s spec and the case JSON schema for a concern
that's entirely about CI-integration wiring, violating the core/adapter
boundary invariant for no real benefit (the platform is already implied by
which CI integration is running).

**GitHub Issues first, implemented as a new function in the existing
`render.mjs`, not a new package.** The GitHub Action already authenticates to
`api.github.com` with the right token and has a summary object in hand at
render time; issue write-back is one more `fetch` call (`POST
/repos/{owner}/{repo}/issues/{number}/comments`) using the same helper that
already posts the PR comment. Verified during design against GitHub's real
REST API docs for the comments endpoint (same auth model, same token scope
`issues: write`, already covered by the default `GITHUB_TOKEN` permissions
for a same-repo run).

**Bitbucket and Azure Boards write-back are separate, smaller follow-on
tasks, each gated on standing up a real verification target.** Per this
project's "verified, not just typed" standard, Bitbucket's issue-comment
endpoint and Azure Boards' work-item comment endpoint (`PATCH
_apis/wit/workitems/{id}` with a `System.History` field, or the dedicated
comments API) will each be confirmed against a real Bitbucket repo / Azure
DevOps org before their task is marked done — not assumed from documentation
memory, the same discipline that caught the GrowthBook/Unleash API
mismatches in `flag-vendor-cookbook`.

**Failure isolation: write-back runs after the run's own pass/fail is
already decided, and never mutates it.** The write-back step reads the
already-produced summary; a tracker API error is logged and swallowed at the
integration layer, mirroring how `render.mjs`'s PR-comment/check-run calls
already don't fail the job on API errors today.

## Risks / Trade-offs

- [A locator collides with an unrelated issue number in a different
  repo/project if a case file is copied between projects without updating
  `oracle.locator`] → resolution is always scoped to the CI run's own
  repo/project (never a locator-embedded repo reference), so a stale locator
  either hits the wrong issue in the *same* repo (a real but narrow risk,
  same class as a human mistyping a ticket reference) or 404s harmlessly if
  the number doesn't exist — never cross-posts to an unrelated repo.
- [Azure Boards convention choice (`AB#123`) is guessed now and could be
  wrong once real API verification happens] → flagged as an Open Question
  below rather than locked in; GitHub's convention ships first and is
  real-verified, so nothing here blocks starting implementation.
- [Posting automated comments to tickets could be noisy for teams running
  ReleaseTwin on every commit rather than just PRs] → mitigated by the
  opt-in default (spec requirement: disabled unless explicitly enabled).

## Migration Plan

None — purely additive, opt-in, no existing behavior changes to any
integration's default output.

## Open Questions

- Exact Azure Boards locator prefix/convention (`AB#123` vs. a bare number
  vs. something else) — to be settled when that task is picked up and
  verified against a real Azure DevOps org, per the Bitbucket/Azure decision
  above. Doesn't change the GitHub-first implementation or the spec (the spec
  only requires *a* documented, non-colliding convention per tracker).
