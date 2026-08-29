## ADDED Requirements

### Requirement: The executor optionally aggregates per-step evidence
The core executor SHALL accept a per-run flag indicating whether to aggregate run evidence. When the flag is set, the executor SHALL produce, alongside the case report, an ordered evidence record: for each executed step, its operation name, outcome, duration, any evidence the operation attached to its result, and — for assertion operations — the checked expression, expected value, and observed value. When the flag is not set, the executor SHALL produce no evidence record and its report output SHALL be identical to the behavior before this capability.

#### Scenario: Flag unset produces no evidence and an unchanged report
- **WHEN** a case is executed with the evidence-aggregation flag unset
- **THEN** no evidence record is returned and the case report is identical to what the executor produced before this capability

#### Scenario: Flag set produces an ordered evidence record
- **WHEN** a case with multiple pipeline steps is executed with the evidence-aggregation flag set
- **THEN** the executor returns an evidence record listing each executed step in order with its outcome, duration, and any operation-attached evidence

#### Scenario: Steps after a halt are marked not executed
- **WHEN** a pipeline halts on a failed step with the evidence-aggregation flag set
- **THEN** the evidence record includes the failed step and marks subsequent steps as not executed

### Requirement: Flag-proof execution aggregates evidence per leg
When the evidence-aggregation flag is set for a flag-proof run, the executor SHALL produce a distinct ordered evidence record for the known-bad leg and for the known-good leg, each labelled with which leg it is.

#### Scenario: Each leg has its own evidence record
- **WHEN** a flag-proof case is executed with the evidence-aggregation flag set
- **THEN** the result carries a separate, leg-labelled evidence record for the known-bad and known-good executions

### Requirement: Evidence aggregation introduces no adapter-specific coupling
The evidence record's shape SHALL be vendor-neutral. The core SHALL carry operation-attached evidence opaquely and SHALL NOT reference any adapter-specific evidence type, field name, or vendor concept.

#### Scenario: Core carries operation evidence opaquely
- **WHEN** an operation attaches evidence of an adapter-defined shape and the executor aggregates it
- **THEN** the core stores and passes that evidence through without interpreting adapter-specific fields
