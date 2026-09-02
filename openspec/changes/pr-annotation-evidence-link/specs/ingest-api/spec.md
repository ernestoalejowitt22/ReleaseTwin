## ADDED Requirements

### Requirement: A successful upload response returns the report's dashboard URL

When the ingest API accepts a case or flag-proof report upload, its response
SHALL include a canonical, absolute dashboard URL identifying where that report
can be viewed within the uploading token's organization. When evidence
accompanying the report was accepted, that URL SHALL resolve to a view from which
the stored evidence for the report is reachable.

The URL SHALL be org-scoped and SHALL carry no fixture content, response bodies,
or credential values — the same "no sensitive content" guarantee that applies to
the case identifier. A response for a report whose evidence was **not** accepted
(tier or redaction signal) SHALL still return the report URL; the distinction is
conveyed by the existing not-accepted signal, not by omitting the URL.

#### Scenario: Accepted upload returns a viewable URL

- **WHEN** an ingest request with a valid API token uploads a case report and it is stored
- **THEN** the response contains an absolute dashboard URL for that report, scoped to the token's organization

#### Scenario: Accepted evidence URL reaches the evidence view

- **WHEN** an upload includes evidence that is accepted
- **THEN** the returned URL resolves to a page from which that report's stored evidence is reachable

#### Scenario: The URL carries no sensitive content

- **WHEN** the returned URL is inspected
- **THEN** it contains only organization/report identifiers and no fixture content, response body, or credential value

#### Scenario: Report stored but evidence not accepted still returns the URL

- **WHEN** an upload's metadata is stored but its evidence is rejected on tier grounds
- **THEN** the response still returns the report's dashboard URL and still carries the existing distinct not-accepted signal
