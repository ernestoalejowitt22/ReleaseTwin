## ADDED Requirements

### Requirement: Evidence capture is a resolved opt-in, off by default
The CLI SHALL determine whether to capture run evidence from an explicit opt-in: an environment variable, and — when a project API token is configured — a per-project default fetched from the hosted platform. Absent an explicit opt-in from either source, the CLI SHALL NOT capture evidence, and its behavior SHALL be unchanged from before this capability. An environment-variable opt-in setting SHALL take precedence over the hosted per-project default.

#### Scenario: No opt-in means no capture
- **WHEN** the CLI runs with neither the evidence environment variable set nor a hosted per-project default enabling it
- **THEN** no evidence is captured, redacted, or uploaded, and the run behaves exactly as before this capability

#### Scenario: Environment opt-in enables capture
- **WHEN** the CLI runs with the evidence-capture environment variable set to enable it
- **THEN** the CLI captures run evidence regardless of the hosted per-project default

#### Scenario: Environment opt-out overrides a hosted default
- **WHEN** the hosted per-project default enables capture but the environment variable is set to disable it
- **THEN** no evidence is captured

### Requirement: Captured evidence is redacted then uploaded
When evidence capture is enabled and a project API token is configured, the CLI SHALL, after execution, apply the `evidence-capture` redaction to the run's evidence and upload the redacted document alongside the case or flag-proof report. When capture is enabled but no token is configured, the CLI MAY still surface evidence locally but SHALL NOT attempt any upload.

#### Scenario: Redacted evidence is uploaded with the report
- **WHEN** a case runs with evidence capture enabled and a token configured
- **THEN** the CLI uploads the report together with the redacted evidence document for that run

#### Scenario: Capture without a token does not upload
- **WHEN** a case runs with evidence capture enabled and no token configured
- **THEN** no upload of any kind is attempted

### Requirement: Evidence upload failure does not change the case result
A failure to upload evidence (network error, rejected by the ingest API, evidence not accepted for the tier) SHALL be surfaced as a distinct warning and SHALL NOT alter the case's pass/fail outcome, the report's own upload, or the CLI's exit code.

#### Scenario: Evidence rejected, report still counts
- **WHEN** a case passes locally, its report uploads successfully, but its evidence upload is rejected
- **THEN** the case is still reported and counted as passing, the exit code reflects only local execution, and the evidence rejection is shown as a separate warning
