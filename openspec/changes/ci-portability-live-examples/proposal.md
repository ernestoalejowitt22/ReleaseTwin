## Why

`ci-report-portability` (PR #121, un-merged) shipped a JUnit-XML reporter, an
Apache-2.0 GitLab CI/CD Component, and `docs/ci.md` snippets for Bitbucket
Pipelines, CircleCI, and Azure Pipelines — but **nothing runs those snippets**.
They are copy-paste guesses. The `spa-ui-adapter-ergonomics` and
`express-flag-proof-example` work also drags Node/React/Angular toolchains into
the .NET engine repo, a cost flagged in both designs.

One move fixes both: put the runnable demo apps in a **separate examples repo**
and drive them through **real Bitbucket Pipelines and Azure Pipelines** (plus
GitHub Actions), using the **published CLI container**. That turns the untested
snippets into a living, linkable proof and a strong funnel artifact ("a real
Bitbucket repo release-proofing an Angular app"), and gets the front-end
toolchains out of the engine repo.

## What Changes

- **New repo `releasetwin-ci-examples`** — GitHub-canonical, auto-mirrored to a
  Bitbucket repo and an Azure DevOps repo. Contents:
  - `apps/express-demo/`, `apps/react-demo/`, `apps/angular-demo/` — the three
    demo apps (moved out of the engine repo).
  - `cases/`, `fixtures/`, `releasetwin.yml` — the ReleaseTwin case files
    (vendored from the engine repo at a pinned tag; a sync check keeps them
    honest).
  - `bitbucket-pipelines.yml`, `azure-pipelines.yml`, `.github/workflows/ci.yml`
    — one pipeline per platform: build the apps, serve the built output, run the
    ReleaseTwin CLI (HTTP flag-proof + contract + **browser** SPA journeys) with
    `--junit-xml`, and publish the result to each platform's native test report.
- **Release the JUnit reporter first** — merge PR #121 (`ci-report-portability`),
  cut a CLI release whose container/tool carries `--junit-xml`, and have the
  pipelines pin that version.
- **Browser cases in external CI** — the pipelines install Chromium (approach
  chosen in design: a Playwright base image + the `dotnet tool`, or a
  browser-capable CLI image) and run the React/Angular journeys, not just HTTP.
- **Engine repo slims down:**
  - `spa-ui-adapter-ergonomics` keeps only the code (`ui.assertText`,
    `ui.waitFor` URL mode — already implemented) + `docs/spa-testing.md`; its
    demo apps and CI job are dropped in favour of the examples repo.
  - `express-flag-proof-example` (already committed on its branch): its
    `examples/express-demo/` app and the `express-example` GitHub Actions job
    move to the examples repo; the `examples/cases-express/` YAML stays as the
    copy-paste reference.
  - `examples/cases-express/` and `examples/cases-spa/` case YAML remain in the
    engine repo as the canonical reference the examples repo vendors.
- **`docs/ci.md`** — replace the speculative Bitbucket/Azure snippets with the
  real pipeline files, linked to the live examples repo and its green runs.

## Capabilities

### New Capabilities
_None in the engine._

### Modified Capabilities
- `cli-packaging`: the published CLI distribution MUST be usable to run
  **browser (`ui.*`) cases in a third-party CI system** — i.e. there is a
  documented, supported way (an image variant, a documented base-image + tool
  recipe, or bundled browsers) to run the UI adapter from the release artifact,
  not only from a source checkout. Exact form decided in design.

## Impact

- **New infrastructure:** a GitHub repo, a Bitbucket account + repo, an Azure
  DevOps repo (the existing org can host it). Mirror automation (a GitHub Actions
  job pushing to both remotes, using stored credentials). CI minutes on three
  platforms.
- **Release:** one CLI release cut from `main` after PR #121 merges. Possibly a
  new container tag/variant if design picks that route (`release.yml` change).
- **Engine repo:** `docs/ci.md` rewrite; removal of `examples/express-demo/` and
  the `express-example` job from the `express-flag-proof-example` branch before
  it merges; `spa-ui-adapter-ergonomics` scope reduction. No `src/` change beyond
  whatever `cli-packaging` resolution requires.
- **Credentials:** Bitbucket + Azure DevOps repo push tokens stored as GitHub
  Actions secrets for the mirror job. No new secrets in the engine repo.
- **Cross-repo maintenance:** the case-file vendoring + sync check is the main
  ongoing cost.
