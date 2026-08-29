## MODIFIED Requirements

### Requirement: The ingest contract has no field for sensitive content
The ingest API's accepted payload SHALL always carry only report metadata in its core fields — case identifier, oracle reference, fixture hash, pass/fail outcome, failure classification, cleanup status, and timing (and, for flag-proof, the equivalent paired-leg summary). These core fields SHALL NOT define any field capable of carrying fixture content, operation response bodies, or credential values.

The payload MAY additionally carry one optional evidence document describing the run's steps and adapter-emitted evidence. When present, that document is treated as already redacted by the caller: the ingest API SHALL NOT inspect it for or strip sensitive content, and SHALL rely on the `evidence-capture` guarantee that redaction happened in the caller's CLI. The contract SHALL still define no field anywhere — core or evidence — capable of carrying a credential or token value, SHALL enforce a maximum evidence document size, and SHALL reject a payload whose evidence exceeds that size without storing anything.

A payload with no evidence document SHALL be accepted and stored exactly as before this change.

#### Scenario: Payload shape excludes sensitive fields
- **WHEN** the ingest API's accepted core payload schema is inspected
- **THEN** it contains no field intended to carry fixture content, response bodies, or credentials

#### Scenario: No field anywhere carries credentials
- **WHEN** the full accepted payload schema, including the optional evidence document, is inspected
- **THEN** it defines no field intended to carry a credential or token value

#### Scenario: Metadata-only payload is unchanged
- **WHEN** a report is uploaded with no evidence document
- **THEN** it is accepted and stored identically to the behavior before evidence was supported

#### Scenario: Evidence document is stored without server-side redaction
- **WHEN** a report is uploaded with an evidence document and the uploading organization is entitled to evidence storage
- **THEN** the evidence is stored as received, with no server-side inspection or stripping of its contents

#### Scenario: Oversized evidence is rejected atomically
- **WHEN** an uploaded payload's evidence document exceeds the maximum size
- **THEN** the entire request is rejected and neither the report nor the evidence is stored
