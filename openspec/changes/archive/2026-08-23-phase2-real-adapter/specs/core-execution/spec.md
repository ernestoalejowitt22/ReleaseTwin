## MODIFIED Requirements

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
