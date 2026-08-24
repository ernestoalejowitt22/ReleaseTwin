## MODIFIED Requirements

### Requirement: Ingest requires a valid API token
Every ingest request SHALL require a valid, non-revoked API token. A request without one, or with an invalid or revoked one, SHALL be rejected before any data is stored. A web-session credential (a Clerk-issued JWT) SHALL NOT satisfy this requirement, even though both are presented as an `Authorization: Bearer` header.

#### Scenario: Missing or invalid token is rejected
- **WHEN** an ingest request is made without a valid API token
- **THEN** the request is rejected with an authentication error and no data is stored

#### Scenario: A web-session credential does not grant ingest API access
- **WHEN** an ingest request presents a valid Clerk-issued JWT (a web-session credential) instead of an API token
- **THEN** the request is rejected the same as if no credential were presented at all
