## MODIFIED Requirements

### Requirement: All cases in a directory are executed and reported
The CLI SHALL load every case file in a given directory, execute each with `CaseExecutor`, and print a per-case result (pass/fail, classification if failed) plus an overall summary.

The CLI SHALL dispatch on a leading subcommand: `init` and `new` invoke case scaffolding (see the `case-scaffolding` capability); `run` executes cases and accepts an optional cases-directory path and an optional `--journey <journeyId>@<version>`. An invocation with no recognized subcommand SHALL behave as it did before subcommands existed — a leading `--journey <journeyId>@<version>` runs that pinned hosted journey, otherwise the first argument (or a documented default when absent) is the cases directory to execute. `--help` SHALL list the subcommands.

#### Scenario: Mixed pass/fail run reports both
- **WHEN** a directory contains one case that passes and one that fails
- **THEN** the CLI's output shows both individual results and a summary indicating one passed and one failed

#### Scenario: Legacy invocation without a subcommand still runs cases
- **WHEN** the CLI is invoked with only a directory path, with no arguments, or with a leading `--journey <journeyId>@<version>`
- **THEN** it executes cases (or the pinned journey) exactly as it did before subcommand dispatch was added

#### Scenario: Unknown subcommand or --help lists the subcommands
- **WHEN** the CLI is invoked with `--help`
- **THEN** it prints usage listing at least `init`, `new`, and `run`
