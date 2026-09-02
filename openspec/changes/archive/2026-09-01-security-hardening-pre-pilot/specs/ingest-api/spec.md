## ADDED Requirements

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
