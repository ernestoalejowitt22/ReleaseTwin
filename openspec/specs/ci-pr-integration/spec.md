# ci-pr-integration Specification

## Purpose

An open-source GitHub Action that runs the ReleaseTwin CLI with its machine-readable run summary enabled and renders that summary onto a pull request as a comment and a check run — using only the workflow's own token, with no hosted service involved.
## Requirements
### Requirement: A GitHub Action renders a run summary onto a pull request
The repository SHALL provide an open-source GitHub Action that runs the CLI with the
machine-readable summary enabled and renders that summary onto the pull request as a
comment and as a check run. The Action SHALL use only the workflow's own `GITHUB_TOKEN`
and GitHub's PR/Checks APIs — it SHALL NOT require a ReleaseTwin account, API token, or any
call to the hosted service.

The Action's source SHALL be licensed permissively (Apache-2.0), independently of the
engine's copyleft license, so it can be forked and adapted freely.

#### Scenario: A run produces a PR comment and a check run
- **WHEN** the Action runs on a pull request and the CLI finishes a run
- **THEN** a PR comment is present showing the pass/fail totals and the flag-proof verdict,
  and a check run named for ReleaseTwin reports the same outcome

#### Scenario: Re-running updates the existing comment in place
- **WHEN** the Action runs a second time on the same pull request
- **THEN** the existing ReleaseTwin comment is updated rather than a duplicate being posted

#### Scenario: The Action needs no ReleaseTwin credentials
- **WHEN** the Action's required inputs and permissions are inspected
- **THEN** it requires only `GITHUB_TOKEN` and does not reference a ReleaseTwin API token
  or hosted endpoint

#### Scenario: A failing run still posts the summary
- **WHEN** the CLI run fails (non-zero exit)
- **THEN** the Action still posts the comment and check run reflecting the failure, rather
  than aborting silently

### Requirement: The Action defaults to an immutable image reference

The published GitHub Action SHALL NOT default its runner image input to a mutable
tag (such as `latest` or a moving major-version tag). Its default SHALL be an
immutable reference — a specific released version or a content digest — that the
release process updates deliberately. When a caller explicitly supplies a mutable
tag, the Action SHALL surface a visible warning in the job log naming the risk;
it SHALL still run, so a caller who accepts the risk is not blocked.

#### Scenario: The default image is immutable

- **WHEN** the Action's input defaults are inspected
- **THEN** the image input defaults to a pinned version or digest, not `latest` or a bare major-version tag

#### Scenario: A caller-supplied mutable tag warns but runs

- **WHEN** a workflow invokes the Action with an image reference that is a mutable tag
- **THEN** the job log contains a warning that the image is not pinned, and the run proceeds

#### Scenario: The release process updates the pinned default

- **WHEN** a new CLI image version is released
- **THEN** the release process updates the Action's default image reference to the new immutable reference

### Requirement: The Action documents the fork-pull-request secret boundary

The Action's documentation SHALL state explicitly that any secrets passed to the
CLI container (via the env-file or forwarded environment variables) are exposed
to the case files it runs, and that a workflow which runs the Action on
pull requests from forks MUST NOT make ingest or other sensitive secrets
available to it.

#### Scenario: The docs carry the fork-PR warning

- **WHEN** the Action's README is inspected
- **THEN** it contains an explicit warning that case files can read any secret handed to the container and that fork-PR workflows must withhold sensitive secrets

### Requirement: The run summary MAY carry dashboard URLs and the Action renders them

The machine-readable run summary MAY carry an optional run-level dashboard URL
and optional per-case evidence URLs, populated by the CLI only when a report
upload succeeded. The summary's `schemaVersion` SHALL be incremented; a consumer
that ignores unknown fields SHALL be unaffected, and a run with no upload SHALL
produce a summary whose only difference from the prior version is the version
integer.

When the summary carries a run-level URL, the Action SHALL render it as a link in
the pull-request comment and SHALL set the check run's details URL to it. When a
case entry carries an evidence URL, the Action SHALL render that case's row in the
comment as a link to it. When the summary carries neither, the comment and check
run SHALL be byte-for-byte what they are today.

The Action SHALL still require only `GITHUB_TOKEN`; the URLs are read from the
summary file the CLI already produced, and their absence is the normal
no-ReleaseTwin-account path.

#### Scenario: A summary with a run URL produces a linked comment and check

- **WHEN** the Action renders a summary that carries a run-level dashboard URL
- **THEN** the PR comment contains a link to that URL and the check run's details URL is set to it

#### Scenario: A failing case with an evidence URL links from its row

- **WHEN** the summary's entry for a failed case carries an evidence URL
- **THEN** that case's row in the PR comment links to the evidence URL

#### Scenario: A summary with no URLs renders exactly as before

- **WHEN** the Action renders a summary produced by a run that performed no upload
- **THEN** the PR comment and check run are identical to the output before this capability existed

#### Scenario: The Action still needs no ReleaseTwin credentials

- **WHEN** the Action's required inputs and permissions are inspected
- **THEN** it still requires only `GITHUB_TOKEN` and does not reference a ReleaseTwin API token or hosted endpoint

### Requirement: The Action is consumable by a stable major-version reference
The repository SHALL maintain a floating `v<major>` git tag (and a
`v<major>.<minor>` tag) that is updated to point at each verified release, so a
workflow that pins the Action with
`uses: <repo>/integrations/github-action@v<major>` resolves to the latest
release compatible with that major version. While the project is pre-1.0 the
current major is `0` (`@v0`).

The floating tags SHALL be updated only after the release's build-and-test gate
passes — they SHALL never point at an unverified commit.

The Action's documentation SHALL present a fully pinned reference (e.g.
`@v0.2.0`) as the recommended form for CI and the `@v<major>` form as the
convenience alternative, and SHALL state that the Action's `image` input must
reference a publicly pullable registry tag.

#### Scenario: Pinning the Action to the major version resolves
- **WHEN** a workflow uses the Action with `@v<major>` after at least one release of that major version has been published
- **THEN** the reference resolves to that release's commit and the Action runs

#### Scenario: The floating tag tracks a later patch release
- **WHEN** a subsequent patch release of the same major version is published and its build-and-test gate passes
- **THEN** the `v<major>` tag is updated to the new release commit, and a workflow pinned to `@v<major>` picks it up on its next run

#### Scenario: A failed release does not move the floating tag
- **WHEN** a release's build or test suite fails
- **THEN** the `v<major>` and `v<major>.<minor>` tags are left pointing at the previous verified release

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
