## Context

The CLI already writes one optional machine-readable artifact — the JSON run
summary — behind `--summary-json` / `RELEASETWIN_SUMMARY_JSON`, extracted from
`runArgs` in `CliEntrypoint.ExtractSummaryJson`, accumulated by
`RunSummaryBuilder`, and written by `RunSummaryWriter` (which also owns
`ValidateDestination`). Flag-proof outcomes are the `FlagProofOutcome` enum in
`src/ReleaseTwin.Core/FlagProof.cs` (`Passed`, `WeakOracle`, `BothFailed`,
`Inverted`, `Ineligible`, `ControlFailed`, `ControlUnverified`). See
proposal.md — Why for motivation.

## Goals / Non-Goals

**Goals:**

- A second optional CLI output — JUnit XML — sharing the summary's flag/env seam
  and directory-validation behaviour, with an honest, total mapping from
  ReleaseTwin outcomes to JUnit pass/failure.
- One packaged platform integration (GitLab CI/CD Component) that turns that
  output into a native test view, mirroring the GitHub Action's role and licence.
- Doc snippets that make Bitbucket / CircleCI / Azure work with zero
  ReleaseTwin-authored code.

**Non-Goals:**

- Any change to `ReleaseTwin.Core` or an adapter. This is CLI output only.
- A new JSON schema version — the JUnit report is a separate file; the summary
  JSON is untouched.
- Reusing JUnit as an internal model. It is a projection of data the CLI already
  has, produced at write time.
- Line-level MR/PR annotations on any platform (deferred in the proposal).

## Decisions

### D1 — JUnit XML as the portability primitive, not per-platform APIs

Every major CI platform ingests JUnit XML natively (`artifacts:reports:junit`,
`PublishTestResults`, `store_test_results`, the Jenkins `junit` step). Emitting
that one format buys a native test view on all of them from ~100 lines in the
CLI. Alternative — a rendering+API integration per platform, mirroring the GitHub
Action's comment+check — was rejected: N auth models and N renderers to maintain,
and nothing for platforms not yet built. The Action stays as the one place we pay
that cost, because GitHub's check-run semantics have no portable equivalent.

### D2 — A `JUnitReport` writer beside `RunSummary`, fed from the same per-case rows

Add `JUnitReportWriter` (+ `ValidateDestination`, same contract as
`RunSummaryWriter`) and extend the extraction in `CliEntrypoint` to also pull
`--junit-xml` / `RELEASETWIN_JUNIT_XML` (argument wins). The per-case data the
`RunSummaryBuilder` already collects (id, passed, classification, flag-proof
outcome name) is exactly what the JUnit projection needs, so the run loop feeds
both builders from one call site. Emit XML with `System.Xml` (e.g.
`XmlWriter`/`XDocument`) so escaping is handled by the framework, not by hand.

### D3 — The outcome → JUnit mapping

| ReleaseTwin outcome | JUnit | `message` |
|---|---|---|
| plain case passed | pass | — |
| plain case failed | `<failure>` | failure classification, else `"failed"` |
| flag-proof `Passed` | pass | — |
| flag-proof `WeakOracle` / `BothFailed` / `Inverted` | `<failure>` | the `FlagProofOutcome` name |
| flag-proof `Ineligible` / `ControlFailed` / `ControlUnverified` | `<failure>` | the `FlagProofOutcome` name |

Rationale: `WeakOracle`/`Inverted`/`BothFailed` are genuine oracle failures — the
build is not release-proven — so they must turn a pipeline red. `Ineligible` /
`ControlFailed` / `ControlUnverified` mean a flag-proof case *asked for* a paired
run and did not get one — no leg ran under a confirmed state. In the widget that
is a failure, not a silent skip: a case declared `flag_proof` and the pipeline
could not honour it, which a reviewer needs to see as red rather than as a
quietly-skipped row. This is deliberately stricter than the CLI's own exit code
(which today does not fail on `Ineligible`); `docs/ci.md` calls out that the
JUnit widget treats "flag proof requested but not performed" as a failure. There
is no `<skipped>` outcome in the mapping. A unit test asserts the mapping is
total over the enum, so a future `FlagProofOutcome` value fails the build until
it is classified.

### D4 — GitLab component shape

`integrations/gitlab-component/templates/releasetwin.yml` — a single-job template
with `spec:inputs:` for `cases-path`, `image` (default the pinned CLI image, same
value the Action defaults to), `stage`, and `job-name`. The job runs the CLI from
the container (`image:` with `entrypoint: [""]`, invoking
`dotnet /app/ReleaseTwin.Cli.dll` — the fixed publish path from the engine
Dockerfile), writes `junit.xml` (and `summary.json`) into the job workspace, and
declares:

```yaml
artifacts:
  when: always
  reports:
    junit: junit.xml
  paths: [junit.xml, summary.json]
```

`when: always` so the report is ingested even on a failing job. Non-zero CLI exit
fails the job.

**No MR note.** The design originally included an optional merge-request note via
`CI_JOB_TOKEN`, but a GitLab job token cannot create MR notes
(`POST …/merge_requests/:iid/notes` returns 401 for a job token — it is outside
the job-token API allowlist). Rather than require a user-supplied project access
token or ship a step that silently never works, the note is dropped from Phase 1:
GitLab's native test widget already shows every case result inline on the merge
request, so the note was redundant. A note gated on an explicit project token is
listed as deferred in the proposal.

README + `LICENSE` (Apache-2.0) alongside, matching `integrations/github-action/`.

### D5 — Catalog publication is out of the change

The component works via `include: { component: $CI_SERVER_FQDN/<path>@<ref> }` or
a raw `include: remote:` against this repo. Publishing to the GitLab CI/CD
Catalog needs a GitLab.com project with the catalog setting enabled and a
release-tag pipeline in *that* project — operational, tracked in tasks as a
user-owned step, not blocking the component's correctness.

## Risks / Trade-offs

- **JUnit can't express the full flag-proof verdict vocabulary** → the `message`
  attribute carries the exact `FlagProofOutcome` name, and `docs/ci.md` states
  the summary JSON / CLI output remain the source of nuance. The widget answers
  "is this build release-proven?", not "why not".
- **The widget is stricter than the CLI exit code for `Ineligible`** → a local
  run without flag-system credentials that is `Ineligible` still exits 0 from the
  CLI but shows red in a GitLab/CircleCI/Azure test widget. Mitigation:
  `docs/ci.md` states this explicitly, and the fix for a legitimately
  flag-proof-less environment is to not declare `flag_proof` on cases that run
  there, or to run those cases in a separate job. The stricter widget behaviour
  is the point — a `flag_proof` case that never gets a paired run is not
  evidence.
- **The GitLab component pins a CLI image that does not exist until the first
  release is cut** → real dependency, called out in the proposal as an
  out-of-scope prerequisite; the component and its tests can be written and
  reviewed against a local build in the meantime.
- **Two directory-validation code paths** (`RunSummaryWriter`,
  `JUnitReportWriter`) → factor the shared check into one helper both call.

## Migration Plan

Purely additive. No flag, no schema bump, no config change. A run that sets
neither `--junit-xml` nor `RELEASETWIN_JUNIT_XML` is unchanged. Rollback is
reverting the change; nothing persists.
