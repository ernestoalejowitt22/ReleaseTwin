# core-execution Specification

## Purpose

Defines the vendor-neutral execution kernel that runs a declarative test case end to end — from fixture integrity and prerequisites through ordered operations, cleanup, and failure classification — without any reference to a specific adapter or product domain.
## Requirements
### Requirement: Case identity and oracle traceability
Every case SHALL declare a stable case identifier and a reference to an approved oracle (e.g. a ticket or requirement locator). The core SHALL carry both through execution into the report so a result can be traced back to what it was meant to prove.

#### Scenario: Report includes oracle reference
- **WHEN** a case with a case ID and oracle locator is executed
- **THEN** the resulting report includes the same case ID and oracle locator unchanged

### Requirement: Fixture integrity verification
A case that declares a fixture with a SHA-256 hash SHALL have that hash verified against the fixture content before any operation executes. Execution SHALL NOT proceed past fixture verification if the hash does not match.

#### Scenario: Verified fixture passes through
- **WHEN** a case's fixture content hashes to the declared SHA-256 value
- **THEN** the core proceeds to prerequisite evaluation

#### Scenario: Tampered fixture blocks execution
- **WHEN** a case's fixture content does not hash to the declared SHA-256 value
- **THEN** the core reports a fixture-integrity failure and does not execute any operation

### Requirement: Prerequisite evaluation before execution
The core SHALL evaluate all declared prerequisite checks for a case before executing any pipeline operation. Each prerequisite check SHALL be attributable to an owner. Each prerequisite check SHALL report one of three outcomes — satisfied, not satisfied, or inconclusive — rather than a plain boolean. A not-satisfied prerequisite SHALL be classified separately from a product assertion failure. An inconclusive prerequisite (the check itself could not be completed, e.g. because a dependency it relies on was unreachable) SHALL be classified separately from both a not-satisfied prerequisite and a product assertion failure.

#### Scenario: Failing prerequisite halts the pipeline
- **WHEN** a declared prerequisite check reports not satisfied
- **THEN** the core does not execute any pipeline operation and classifies the result as a prerequisite failure, not a product failure

#### Scenario: Passing prerequisites allow execution to proceed
- **WHEN** all declared prerequisite checks report satisfied
- **THEN** the core proceeds to execute the case's pipeline operations in declared order

#### Scenario: Inconclusive prerequisite is distinct from not-satisfied
- **WHEN** a declared prerequisite check cannot be completed (e.g. the dependency it checks is unreachable) and reports inconclusive
- **THEN** the core does not execute any pipeline operation and classifies the result distinctly from both a not-satisfied prerequisite and a product assertion failure, so the report never claims a confirmed prerequisite gap when the check could not actually confirm one

### Requirement: Required-capability check precedes reference validation
The core SHALL check a case's required capabilities before validating that its declared operation, prerequisite, and cleanup references exist in the installed catalog. A case whose required capability is unavailable SHALL report a missing-capability result and SHALL NOT be checked for unknown references, even if it has some.

#### Scenario: Capability-gated case with an uninstalled adapter does not crash
- **WHEN** a case declares a required capability that no installed adapter provides, and its pipeline references operation names that likewise belong to no installed adapter
- **THEN** the core reports a missing-capability result for that case, and the CLI run continues to the next case rather than raising an unhandled exception

#### Scenario: Reference validation still runs for capability-satisfied cases
- **WHEN** a case's required capabilities are all available, but its pipeline references an operation name no installed adapter has registered
- **THEN** the core reports an unknown-reference error identifying the missing operation, exactly as before this change

### Requirement: Ordered pipeline execution
The core SHALL execute a case's declared operations in the order they are declared. If an operation fails and the case does not mark that failure as expected, the core SHALL stop executing subsequent operations in that case.

#### Scenario: Operations run in declared order
- **WHEN** a case declares operations A, then B, then C
- **THEN** the core invokes A, then B, then C, and does not invoke B before A completes

#### Scenario: Unexpected operation failure stops the pipeline
- **WHEN** an operation fails and the case does not declare that failure as expected
- **THEN** the core does not execute the remaining declared operations

### Requirement: Expected-failure and unexpected-pass handling
A case MAY declare that a specific operation is expected to fail. The core SHALL treat an expected failure as case-passing and SHALL treat an unexpected pass of that operation as case-failing.

#### Scenario: Expected failure is reported as a pass
- **WHEN** an operation declared as expected-to-fail fails
- **THEN** the case result is reported as passing

#### Scenario: Unexpected pass is reported as a failure
- **WHEN** an operation declared as expected-to-fail instead succeeds
- **THEN** the case result is reported as failing, with a distinct classification from an ordinary assertion failure

### Requirement: Cleanup runs regardless of pipeline outcome
The core SHALL execute a case's declared cleanup operations after the pipeline completes, whether the pipeline succeeded, failed, or was halted by a prerequisite or fixture failure. Cleanup failures SHALL be recorded without overwriting the pipeline's own result.

#### Scenario: Cleanup runs after a pipeline failure
- **WHEN** a pipeline operation fails and halts execution
- **THEN** the core still executes the case's declared cleanup operations before completing the case

#### Scenario: Cleanup failure does not mask pipeline result
- **WHEN** a pipeline succeeds but a cleanup operation fails
- **THEN** the case's pipeline result remains a pass, and the cleanup failure is recorded separately in the report

### Requirement: Bounded retry and timeout
Operations and prerequisite checks MAY declare a retry policy and a timeout. The core SHALL NOT retry beyond the declared bound and SHALL classify a timeout distinctly from an assertion failure.

#### Scenario: Retries stop at the declared bound
- **WHEN** an operation with a retry limit of N fails N+1 times
- **THEN** the core stops retrying after N attempts and reports the operation as failed

#### Scenario: Timeout is classified distinctly
- **WHEN** an operation exceeds its declared timeout
- **THEN** the core reports a timeout classification rather than an assertion-failure classification

### Requirement: Resource-key serialization
Cases that declare the same resource key SHALL NOT execute their pipeline operations concurrently against that resource. Cases with different or no declared resource key are unaffected by this constraint.

#### Scenario: Same resource key serializes execution
- **WHEN** two cases declare the same resource key and are scheduled concurrently
- **THEN** the core executes their pipeline operations one at a time, never overlapping, for that resource key

### Requirement: Failure classification
Every failed case SHALL be classified into one of: prerequisite failure, product assertion failure, infrastructure/harness failure, or unstable (recovered after retry). The classification SHALL be included in the report.

#### Scenario: Distinct classifications for distinct causes
- **WHEN** a case fails due to a prerequisite check versus a JSONPath assertion versus an operation timeout
- **THEN** each failure is reported with the classification matching its cause, not a single generic failure status

### Requirement: Machine-readable report
The core SHALL produce a machine-readable report per case containing: case ID, oracle reference, fixture hash, pass/fail outcome, failure classification (if failed), cleanup status, and timing.

#### Scenario: Report is complete for a passing case
- **WHEN** a case completes successfully
- **THEN** the report contains the case ID, oracle reference, fixture hash, a passing outcome, and cleanup status

### Requirement: Per-step operation parameters
A pipeline step MAY declare a set of named parameters. The core SHALL pass those parameters to the operation's execution, unmodified, so an operation's behavior can be data-driven by the case rather than fixed at adapter-construction time. A step with no declared parameters SHALL still execute correctly, receiving an empty parameter set.

#### Scenario: Operation receives its step's declared parameters
- **WHEN** a pipeline step declares parameters (for example, a URL and an HTTP method)
- **THEN** the operation executing that step receives exactly those parameters, unchanged

#### Scenario: Step with no parameters still executes
- **WHEN** a pipeline step declares no parameters
- **THEN** the operation executing that step receives an empty parameter set and executes normally

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

