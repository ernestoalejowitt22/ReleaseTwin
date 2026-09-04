## Why

`docs/ci.md` already links to real, verified proof that ReleaseTwin runs outside GitHub
Actions — a live Bitbucket Pipelines build, a live Azure Pipelines build, the public GHCR
image, the NuGet package — but every one of those is a bare text link. The
`feature-proof-showcase` change just applied this project's own "the artifact is the
deliverable" bar to the hosted dashboard's marketing pages (real screenshots, not
descriptions); `docs/ci.md` — the doc that makes the strongest "works everywhere" claim in
this repo — is held to a lower bar than the dashboard it links out to. A reader has to
leave the doc and trust an unfamiliar site's UI to verify any of these claims.

## What Changes

- Add a real screenshot of the GitHub Action's PR-annotation comment, captured from an
  actual comment on a merged PR in this repo (`.github/workflows/pr-annotations.yml`
  dogfoods the Action on every PR) — replacing prose-only description of what the comment
  looks like.
- Add a real screenshot of the NuGet package page (`nuget.org/packages/releasetwin`)
  next to the existing `dotnet tool install` instructions.
- Add a real screenshot of the GHCR package page (`ghcr.io/ernestoalejowitt22/releasetwin`)
  next to the existing `docker run` instructions.
- Add real screenshots of the two already-linked live CI runs — Bitbucket Pipelines
  [build #1](https://bitbucket.org/releasetwin/releasetwin-ci-example-projects/pipelines/results/1)
  and Azure Pipelines
  [build #239](https://ernestotesting.visualstudio.com/My%20First%20Project/_build/results?buildId=239)
  — next to the existing "proven live" paragraph, so the passing status is visible without
  a click.
- All five screenshots are of third-party sites (nuget.org, GitHub Packages/GHCR,
  bitbucket.org, dev.azure.com) this repo's own CI has no way to regenerate — unlike the
  hosted-dashboard screenshots in `feature-proof-showcase`, these are one-time, manually
  captured PNGs committed under `docs/assets/ci/`, not part of any capture pipeline. The
  existing text links stay as the source of truth; a doc comment notes the screenshots may
  drift from the live page and links win on conflict.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — `docs/ci.md` prose and the linked live-proof claims are unchanged; this only adds
visual evidence alongside them, no behavior or requirement changes)

This change sets `skip_specs: true`: it adds documentation screenshots for already-true,
already-linked claims. No API, CLI, or CI-integration behavior changes.

## Impact

- **`docs/ci.md`**: five new `![alt](...)` image embeds, no prose rewrites beyond what's
  needed to introduce each image.
- **New directory `docs/assets/ci/`**: five committed PNGs (~200KB–1MB combined, typical
  full-page/panel screenshot sizes).
- **No code changes.** No CI, build, or test impact.
- **No manual user steps** — screenshots are captured once during this change (via browser
  automation against the already-public pages) and committed; no credential or account
  needed since none of the five pages require authentication to view.
