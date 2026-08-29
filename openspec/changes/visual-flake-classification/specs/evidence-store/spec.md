## ADDED Requirements

### Requirement: Stored screen recordings follow evidence scope and retention but are non-authoritative
A screen recording accepted with an ingest upload SHALL be stored scoped to the uploading token's
project and organization, and SHALL be subject to the same per-project retention window and purge
as other evidence. A stored recording SHALL be marked non-authoritative: no flag-proof result and
no report outcome SHALL reference it, and its expiry or absence SHALL never change a report's
recorded outcome or adjudication.

#### Scenario: Recording is organization-scoped like other evidence
- **WHEN** a recording is uploaded with a token for a project in organization A
- **THEN** no user outside organization A can retrieve or view that recording under any view

#### Scenario: Recording is purged on the project's retention window
- **WHEN** the purge runs and a recording is older than its project's retention window
- **THEN** the recording is deleted, and the metadata report and any flag-proof result it was
  attached to are unchanged

#### Scenario: Recording expiry does not alter adjudication
- **WHEN** a flag-proof report's recording has expired
- **THEN** the flag-proof result still shows its original adjudication, computed without reference
  to the recording

### Requirement: Video analysis is on-demand from stored evidence, not at upload
Analysis of a stored screen recording SHALL be initiated only by an explicit, authenticated
on-demand request scoped to that recording's organization. It SHALL NOT run automatically at
upload or purge time. The result SHALL be stored scoped to the recording's report and SHALL be
advisory only, never changing that report's outcome.

#### Scenario: Upload does not trigger video analysis
- **WHEN** a report with a screen recording is ingested
- **THEN** no video analysis is performed as part of ingest

#### Scenario: On-demand video analysis is organization-scoped and advisory
- **WHEN** an authenticated user in the recording's organization requests analysis of that
  recording
- **THEN** the analysis runs, its result is stored against that report as advisory, and the
  report's recorded outcome is unchanged
