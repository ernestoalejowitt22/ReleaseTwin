## Purpose

The authenticated hosted endpoint that receives uploaded case/flag-proof report metadata from a customer's own CLI runs, storing it scoped to the uploading token's organization — and, by the shape of its own contract, structurally incapable of accepting fixture content, response bodies, or secrets.

## ADDED Requirements

### Requirement: Ingest requires a valid API token
Every ingest request SHALL require a valid, non-revoked API token. A request without one, or with an invalid or revoked one, SHALL be rejected before any data is stored.

#### Scenario: Missing or invalid token is rejected
- **WHEN** an ingest request is made without a valid API token
- **THEN** the request is rejected with an authentication error and no data is stored

### Requirement: Uploaded reports are scoped to the token's project
A report accepted by the ingest API SHALL be stored as belonging to the project the presenting token was issued for, never to any other project.

#### Scenario: Report is attributed to the correct project
- **WHEN** a report is uploaded using a token issued for project A
- **THEN** the stored report is associated with project A and is not visible to any other project

### Requirement: The ingest contract has no field for sensitive content
The ingest API's accepted payload SHALL contain only report metadata — case identifier, oracle reference, fixture hash, pass/fail outcome, failure classification, cleanup status, and timing (and, for flag-proof, the equivalent paired-leg summary). It SHALL NOT define any field capable of carrying fixture content, operation response bodies, or credential values.

#### Scenario: Payload shape excludes sensitive fields
- **WHEN** the ingest API's accepted payload schema is inspected
- **THEN** it contains no field intended to carry fixture content, response bodies, or credentials

### Requirement: Malformed reports are rejected without partial storage
A report that does not conform to the ingest contract SHALL be rejected in full; no partial record SHALL be stored.

#### Scenario: Malformed report is rejected atomically
- **WHEN** an uploaded report is missing a required field or has a field of the wrong type
- **THEN** the ingest API rejects the entire request and no record is created
