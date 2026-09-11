## ADDED Requirements

### Requirement: The CLI can upload an existing JUnit report

The CLI SHALL provide a verb that uploads an existing JUnit XML file to the hosted platform's JUnit
ingest endpoint, so a suite that already emits JUnit can reach the dashboard without a case file
being authored. The verb SHALL take the path to the file and MAY take an optional release label,
which the CLI SHALL pass through to the platform.

The file SHALL be uploaded as received. The CLI SHALL NOT parse, rewrite, validate, or reformat its
contents, so the platform remains the single place JUnit dialect differences are interpreted.

The verb SHALL resolve the platform URL and API token the same way the rest of the CLI's hosted
calls do. An absent token SHALL be a clear error, not a silent no-op — unlike a run, where uploading
is an optional side effect, here the upload is the entire purpose of the command.

#### Scenario: An existing report is uploaded

- **WHEN** the upload verb is invoked with a path to a JUnit XML file and an API token is configured
- **THEN** the file's bytes are sent to the platform's JUnit ingest endpoint unmodified

#### Scenario: A release label is passed through

- **WHEN** the upload verb is invoked with a release label
- **THEN** the platform receives that label alongside the uploaded report

#### Scenario: The contents are not interpreted locally

- **WHEN** the uploaded file uses a JUnit dialect the CLI has never seen
- **THEN** the CLI uploads it unchanged rather than rejecting it, leaving acceptance to the platform

#### Scenario: A missing token is an error, not a silent success

- **WHEN** the upload verb is invoked with no API token configured
- **THEN** the CLI reports that a token is required and exits non-zero, uploading nothing

#### Scenario: A missing file is an error

- **WHEN** the upload verb is invoked with a path that does not exist
- **THEN** the CLI reports the path it could not read and exits non-zero, making no network call

### Requirement: The upload verb reports the platform's outcome

On success the CLI SHALL report how many test cases the platform recorded and where the run history
can be viewed. On failure it SHALL surface the platform's own rejection message rather than a
generic error, because that message names which limit or malformation caused the rejection, and
SHALL exit non-zero.

This differs deliberately from an upload during a run, where a failure is a warning that leaves the
exit code untouched: there, the run's local result is the outcome that matters; here, the upload is
the outcome.

#### Scenario: A successful upload reports what was recorded

- **WHEN** the platform accepts an uploaded report
- **THEN** the CLI prints the number of test cases recorded and the run-history URL, and exits zero

#### Scenario: A rejection is surfaced verbatim

- **WHEN** the platform rejects an uploaded report because it exceeds a limit or is malformed
- **THEN** the CLI prints the platform's own explanation and exits non-zero

#### Scenario: Upload failure fails the command

- **WHEN** the upload cannot be completed for any reason
- **THEN** the CLI exits non-zero, unlike an upload attempted as part of a run

### Requirement: The upload verb is dispatched before the run fallthrough

The CLI's argument dispatch treats an unrecognized leading argument as a directory of cases to run.
The upload verb SHALL be matched before that fallthrough, so invoking it never silently attempts to
execute the named path as a case directory.

#### Scenario: The verb is not mistaken for a case directory

- **WHEN** the upload verb is invoked
- **THEN** the CLI performs an upload and does not attempt to run cases from any directory
