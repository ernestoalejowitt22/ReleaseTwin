## Context

`docs/ci.md` already names five real, already-public proof points with no screenshot
behind any of them: the NuGet package (`nuget.org/packages/releasetwin`), the GHCR image
(`ghcr.io/ernestoalejowitt22/releasetwin`), a PR-annotation comment from
`integrations/github-action/` (dogfooded on every PR to this repo via
`.github/workflows/pr-annotations.yml`), and the two live CI runs already linked at the
bottom of the doc (Bitbucket build #1, Azure build #239). See proposal.md for why this
matters now.

Unlike `feature-proof-showcase`'s hosted-dashboard screenshots, none of these five pages
are ours to regenerate from CI — they're third-party UIs (GitHub, nuget.org, GHCR,
Bitbucket, Azure DevOps).

## Goals / Non-Goals

**Goals:**
- Every claim in `docs/ci.md`'s "works everywhere" narrative that currently ends in a bare
  link gets a real screenshot next to it.
- Screenshots are captured from the actual pages, not recreated/mocked.

**Non-Goals:**
- Any capture automation or CI wiring for these screenshots — see proposal.md Impact.
  They're a one-time, manually-triggered capture, refreshed by hand if a page's UI changes
  enough to look stale (not on a schedule).
- Marketplace listings (GitHub Marketplace, Azure DevOps extension, Bitbucket pipe) —
  separate future changes, per the original `feature-proof-showcase` proposal's Impact
  section.

## Decisions

**Which PR comment to screenshot: this repo's own dogfooded comment, not a customer's.**
Every one of this repo's own recent PR comments (checked: PR #117–#124) shows an identical
simple shape — `1 passed · 0 failed`, no flag-proof detail — because the repo's own CI
cases are small. A richer comment (a failing case, a populated flag-proof line) exists on
customer PRs (e.g. NAHA), but NAHA is private (see commercialization-readiness memory) and
not this repo's to screenshot for a public doc. Alternative considered: wait for or
construct a PR with a failing case in this repo purely to get a richer screenshot —
rejected as manufacturing evidence for a doc, the opposite of the "real artifact" bar this
change exists to raise. The plain passing comment is still real, live proof of the shape;
`integrations/github-action/README.md` already documents the richer fields in prose.

**Screenshots are viewport/element crops, not full-page captures**, matching each page's
meaningful content (the package header + install snippet for NuGet/GHCR, the comment body
for the PR annotation, the run status + job list for Bitbucket/Azure) — full-page captures
of nuget.org/GitHub/Azure DevOps chrome would mostly show navigation the reader doesn't
need and would date faster (nav redesigns) than the content itself.

**Storage: static PNGs under `docs/assets/ci/`, referenced by relative Markdown image
syntax.** Matches how the rest of `docs/` has no existing image convention to conflict
with; `docs/assets/ci/` groups these by their doc rather than a flat `docs/assets/`.

**Staleness handling: a single doc-level note, not a caption on every image.** The
proposal's Impact section already states the text links are the source of truth; repeating
"may be stale" under all five images would be noisy. One sentence near the top of the new
image block covers it once.

## Risks / Trade-offs

- **The pages can change their UI at any time** (nuget.org redesign, GitHub PR UI changes,
  Bitbucket/Azure DevOps UI changes) → mitigated by keeping the existing text links as the
  authoritative claim; the doc note says screenshots may lag the live page.
- **Build #1 / #239 are specific, static build results** — they don't grow stale in content
  (a finished build's page doesn't change), only in visual chrome around it, so this risk
  is lower than for the NuGet/GHCR pages which show current/latest package state.

**Deviation during capture: Azure build #239's results page requires Microsoft sign-in**,
unlike NuGet/GHCR/GitHub/Bitbucket (all publicly viewable with no auth). Headless capture
from this session was refused (never enter credentials into a login form, regardless of
authorization); the user signed in themselves and captured the Express demo job's
"Run release-proof cases" step log instead of the top-level run-status page. This is
arguably stronger evidence than the originally-planned run-status crop — it shows the
actual `dotnet run --project src/ReleaseTwin.Cli` invocation and its
`PASS` / `FLAGPROOF ... (Passed)` / `2 passed, 0 failed` output, i.e. ReleaseTwin CLI
literally running inside Azure Pipelines, not just a green checkmark next to a job name.
