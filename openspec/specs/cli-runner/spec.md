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

The set of adapters the CLI considers MAY be declared in an optional `releasetwin.yaml` file at the project root, via an `adapters:` list of adapter names. When the file is absent or has no `adapters:` key, the CLI SHALL consider every adapter it knows about and auto-load each one whose credentials fully resolve — the behavior that existed before this file. When an `adapters:` list is present it is authoritative: only listed adapters are considered, a credential-free adapter (HTTP) is available whether or not it is listed, an adapter configured in the environment but not listed SHALL NOT be installed, and a listed credentialed adapter whose credentials resolve from neither the environment nor a hosted fetch SHALL be reported as a clear startup error rather than silently skipped. The file names which adapters a project uses; it SHALL NOT contain credentials, which continue to resolve only from the environment or the hosted `adapter-credentials` capability. A `releasetwin.yaml` that is present but malformed SHALL be a startup error.

#### Scenario: Cases from two different adapters run in the same invocation
- **WHEN** the CLI is run with a cases directory containing one case using Azure DevOps operations and one case using generic HTTP operations
- **THEN** both cases execute successfully in the same run, using their respective adapters

#### Scenario: No config file preserves auto-detection
- **WHEN** there is no `releasetwin.yaml` (or it has no `adapters:` key) and an adapter's full credential set is present in the environment
- **THEN** the CLI installs that adapter exactly as it did before this file existed

#### Scenario: A listed adapter with no credentials is a startup error
- **WHEN** `releasetwin.yaml` lists a credentialed adapter and neither the environment nor a hosted fetch provides its credentials
- **THEN** the CLI exits with a clear error naming that adapter, without executing any case

#### Scenario: An unlisted but environment-configured adapter is not installed
- **WHEN** `releasetwin.yaml` has an `adapters:` list that omits an adapter whose credential environment variables are nonetheless fully set
- **THEN** the CLI does not install that adapter, and a case referencing its operations reports the missing-capability outcome

#### Scenario: HTTP is always available
- **WHEN** `releasetwin.yaml` has an `adapters:` list that does not include `http`
- **THEN** the generic HTTP adapter is still installed and `http.*` operations still run

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

### Requirement: The CLI can run a pinned hosted journey
The CLI SHALL support running a journey fetched from `hosted-journeys` by ID and pinned version, in
place of or alongside a local cases directory, using the same execution and reporting behavior as
any locally-loaded case.

#### Scenario: A hosted journey runs like a local case
- **WHEN** the CLI is invoked with a hosted journey reference (ID and pinned version) instead of a
  local cases directory
- **THEN** the fetched journey executes under the same pipeline, cleanup, and reporting guarantees
  as a case loaded from a local file

#### Scenario: A fetch failure is a clear error, not a silent skip
- **WHEN** the CLI cannot fetch the specified journey version (network failure, invalid token,
  version not found)
- **THEN** the CLI reports a clear error and does not proceed as though no journey were requested

### Requirement: Case-file environment-variable references can resolve from hosted project secrets
When a case file's `${VAR_NAME}` reference is not satisfied by the local process environment and a
project API token is configured, the CLI SHALL attempt to resolve it from that project's stored
secrets (`project-secrets`) before treating the reference as missing. A local environment variable,
when present, SHALL always take precedence and SHALL NOT trigger a hosted fetch for that name.

#### Scenario: A hosted-stored secret resolves a reference the local environment doesn't have
- **WHEN** a case file references `${VAR_NAME}`, no local environment variable of that name is set,
  and a project API token is configured with that project having a stored secret under that name
- **THEN** the reference resolves to the hosted-stored secret's value

#### Scenario: A local environment variable takes precedence over a hosted-stored secret
- **WHEN** a case file references `${VAR_NAME}`, a local environment variable of that name is set,
  and the project also has a different stored secret under that name
- **THEN** the reference resolves to the local environment variable's value, and no hosted fetch is
  attempted for that name

#### Scenario: Neither source resolves the reference
- **WHEN** a case file references `${VAR_NAME}`, no local environment variable of that name is set,
  and either no project API token is configured or the project has no stored secret under that name
- **THEN** the reference is reported as missing, the same clear load-time error as today

### Requirement: Evidence capture is a resolved opt-in, off by default
The CLI SHALL determine whether to capture run evidence from an explicit opt-in: an environment variable, and — when a project API token is configured — a per-project default fetched from the hosted platform. Absent an explicit opt-in from either source, the CLI SHALL NOT capture evidence, and its behavior SHALL be unchanged from before this capability. An environment-variable opt-in setting SHALL take precedence over the hosted per-project default. Whether evidence is captured for a run SHALL NOT depend on whether a project API token is configured or a local evidence directory is set — those settings only affect where captured evidence is written, per the "Captured evidence is redacted then written" requirement.

#### Scenario: No opt-in means no capture
- **WHEN** the CLI runs with neither the evidence environment variable set nor a hosted per-project default enabling it
- **THEN** no evidence is captured, redacted, uploaded, or written locally, and the run behaves exactly as before this capability

#### Scenario: Environment opt-in enables capture
- **WHEN** the CLI runs with the evidence-capture environment variable set to enable it
- **THEN** the CLI captures run evidence regardless of the hosted per-project default

#### Scenario: Environment opt-out overrides a hosted default
- **WHEN** the hosted per-project default enables capture but the environment variable is set to disable it
- **THEN** no evidence is captured

#### Scenario: Capture is enabled with no token and no local evidence directory
- **WHEN** the CLI runs with evidence capture enabled, no project API token configured, and no local evidence directory configured
- **THEN** the CLI still captures and redacts evidence internally, exactly as when a destination is configured, but the run produces no evidence output anywhere since neither destination is set

### Requirement: Captured evidence is redacted then written
When evidence capture is enabled, the CLI SHALL, after execution, apply the `evidence-capture` redaction to the run's evidence and write the redacted document to every destination configured for that run: upload to the ingest API when a project API token is configured, and/or write to a local directory when one is configured via an environment variable. Configuring neither destination leaves capture enabled but produces no output, per the opt-in requirement's scenario above. Configuring both SHALL perform both — one destination's failure or absence SHALL NOT affect the other.

#### Scenario: Redacted evidence is uploaded with the report
- **WHEN** a case runs with evidence capture enabled and a token configured
- **THEN** the CLI uploads the report together with the redacted evidence document for that run

#### Scenario: Capture with only a local directory configured does not upload
- **WHEN** a case runs with evidence capture enabled, a local evidence directory configured, and no token configured
- **THEN** the CLI writes the redacted evidence document (and any redacted screenshots) under the local evidence directory, and attempts no upload

#### Scenario: Capture with both a token and a local directory configured does both
- **WHEN** a case runs with evidence capture enabled, a token configured, and a local evidence directory also configured
- **THEN** the CLI uploads the redacted evidence document to the ingest API and also writes it to the local evidence directory

### Requirement: Evidence upload failure does not change the case result
A failure to upload evidence (network error, rejected by the ingest API, evidence not accepted for the tier) SHALL be surfaced as a distinct warning and SHALL NOT alter the case's pass/fail outcome, the report's own upload, or the CLI's exit code. A failure to write evidence to a local directory (permission error, disk full, invalid path) SHALL likewise be surfaced as a distinct warning and SHALL NOT alter the case's pass/fail outcome, its report, or the CLI's exit code — and SHALL NOT prevent a concurrently configured upload from being attempted.

#### Scenario: Evidence rejected, report still counts
- **WHEN** a case passes locally, its report uploads successfully, but its evidence upload is rejected
- **THEN** the case is still reported and counted as passing, the exit code reflects only local execution, and the evidence rejection is shown as a separate warning

#### Scenario: Local evidence write failure does not affect the case result or upload
- **WHEN** a case passes locally with both a token and a local evidence directory configured, and writing evidence to the local directory fails (e.g. the directory is not writable)
- **THEN** the case is still reported and counted as passing, the evidence upload to the ingest API is still attempted, and the local write failure is shown as a separate warning

### Requirement: Local evidence output requires no hosted account
The CLI SHALL support writing captured, redacted run evidence to a local directory via an environment variable, independent of any hosted project, API token, or network access. This SHALL work identically for a run with no `RELEASETWIN_API_TOKEN` configured at all.

#### Scenario: Local evidence works fully offline
- **WHEN** the CLI runs with evidence capture enabled and a local evidence directory configured, no API token configured, and no network access available
- **THEN** the run completes, is reported normally, and redacted evidence is written to the local evidence directory with no network attempt made for evidence

### Requirement: Locally written evidence is organized per case
When writing evidence to a local directory, the CLI SHALL group each case's redacted evidence document and any redacted screenshots under a location identifiable by that case's id, so a run with multiple cases does not overwrite or intermix different cases' evidence.

#### Scenario: Two cases in one run produce separate evidence locations
- **WHEN** a run with a local evidence directory configured executes two cases with different ids, both producing evidence
- **THEN** each case's evidence document and screenshots are written under a location distinguishable by that case's id, and neither case's evidence overwrites the other's

