## Why

ReleaseTwin's only first-class CI integration is a GitHub Action. On every other
platform the CLI works (non-zero exit gates the job), but the run is invisible:
no test tab, no trend, no per-case breakdown, no merge-request annotation. That
is the wrong shape for an inbound OSS funnel — GitLab, Bitbucket, CircleCI, and
Azure Pipelines users find tools in *their* platform's catalog, and a tool that
produces nothing their pipeline understands does not get adopted or shared.

Comparable OSS-funnel businesses (Sidekiq, Plausible, Judoscale) get real
discovery from exactly one channel type: a platform whose catalog surfaces the
tool to a captive audience. For CI, that means the GitLab CI/CD Catalog — and,
more cheaply, every platform's native test-report ingestion, which is a single
well-known format: JUnit XML.

## What Changes

- **The CLI emits a JUnit XML report** on demand (`--junit-xml <path>` / an env
  var, mirroring `--summary-json`). It carries one `<testcase>` per case, with
  flag-proof verdicts mapped honestly onto pass / failure (a flag-proof case that
  could not be paired is a failure) with the ReleaseTwin verdict name in the
  failure message. No file is written unless the
  flag is set; existing output is unchanged.
- **A GitLab CI/CD Component** (`integrations/gitlab-component/`) that runs the
  CLI in a job and wires the JUnit report into `artifacts:reports:junit` so
  GitLab's MR test widget and pipeline Tests tab populate automatically, with no
  GitLab API token required. Apache-2.0, same as the Action.
- **`docs/ci.md` gains a portability section** with copy-paste snippets for
  Bitbucket Pipelines, CircleCI, and Azure Pipelines that consume the JUnit
  report through each platform's *native* test-results step — no per-platform
  integration code.
- The existing GitHub Action is **unchanged**. (It could later also surface the
  JUnit artifact, but that is out of scope here.)

### Phase 1 (this change)

JUnit XML reporter + GitLab CI/CD Component + doc snippets for Bitbucket /
CircleCI / Azure that ride the JUnit format.

### Explicitly deferred

- A packaged **Bitbucket Pipe**, **CircleCI Orb**, or **Azure DevOps task
  extension** — each is a publish/registry pipeline of its own; add one only when
  a real prospect runs on that platform and asks. The doc snippets cover the
  functional need until then.
- **Any merge-request note / comment from the GitLab component** — a GitLab job
  token cannot post MR notes, so a note would need a user-supplied project access
  token. The native test widget already shows every case result on the MR, so a
  note is deferred until a prospect asks and is willing to provision the token.
- **Rich PR-diff annotations** (GitLab MR line discussions, Bitbucket Code
  Insights, Azure PR status policies) — the JUnit widget is enough signal;
  line-level annotation is a large per-platform API surface.
- A **Jenkins** shared library — Jenkins consumes JUnit XML natively via the
  `junit` step; a doc snippet suffices and no prospect has named it.
- Changing the **GitHub Action** to also emit/attach the JUnit artifact.

## Capabilities

### New Capabilities

- `ci-report-formats`: The CLI's obligation to emit a portable, platform-neutral
  machine-readable test report (JUnit XML) describing a run — the case results
  and the flag-proof verdict mapping — written only on request, carrying no
  bodies or secrets, so any CI platform's native test-report ingestion can
  render a ReleaseTwin run.

### Modified Capabilities

- `ci-pr-integration`: today this capability is described purely in terms of *a
  GitHub Action*. It gains a requirement that the project also provides a GitLab
  CI/CD Component that runs the CLI, feeds the JUnit report into GitLab's native
  MR test widget, and needs only the job's own token — plus a requirement that
  `docs/ci.md` documents consuming the JUnit report on the other major platforms
  through their native steps. The GitHub Action requirements are unchanged.

## Impact

- **Code:** `src/ReleaseTwin.Cli` — a new reporter alongside the summary-JSON
  writer; a mapping from `FlagProofOutcome` / case classification to JUnit
  `testcase` state. No change to `ReleaseTwin.Core` or any adapter — this is a
  CLI output concern and does not touch the core/adapter boundary.
- **New files:** `integrations/gitlab-component/` (a `templates/*.yml` component +
  README + LICENSE), CLI tests for the reporter, and a JUnit-schema assertion
  test.
- **Docs:** `docs/ci.md` portability section; a short note in
  `integrations/github-action/README.md` pointing GitLab users to the component.
- **Distribution / manual steps (user):** to make the GitLab component
  discoverable it must live in a GitLab.com project published to the CI/CD
  Catalog — this needs a `gitlab.com` namespace/project (e.g. a mirror of this
  repo or a dedicated `releasetwin/releasetwin` project) and the catalog opt-in
  in that project's settings. Enumerated in tasks; the component is usable by
  direct `include:` reference even before it is cataloged.
- **Prerequisite, out of scope:** the GitLab component must pin a released CLI
  version, so a first `v*.*.*` release (NuGet + GHCR) must exist. Cutting that
  release — setting `NUGET_API_KEY`, tagging, ticking the Action Marketplace box
  — is tracked as a separate pure-ops checklist, not in this change's tasks.
- **`flags.json`:** unaffected (no feature flag).
- **Licensing:** the new `integrations/gitlab-component/` tree is Apache-2.0,
  consistent with `integrations/github-action/`.
