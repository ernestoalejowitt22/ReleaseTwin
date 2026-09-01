## ADDED Requirements

### Requirement: Post-toggle read-back failure is a distinct outcome
When a flag-proof run's feature-state control accepts a state change but a
post-change read-back shows the feature did not reach the intended state, the
core SHALL report the run as failing with a `ControlUnverified` classification.
This outcome SHALL be distinct from:
- `ControlFailed` — the state-change request itself failed or did not complete;
- `WeakOracle` — both legs passed;
- `BothFailed` — both legs failed.

Neither leg SHALL be reported as having executed under the intended feature
state when the run is `ControlUnverified`, and the result message SHALL name
which leg's state could not be confirmed.

#### Scenario: Toggle accepted but not reflected
- **WHEN** the feature-state control reports the known-good state change as accepted but the read-back shows the feature is still in its known-bad state
- **THEN** the flag-proof run is reported as failing with `ControlUnverified`, not `WeakOracle`, `BothFailed`, or `ControlFailed`, and the message identifies the known-good leg

#### Scenario: Read-back not requested leaves classification unchanged
- **WHEN** a flag-proof case does not request a read-back and both legs pass
- **THEN** the run is classified `WeakOracle` exactly as before this change — `ControlUnverified` is only reachable when a read-back was requested and contradicted the intended state

#### Scenario: Verified state runs the pair normally
- **WHEN** each leg's state change is confirmed by its read-back, the known-bad leg fails, and the known-good leg passes
- **THEN** the run is reported as `Passed`, identical to a run with no read-back
