## ADDED Requirements

### Requirement: Results are optionally uploaded to the hosted platform
If an API token is supplied via environment variable, the CLI SHALL upload each executed case's report (and flag-proof result, where applicable) to the ingest API after execution, mapped into the ingest contract (design.md D1). If no token is supplied, the CLI SHALL run and report exactly as before, with no upload attempted and no error raised for its absence.

#### Scenario: Upload occurs when a token is configured
- **WHEN** the CLI runs with an API token supplied via environment variable
- **THEN** each executed case's report is uploaded to the ingest API after execution completes

#### Scenario: No upload is attempted without a token
- **WHEN** the CLI runs with no API token configured
- **THEN** it executes and reports cases exactly as it did before this capability existed, without attempting any upload and without treating the missing token as an error

### Requirement: Upload failure does not change the case's local result
A failure to upload a report (network error, rejected by the ingest API, etc.) SHALL be reported to the user as a distinct warning, but SHALL NOT alter the case's own pass/fail outcome or the CLI's exit code.

#### Scenario: Upload failure is a warning, not a case failure
- **WHEN** a case passes locally but its report fails to upload
- **THEN** the case is still reported and counted as passing, and the CLI's exit code still reflects only local execution outcomes; the upload failure is surfaced separately as a warning
