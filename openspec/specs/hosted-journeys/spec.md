# hosted-journeys Specification

## Purpose

Stores journey definitions (multi-step pipelines, authored visually in the dashboard) as versioned,
immutable content the CLI can fetch and run at execution time — the first hosted capability that
hands the CLI something to execute, distinct from `ingest-api`'s existing upload-only direction.

## Requirements

### Requirement: A saved journey version is immutable
Once created, a journey version's content SHALL NOT change. Editing a journey SHALL create a new
version rather than mutating an existing one.

#### Scenario: Editing a journey does not alter a previously fetched version
- **WHEN** a journey is edited and saved after a version of it has already been fetched by a CLI run
- **THEN** a later fetch of that same, specific version returns the same content as it did before
  the edit — the edit is only visible as a new version

### Requirement: The CLI fetches one specific, pinned journey version
Running a hosted journey SHALL require specifying which version to run. The CLI SHALL NOT fetch
"whatever is currently latest" implicitly — a run that doesn't pin a version is rejected rather than
silently resolved to the newest one.

#### Scenario: A pinned run is reproducible
- **WHEN** the same journey version is run twice, with no edits to that version in between
- **THEN** both runs execute identical content

#### Scenario: Running without a pinned version is rejected
- **WHEN** a CLI invocation requests a hosted journey without specifying a version
- **THEN** the request is rejected rather than defaulting to the latest version

### Requirement: Fetching a journey version requires the project's API token
A journey version SHALL only be fetchable using a valid API token scoped to the project that owns
it, the same authentication `ingest-api` already requires for uploads.

#### Scenario: An unauthenticated or wrong-project fetch is denied
- **WHEN** a journey version is requested without a valid token, or with a token scoped to a
  different project
- **THEN** the request is denied and the journey's content is not returned

### Requirement: A journey version records who created it and when
Each journey version SHALL record the authenticated dashboard user who created it and its creation
timestamp, so a customer can audit who introduced a given version's content.

#### Scenario: Version history shows authorship
- **WHEN** a customer views a journey's version history
- **THEN** each version shows who created it and when
