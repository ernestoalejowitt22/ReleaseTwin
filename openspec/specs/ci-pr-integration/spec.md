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

