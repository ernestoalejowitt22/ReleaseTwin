## ADDED Requirements

### Requirement: A case can assert the current render matches a stored baseline
A case's pipeline MAY include a screenshot-baseline assertion step that captures the current
browser render (full page or a declared element/region) and compares it against a stored baseline
image for that step. A pixel difference beyond the step's declared threshold SHALL be a
deterministic failure; a difference within the threshold SHALL be a pass. The comparison result
SHALL be the sole authority for the step's pass/fail, independent of any later analysis.

#### Scenario: Render within threshold passes
- **WHEN** a screenshot-baseline assertion runs and the render differs from its baseline by less
  than the declared threshold
- **THEN** the step passes

#### Scenario: Render beyond threshold fails deterministically
- **WHEN** a screenshot-baseline assertion runs and the render differs from its baseline by more
  than the declared threshold
- **THEN** the step fails, and the failure is classified and cleaned up like any other UI step's
  failure

#### Scenario: Missing baseline is a clear failure, not a silent pass
- **WHEN** a screenshot-baseline assertion runs and no baseline image exists for that step
- **THEN** the step fails with a distinct reason indicating the baseline is absent, rather than
  passing or crashing

### Requirement: A failed screenshot-baseline assertion emits a baseline/actual/diff triplet
When a screenshot-baseline assertion fails, the adapter SHALL emit, as that step's evidence, the
baseline image, the actual render, and a diff image highlighting the changed regions. When
evidence capture is not enabled, no triplet SHALL be persisted or uploaded.

#### Scenario: Triplet is available as step evidence on failure
- **WHEN** a screenshot-baseline assertion fails with evidence capture enabled
- **THEN** the step's evidence contains a baseline image, an actual image, and a diff image

### Requirement: A UI step can emit a screen recording as non-authoritative evidence
A UI leg MAY be configured to emit a screen recording of its execution. Recording SHALL be
captured only when explicitly enabled, separately from screenshot evidence. A recording SHALL
NOT influence any step outcome, case outcome, failure classification, or flag-proof adjudication.

#### Scenario: Recording enabled produces a recording without affecting outcomes
- **WHEN** a case's UI leg runs with recording enabled
- **THEN** a recording is produced as evidence and every step and case outcome is identical to the
  same run with recording disabled

#### Scenario: Recording stays off unless explicitly enabled
- **WHEN** a case's UI leg runs with evidence capture enabled but recording not enabled
- **THEN** no recording is produced
