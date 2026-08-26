## ADDED Requirements

### Requirement: The CLI can run a pinned hosted journey
The CLI SHALL support running a journey fetched from `hosted-journeys` by ID and pinned version, in
place of or alongside a local cases directory, using the same execution and reporting behavior as
any locally-loaded case.

#### Scenario: A hosted journey runs like a local case
- **WHEN** the CLI is invoked with a hosted journey reference (ID and pinned version) instead of a
  local cases directory
- **THEN** the fetched journey executes under the same pipeline, cleanup, and reporting guarantees
  as a case loaded from a local file

#### Scenario: A fetch failure is a clear error, not a silent skip
- **WHEN** the CLI cannot fetch the specified journey version (network failure, invalid token,
  version not found)
- **THEN** the CLI reports a clear error and does not proceed as though no journey were requested
