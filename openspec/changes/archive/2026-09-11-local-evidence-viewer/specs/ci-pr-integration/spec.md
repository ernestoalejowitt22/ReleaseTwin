## ADDED Requirements

### Requirement: The Action can attach a run's evidence to the workflow as an artifact

The GitHub Action SHALL provide an opt-in input that captures evidence for the run and
uploads the single-file evidence export as a workflow artifact, so a pull request's evidence
is reachable from the check without an account, a hosted upload, or a local checkout.

The input SHALL default to off, leaving the Action's existing behavior and its produced
artifacts unchanged for every workflow that does not set it. When it is on, the Action SHALL
capture evidence to a location inside the runner's own temporary space — never into the
checked-out workspace, which it mounts read-only — and upload the export from there.

Enabling it SHALL NOT cause evidence to be uploaded to any hosted service: the capture is
local to the runner and the export leaves only as a workflow artifact.

#### Scenario: Evidence is uploaded as an artifact when enabled

- **WHEN** a workflow runs the Action with the evidence input enabled and the run produces
  evidence
- **THEN** the single-file evidence export is uploaded as a workflow artifact for that run

#### Scenario: The default is unchanged behavior

- **WHEN** a workflow runs the Action without setting the evidence input
- **THEN** no evidence is captured and the Action's artifacts and outputs are exactly as before

#### Scenario: Capture never writes into the checked-out workspace

- **WHEN** the Action captures evidence
- **THEN** it writes it under the runner's temporary directory, leaving the mounted workspace
  unmodified

#### Scenario: Enabling evidence sends nothing to a hosted service

- **WHEN** the Action runs with the evidence input enabled and no ReleaseTwin API token
  configured
- **THEN** the run completes, the artifact is uploaded, and no hosted evidence upload is attempted

### Requirement: An evidence upload failure does not change the reported run result

A failure to capture, export, or upload the evidence artifact SHALL be surfaced as a warning
and SHALL NOT alter the run's pass/fail outcome, the pull-request comment, the check run, or
the job's conclusion.

#### Scenario: Export failure leaves the verdict intact

- **WHEN** the run completes but producing or uploading the evidence artifact fails
- **THEN** the comment and check run report the run's actual outcome, and the failure is
  surfaced as a warning rather than changing that outcome
