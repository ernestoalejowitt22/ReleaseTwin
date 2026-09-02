## ADDED Requirements

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
