## ADDED Requirements

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

## MODIFIED Requirements

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
