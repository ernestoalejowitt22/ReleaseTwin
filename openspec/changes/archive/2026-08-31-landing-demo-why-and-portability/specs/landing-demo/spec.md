## MODIFIED Requirements

### Requirement: The demo assets are derived from a real pipeline run

The landing demo's PR-gate and PR-comment panels SHALL be generated from the actual run
summaries the ReleaseTwin CLI produced for a real pull request, using the same content
model as `integrations/github-action/` — not hand-authored mock markup or invented run
data. The dashboard panels SHALL be screenshots of the real hosted dashboard rendering the
resulting run.

The demo's non-GitHub CI panel SHALL also be derived from a real ReleaseTwin CLI run
against the same `--summary-json` contract: either the Bitbucket Pipelines YAML snippet
that invokes the CLI as documented in `/docs/ci`, or a captured pipeline-log render of an
actual CLI gate run (its real stdout and exit status), or both. It SHALL NOT be a
hand-authored mock of a Bitbucket pull request and SHALL NOT be styled or captioned as a
Bitbucket pull-request UI.

#### Scenario: The PR comment panel reflects the real run summary

- **WHEN** the landing demo shows the ReleaseTwin PR comment
- **THEN** its totals, flag-proof line, and notable-cases table are those of a real
  ReleaseTwin run summary, matching what `integrations/github-action/` would render for it

#### Scenario: The dashboard panels are real dashboard screenshots

- **WHEN** the landing demo shows the run history, evidence viewer, trend analytics, and
  release-readiness rollup
- **THEN** each is a screenshot of the actual hosted dashboard rendering that run

#### Scenario: The non-GitHub CI panel reflects a real CLI gate run

- **WHEN** the landing demo shows a non-GitHub CI surface
- **THEN** it shows the documented Bitbucket Pipelines snippet and/or a pipeline-log
  render of a real CLI run's stdout and exit status, not a mocked Bitbucket pull-request
  screenshot

### Requirement: A committed script regenerates the demo assets

The repository SHALL provide a script that regenerates every landing-demo asset from a
pipeline run and the hosted dashboard, so a change to the Action's output format or the
dashboard UI can be reflected by re-running it rather than hand-editing images. The
script SHALL document the credentials it needs and SHALL reuse the existing e2e
credential sources rather than introducing new ones.

Where the non-GitHub CI panel is a captured pipeline-log render (rather than the
static YAML snippet), the script SHALL regenerate that asset too from a real CLI run, so
a change to the CLI's gate output is reflected by re-running it. Where the panel is only
the YAML snippet, it SHALL be sourced from the same snippet shown in `/docs/ci` rather
than duplicated.

#### Scenario: Regenerating after a renderer change

- **WHEN** `integrations/github-action/render.mjs` changes the PR comment format
- **THEN** re-running the capture script produces updated demo images with no manual
  image editing

#### Scenario: Regenerating the non-GitHub panel

- **WHEN** the non-GitHub CI panel is a captured pipeline-log render and the CLI's gate
  output format changes
- **THEN** re-running the capture script produces an updated panel with no manual image
  editing, and the Bitbucket Pipelines snippet on the landing page stays identical to the
  one in `/docs/ci`

### Requirement: Captured evidence contains only test data

The demo SHALL be captured against test data only — the NAHA e2e environment and seeded
fixtures. Before an asset is committed it SHALL be reviewed so that no real customer
data, real credentials, or real operator content is visible in any panel. This review
SHALL cover the non-GitHub CI panel's pipeline-log output as well.

#### Scenario: A capture surfacing a secret is not committed

- **WHEN** a captured screenshot, PR artifact, or pipeline-log render shows a
  credential-shaped value or non-test content
- **THEN** that asset is re-captured with different data or excluded, and is not committed
