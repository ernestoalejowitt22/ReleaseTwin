## ADDED Requirements

### Requirement: An operation may attach structured evidence to its result
The operation contract SHALL provide an optional way for an operation to attach a structured evidence value to the result it returns. An operation that attaches nothing SHALL behave exactly as before, and the core SHALL neither require evidence nor fail a step for its absence. The evidence value's shape is adapter-defined; the core treats it opaquely.

#### Scenario: Operation without evidence is unaffected
- **WHEN** an operation returns a result without attaching evidence
- **THEN** the step executes and reports exactly as it did before this capability, with no evidence recorded for it

#### Scenario: Operation-attached evidence reaches the evidence record
- **WHEN** an operation attaches structured evidence and the run has evidence aggregation enabled
- **THEN** that evidence appears in the run's evidence record under that step, unmodified by the core

#### Scenario: Core defines no adapter-specific evidence shape
- **WHEN** the core contracts are inspected
- **THEN** they define evidence only as an opaque value, with no field or type naming a specific adapter, protocol, or vendor
