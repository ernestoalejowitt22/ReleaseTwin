## ADDED Requirements

### Requirement: A GitLab CI/CD Component runs the CLI and feeds GitLab's native test widget

The repository SHALL provide an open-source GitLab CI/CD Component, under
`integrations/gitlab-component/`, that a GitLab pipeline can consume with a single
`include:` entry. The component SHALL run the ReleaseTwin CLI over a
caller-specified cases path, request a JUnit report from it, and expose that
report to GitLab as `artifacts:reports:junit` so the merge-request test widget
and the pipeline **Tests** tab populate from the run with no further wiring.

The component SHALL gate the job the way the CLI's own exit code does — a case
failure or an adverse flag-proof verdict SHALL fail the job, so the component can
be a required check on the target branch.

The component SHALL require only credentials the caller supplies for the run
itself (flag-source credentials, an optional hosted API token). It SHALL NOT
require a ReleaseTwin account or hosted call to produce the test widget, and it
SHALL NOT require any GitLab API token — the merge-request test widget is
populated entirely by the JUnit artifact.

The component's source SHALL be licensed Apache-2.0, independently of the
engine's copyleft licence, consistent with `integrations/github-action/`.

The component SHALL be usable by a direct `include:` reference to a pinned ref
even before it is published to the GitLab CI/CD Catalog; catalog publication is
an operational step, not a precondition for the component working.

#### Scenario: The component populates the GitLab test widget

- **WHEN** a GitLab pipeline includes the component pointed at a cases directory
  and the pipeline runs
- **THEN** the merge request's test widget and the pipeline Tests tab show one
  result per case, with failures reflecting the CLI's outcomes and flag-proof
  verdicts (including a flag-proof case that could not be paired)

#### Scenario: A failing run fails the job

- **WHEN** the CLI run under the component has any case failure or an adverse
  flag-proof verdict
- **THEN** the component's job exits non-zero

#### Scenario: The component needs no ReleaseTwin credentials for the widget

- **WHEN** the component's inputs and required variables are inspected
- **THEN** producing the test widget requires only the JUnit artifact the CLI
  writes, with no ReleaseTwin API token or hosted endpoint referenced

#### Scenario: The component references no GitLab API token

- **WHEN** the component's template and documented variables are inspected
- **THEN** no GitLab personal, project, or job token is required or referenced —
  the run produces the widget through the JUnit artifact alone

#### Scenario: The component is Apache-2.0

- **WHEN** the licensing of `integrations/gitlab-component/` is inspected
- **THEN** it is Apache-2.0, separate from the engine's licence

### Requirement: The CI documentation shows consuming the JUnit report on the other major platforms

`docs/ci.md` SHALL document how to consume the CLI's JUnit report through the
native test-results step of Bitbucket Pipelines, CircleCI, and Azure Pipelines,
with a copy-pasteable snippet for each, so a user on any of those platforms gets
a native test view without any ReleaseTwin-authored per-platform integration.

The documentation SHALL make clear that these snippets rely only on the CLI's
`--junit-xml` output and each platform's own test-report ingestion — there is no
ReleaseTwin package to install for them.

#### Scenario: Each platform has a snippet

- **WHEN** `docs/ci.md` is inspected
- **THEN** it contains a working snippet for each of Bitbucket Pipelines,
  CircleCI, and Azure Pipelines that runs the CLI with `--junit-xml` and wires
  the output into that platform's native test-results step

#### Scenario: The docs state no package is needed for these platforms

- **WHEN** the portability section of `docs/ci.md` is read
- **THEN** it states that the Bitbucket / CircleCI / Azure paths need only the
  JUnit output and the platform's own ingestion, with no ReleaseTwin package
