## MODIFIED Requirements

### Requirement: All cases in a directory are executed and reported
The CLI SHALL load every case file in a given directory, execute each with `CaseExecutor`, and print a per-case result (pass/fail, classification if failed) plus an overall summary.

The CLI SHALL dispatch on a leading subcommand: `init` and `new` invoke case scaffolding (see the `case-scaffolding` capability); `run` executes cases and accepts an optional cases-directory path and an optional `--journey <journeyId>@<version>`. An invocation with no recognized subcommand SHALL behave as it did before subcommands existed — a leading `--journey <journeyId>@<version>` runs that pinned hosted journey, otherwise the first argument (or a documented default when absent) is the cases directory to execute. `--help` SHALL list the subcommands.

The CLI SHALL accept an optional `--summary-json <path>` flag (or the `RELEASETWIN_SUMMARY_JSON` environment variable). When set, after the run completes — whether it passed or failed — the CLI SHALL write a versioned JSON run summary to that path in addition to its normal human-readable output. The summary SHALL contain a schema version, the overall pass/fail result, per-outcome totals, the flag-proof tallies, and a per-case list of id, outcome, failure classification, flag-proof result, and release label. The summary SHALL contain only metadata the CLI already reports — no fixture content, response bodies, or credential values. When the flag and environment variable are both unset, no summary file SHALL be written and behavior SHALL be identical to before this option existed.

#### Scenario: Mixed pass/fail run reports both
- **WHEN** a directory contains one case that passes and one that fails
- **THEN** the CLI's output shows both individual results and a summary indicating one passed and one failed

#### Scenario: Legacy invocation without a subcommand still runs cases
- **WHEN** the CLI is invoked with only a directory path, with no arguments, or with a leading `--journey <journeyId>@<version>`
- **THEN** it executes cases (or the pinned journey) exactly as it did before subcommand dispatch was added

#### Scenario: Unknown subcommand or --help lists the subcommands
- **WHEN** the CLI is invoked with `--help`
- **THEN** it prints usage listing at least `init`, `new`, and `run`

#### Scenario: --summary-json writes a machine-readable summary
- **WHEN** the CLI runs with `--summary-json out.json`
- **THEN** after the run, `out.json` contains the schema version, overall result, totals, flag-proof tallies, and the per-case list

#### Scenario: The summary is written even when the run fails
- **WHEN** the CLI runs with a summary path set and at least one case fails
- **THEN** the summary file is still written and its overall result is `failed`

#### Scenario: No summary flag means no summary file
- **WHEN** the CLI is invoked with neither `--summary-json` nor `RELEASETWIN_SUMMARY_JSON`
- **THEN** no summary file is written and output is identical to before this option existed
