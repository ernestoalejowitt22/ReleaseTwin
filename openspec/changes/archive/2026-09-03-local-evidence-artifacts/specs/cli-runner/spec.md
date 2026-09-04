## MODIFIED Requirements

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

## ADDED Requirements

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
