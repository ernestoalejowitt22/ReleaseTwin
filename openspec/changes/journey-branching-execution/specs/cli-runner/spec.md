## MODIFIED Requirements

### Requirement: The CLI can run a pinned hosted journey
The CLI SHALL support running a journey fetched from `hosted-journeys` by ID and pinned version, in
place of or alongside a local cases directory, using the same execution and reporting behavior as
any locally-loaded case. A fetched version whose pipeline contains choice entries or step `when`
guards SHALL load and execute under the `journey-branching` semantics, exactly as a local case file
with the same content would.

#### Scenario: A hosted journey runs like a local case
- **WHEN** the CLI is invoked with a hosted journey reference (ID and pinned version) instead of a
  local cases directory
- **THEN** the fetched journey executes under the same pipeline, cleanup, and reporting guarantees
  as a case loaded from a local file

#### Scenario: A fetch failure is a clear error, not a silent skip
- **WHEN** the CLI cannot fetch the specified journey version (network failure, invalid token,
  version not found)
- **THEN** the CLI reports a clear error and does not proceed as though no journey were requested

#### Scenario: A hosted journey with a choice runs only the taken branch
- **WHEN** the fetched pinned version contains a choice and a guarded step
- **THEN** only the taken branch's steps and the guarded step whose condition holds are executed,
  and the journey's result is reported like any other case

#### Scenario: A hosted journey that violates branching rules is a clear parse error
- **WHEN** the fetched pinned version contains a nested choice, an unsupported operator, or an
  out-of-scope capture reference
- **THEN** the CLI reports a clear parse error naming the journey and version and runs nothing
