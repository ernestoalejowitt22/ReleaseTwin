## MODIFIED Requirements

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
