## Purpose

Defines the local CLI's behavior for composing adapters, running loaded cases, and reporting results — the minimum needed for a design partner to run this outside of a unit test, and for it to work as a CI gate.

## ADDED Requirements

### Requirement: Adapter credentials come from the environment
The CLI SHALL resolve adapter credentials (e.g. an Azure DevOps PAT) from environment variables at startup, never from a command-line argument or a committed file, consistent with the adapter-sdk requirement that adapters never hardcode credentials.

#### Scenario: Missing required environment variable is a clear startup error
- **WHEN** a required credential environment variable is not set
- **THEN** the CLI reports which variable is missing and exits without attempting to load or run any case

### Requirement: All cases in a directory are executed and reported
The CLI SHALL load every case file in a given directory, execute each with `CaseExecutor`, and print a per-case result (pass/fail, classification if failed) plus an overall summary.

#### Scenario: Mixed pass/fail run reports both
- **WHEN** a directory contains one case that passes and one that fails
- **THEN** the CLI's output shows both individual results and a summary indicating one passed and one failed

### Requirement: Exit code reflects overall pass/fail
The CLI SHALL exit with a non-zero status code if any executed case fails, and a zero status code if every case passes, so it can gate a CI pipeline without additional parsing of its output.

#### Scenario: Any failure produces a non-zero exit code
- **WHEN** at least one case in the run fails
- **THEN** the CLI process exits with a non-zero status code

#### Scenario: All-passing run produces a zero exit code
- **WHEN** every case in the run passes
- **THEN** the CLI process exits with a zero status code
