## ADDED Requirements

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
