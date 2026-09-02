# ingest-api Specification

## Purpose

The authenticated hosted endpoint that receives uploaded case/flag-proof report metadata from a customer's own CLI runs, storing it scoped to the uploading token's organization — and, by the shape of its own contract, structurally incapable of accepting fixture content, response bodies, or secrets.
## Requirements
### Requirement: Ingest requires a valid API token
Every ingest request SHALL require a valid, non-revoked API token. A request without one, or with an invalid or revoked one, SHALL be rejected before any data is stored. A web-session credential (a Clerk-issued JWT) SHALL NOT satisfy this requirement, even though both are presented as an `Authorization: Bearer` header.

#### Scenario: Missing or invalid token is rejected
- **WHEN** an ingest request is made without a valid API token
- **THEN** the request is rejected with an authentication error and no data is stored

#### Scenario: A web-session credential does not grant ingest API access
- **WHEN** an ingest request presents a valid Clerk-issued JWT (a web-session credential) instead of an API token
- **THEN** the request is rejected the same as if no credential were presented at all

### Requirement: Uploaded reports are scoped to the token's project
A report accepted by the ingest API SHALL be stored as belonging to the project the presenting token was issued for, never to any other project.

#### Scenario: Report is attributed to the correct project
- **WHEN** a report is uploaded using a token issued for project A
- **THEN** the stored report is associated with project A and is not visible to any other project

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

### Requirement: Malformed reports are rejected without partial storage
A report that does not conform to the ingest contract SHALL be rejected in full; no partial record SHALL be stored.

#### Scenario: Malformed report is rejected atomically
- **WHEN** an uploaded report is missing a required field or has a field of the wrong type
- **THEN** the ingest API rejects the entire request and no record is created

### Requirement: Reports may carry an optional release label
The ingest payload's core metadata MAY include an optional `release` string, carried
unchanged from the uploaded case's `release` label. It is a short opaque grouping
identifier — subject to the same "no sensitive content" guarantee as the case identifier
it sits beside — and the API SHALL store it verbatim alongside the report. A payload with
no `release` value SHALL be accepted and stored exactly as before this field existed.

#### Scenario: A report with a release label is stored with it
- **WHEN** a report is uploaded with a `release` value of `4.2`
- **THEN** the stored report carries `release = "4.2"` and it is available to the release
  rollup

#### Scenario: A report with no release label is unchanged
- **WHEN** a report is uploaded with no `release` value
- **THEN** it is accepted and stored identically to the behavior before this field existed

#### Scenario: The release label defines no sensitive field
- **WHEN** the accepted payload schema is inspected
- **THEN** `release` is a plain short string with no capacity to carry fixture content,
  response bodies, or credentials

### Requirement: Screenshot identifiers in an upload are constrained to a fixed format

When an ingest upload carries screenshot parts, each screenshot's identifier
SHALL be a lowercase 32-character hexadecimal string. An upload containing a
screenshot identifier that does not match this format SHALL be rejected in full,
with neither the report, the evidence document, nor any screenshot stored. The
identifier SHALL be treated as opaque data, never as a storage path, key
fragment, or file name supplied by the caller.

#### Scenario: A malformed screenshot identifier rejects the whole upload

- **WHEN** an ingest upload includes a screenshot part whose identifier contains a path separator, an uppercase letter, or is not 32 hex characters
- **THEN** the entire request is rejected and no report, evidence, or screenshot is stored

#### Scenario: A well-formed upload is unaffected

- **WHEN** an ingest upload includes screenshot parts whose identifiers are all lowercase 32-character hex strings
- **THEN** the upload is processed exactly as before this constraint existed

### Requirement: A successful upload response returns the report's dashboard URL

When the ingest API accepts a case or flag-proof report upload, its response
SHALL include a canonical dashboard URL identifying where that report can be
viewed within the uploading token's organization (`reportUrl`), and a URL for the
project's run history for run-level linking (`runUrl`). When evidence
accompanying the report was accepted, `reportUrl` SHALL resolve to a view from
which the stored evidence for the report is reachable.

Both URLs SHALL be org-scoped and SHALL carry no fixture content, response
bodies, or credential values — the same "no sensitive content" guarantee that
applies to the case identifier. The URLs SHALL be absolute when the deployment
has a configured web base URL, and MAY be site-relative otherwise. A response for
a report whose evidence was **not** accepted (tier or redaction signal) SHALL
still return both URLs; the distinction is conveyed by the existing not-accepted
signal, not by omitting a URL.

#### Scenario: Accepted upload returns a viewable URL

- **WHEN** an ingest request with a valid API token uploads a case report and it is stored
- **THEN** the response contains a `reportUrl` for that report and a `runUrl` for the project's run history, both scoped to the token's organization

#### Scenario: URLs are absolute when a web base URL is configured

- **WHEN** the deployment has a web base URL configured and a report is stored
- **THEN** the returned `reportUrl` and `runUrl` are absolute URLs under that base

#### Scenario: Accepted evidence URL reaches the evidence view

- **WHEN** an upload includes evidence that is accepted
- **THEN** the returned URL resolves to a page from which that report's stored evidence is reachable

#### Scenario: The URL carries no sensitive content

- **WHEN** the returned URL is inspected
- **THEN** it contains only organization/report identifiers and no fixture content, response body, or credential value

#### Scenario: Report stored but evidence not accepted still returns the URL

- **WHEN** an upload's metadata is stored but its evidence is rejected on tier grounds
- **THEN** the response still returns the report's dashboard URL and still carries the existing distinct not-accepted signal
