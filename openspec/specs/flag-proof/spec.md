# flag-proof Specification

## Purpose

Defines paired known-bad/known-good execution of the same case against the same immutable build and fixture, reported as a single release-proof result that can detect a weak oracle — the product.s most differentiated capability.
## Requirements
### Requirement: Paired execution against the same build and fixture
A flag-proof run SHALL execute a case's pipeline twice — once with the target feature state set to known-bad, once set to known-good — against the same immutable build and the same verified fixture. The two executions SHALL differ only in feature state.

#### Scenario: Same fixture and build used for both legs
- **WHEN** a flag-proof run executes the known-bad and known-good legs
- **THEN** both legs use the same fixture hash and the same build identity, differing only in the declared feature state

### Requirement: Single release-proof result
The core SHALL combine the known-bad and known-good leg results into one release-proof result rather than reporting them as two independent case results.

#### Scenario: Report shows one release-proof outcome
- **WHEN** both legs of a flag-proof run complete
- **THEN** the report contains a single release-proof result referencing both legs' outcomes, not two unrelated case reports

### Requirement: Discriminating outcome is the expected pass condition
A flag-proof run SHALL be reported as passing only when the known-bad leg fails and the known-good leg passes. Any other combination SHALL be reported as failing or weak, not passing.

#### Scenario: Correct discrimination passes
- **WHEN** the known-bad leg fails and the known-good leg passes
- **THEN** the flag-proof result is reported as passing

#### Scenario: Both legs passing is reported as a weak oracle
- **WHEN** both the known-bad leg and the known-good leg pass
- **THEN** the flag-proof result is reported as failing with a weak-oracle classification, not as passing

#### Scenario: Both legs failing is reported distinctly
- **WHEN** both the known-bad leg and the known-good leg fail
- **THEN** the flag-proof result is reported as failing, classified separately from the weak-oracle (both-pass) case

### Requirement: Feature-state eligibility check
Before running a flag-proof pair, the core SHALL verify that a way to set the
target feature's state is available — either an installed adapter that provides a
feature-state controller, or an HTTP `control` block declared on the case (see
the `http-flag-control` capability). If neither is available, the core SHALL
report a deferred/ineligible result rather than attempting either leg.

#### Scenario: Missing feature-flag control defers the run
- **WHEN** no installed adapter provides a feature-state controller and the case declares no `control` block
- **THEN** the core reports the flag-proof run as deferred/ineligible and does not execute either leg

#### Scenario: An HTTP control block makes the case eligible
- **WHEN** a `flag_proof` case declares a `control` block and no adapter provides a feature-state controller
- **THEN** the run is eligible and executes both legs, setting the flag via the control request

