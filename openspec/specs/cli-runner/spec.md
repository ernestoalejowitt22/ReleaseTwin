# cli-runner Specification

## Purpose

Defines the local CLI's behavior for composing adapters, running loaded cases, and reporting results — the minimum needed for a design partner to run this outside of a unit test, and for it to work as a CI gate.

## Requirements

### Requirement: Adapter credentials come from the environment or a hosted fetch
The CLI SHALL resolve each adapter's credentials (e.g. an Azure DevOps PAT, a LaunchDarkly API token) from environment variables at startup, never from a command-line argument or a committed file, consistent with the adapter-sdk requirement that adapters never hardcode credentials. Partial environment configuration for an adapter (some, but not all, of its required variables set) SHALL be reported as a clear startup error, exactly as before this capability existed. When an adapter's environment variables are entirely unset and a project API token is configured, the CLI SHALL additionally attempt to fetch that adapter's credentials from the hosted `adapter-credentials` capability before treating the adapter as not installed. When an adapter's full environment configuration is present, the CLI SHALL use it without attempting a hosted fetch.

#### Scenario: Partial environment configuration is a clear startup error
- **WHEN** some, but not all, of an adapter's required credential environment variables are set
- **THEN** the CLI reports which variables are missing and exits without attempting to load or run any case, regardless of whether a project API token is configured

#### Scenario: Full environment configuration is used without a hosted fetch
- **WHEN** an adapter's full set of credential environment variables is set
- **THEN** the CLI installs that adapter using those values, without attempting a hosted fetch — unchanged from this capability's absence

#### Scenario: A hosted-fetched credential is used when the environment has none
- **WHEN** an adapter's credential environment variables are entirely unset, a project API token is configured, and that project has stored credentials for that adapter
- **THEN** the CLI fetches and installs the adapter using the hosted-stored credentials

#### Scenario: An environment-supplied credential takes precedence over a hosted one
- **WHEN** an adapter's credential environment variables are fully set and that project also has different stored credentials for the same adapter
- **THEN** the CLI uses the environment-supplied values, not the hosted-stored ones

#### Scenario: Neither source configures the adapter
- **WHEN** an adapter's credential environment variables are entirely unset, and either no project API token is configured or the project has no stored credentials for that adapter
- **THEN** the CLI proceeds without installing that adapter — the same as today's behavior when credentials are entirely absent, not a startup error, since installing a credentialed adapter is optional

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

### Requirement: Multiple adapters compose in the CLI
The CLI SHALL be able to install more than one adapter into the same composition, so a case can reference operations from any installed adapter. An adapter that requires no credentials (e.g. a generic HTTP adapter) SHALL install successfully without any credential environment variables being set.

#### Scenario: Cases from two different adapters run in the same invocation
- **WHEN** the CLI is run with a cases directory containing one case using Azure DevOps operations and one case using generic HTTP operations
- **THEN** both cases execute successfully in the same run, using their respective adapters

### Requirement: Results are optionally uploaded to the hosted platform
If an API token is supplied via environment variable, the CLI SHALL upload each executed case's report to the ingest API after execution, mapped into the ingest contract. For a case run in flag-proof mode, the CLI SHALL upload its flag-proof result instead of a plain case report. If no token is supplied, the CLI SHALL run and report exactly as before, with no upload attempted and no error raised for its absence.

#### Scenario: Upload occurs when a token is configured
- **WHEN** the CLI runs with an API token supplied via environment variable
- **THEN** each executed case's report is uploaded to the ingest API after execution completes

#### Scenario: Flag-proof result is uploaded for flag-proof cases
- **WHEN** the CLI runs a case in flag-proof mode with an API token configured
- **THEN** the uploaded data is that case's flag-proof result, not a plain case report

#### Scenario: No upload is attempted without a token
- **WHEN** the CLI runs with no API token configured
- **THEN** it executes and reports cases exactly as it did before this capability existed, without attempting any upload and without treating the missing token as an error

### Requirement: Upload failure does not change the case's local result
A failure to upload a report (network error, rejected by the ingest API, etc.) SHALL be reported to the user as a distinct warning, but SHALL NOT alter the case's own pass/fail outcome or the CLI's exit code.

#### Scenario: Upload failure is a warning, not a case failure
- **WHEN** a case passes locally but its report fails to upload
- **THEN** the case is still reported and counted as passing, and the CLI's exit code still reflects only local execution outcomes; the upload failure is surfaced separately as a warning

### Requirement: Cases can declare flag-proof mode
A case file SHALL be able to declare flag-proof mode by specifying a feature key to toggle and a build identity. When a case declares flag-proof mode, the CLI SHALL run it via the paired known-bad/known-good mechanism instead of a single execution.

#### Scenario: Case file declares flag-proof mode
- **WHEN** a loaded case file specifies a feature key and build identity for flag-proof mode
- **THEN** the CLI runs that case as a flag-proof pair rather than a single execution

#### Scenario: Case file without flag-proof declaration runs as before
- **WHEN** a loaded case file does not declare flag-proof mode
- **THEN** the CLI runs it exactly as a single execution, unaffected by this capability existing

### Requirement: Flag-proof outcome is reported distinctly
For each case run in flag-proof mode, the CLI SHALL print the flag-proof outcome (passing, weak-oracle, both-failed, inverted, or ineligible), distinct from the plain pass/fail line used for ordinary cases.

#### Scenario: Flag-proof result is printed with its outcome
- **WHEN** a flag-proof case finishes running
- **THEN** the CLI output names that case's flag-proof outcome, not just a plain PASS or FAIL

### Requirement: Flag-proof outcome affects the overall exit code
A flag-proof case whose outcome is anything other than the discriminating pass outcome SHALL count as a failure for the CLI's overall exit code, on the same terms as an ordinary failed case.

#### Scenario: Weak-oracle flag-proof case produces a non-zero exit code
- **WHEN** a run contains only one case, in flag-proof mode, and its outcome is weak-oracle
- **THEN** the CLI process exits with a non-zero status code

#### Scenario: Passing flag-proof outcome does not block a zero exit code
- **WHEN** every case in the run — ordinary and flag-proof alike — passes (flag-proof cases with the discriminating pass outcome)
- **THEN** the CLI process exits with a zero status code

### Requirement: Flag-proof mode requires a capable adapter
If a case declares flag-proof mode but no installed adapter exposes feature-state control, the CLI SHALL report that case as ineligible rather than crashing or silently skipping it, and SHALL NOT count it as a passing case.

#### Scenario: No installed adapter supports feature-state control
- **WHEN** a case declares flag-proof mode and none of the adapters installed for the run expose feature-state control
- **THEN** the CLI reports that case as ineligible and it counts toward the overall exit code as a non-passing result

### Requirement: Effective required capabilities are derived, not just declared
The CLI SHALL compute each loaded case's effective required capabilities as the union of the case file's explicit `requires:` declarations and any capability implied by a known adapter manifest for the operation, prerequisite, and cleanup names actually referenced in that case — so a case is protected from crashing on a missing capability even if its author forgot to declare `requires:` for an operation a known manifest explains.

#### Scenario: Case forgets to declare requires: for a manifest-known operation
- **WHEN** a case's pipeline references an operation name a known adapter manifest maps to a capability, and the case file does not declare that capability in `requires:`
- **THEN** the CLI still includes that capability in the case's effective required capabilities, so a missing installation is reported as missing-capability rather than crashing

#### Scenario: Explicit requires: declarations are preserved
- **WHEN** a case file declares a `requires:` capability that no known manifest would have inferred
- **THEN** that capability remains part of the case's effective required capabilities, unchanged from today's behavior

#### Scenario: Operation unknown to any manifest is unaffected
- **WHEN** a case's pipeline references an operation name that no known adapter manifest explains
- **THEN** the CLI does not infer any additional required capability for it, and existing unknown-reference behavior applies unchanged
