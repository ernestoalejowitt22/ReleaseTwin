## Purpose

Lets flag proof run against any feature-flag system reachable by a single HTTP
request. A flag-proof case declares how to set its flag's state over HTTP — as
case data, not adapter code — and the always-installed HTTP adapter performs it
before each leg, so the known-bad/known-good mechanic works without an adapter per
flag vendor.

## ADDED Requirements

### Requirement: A flag-proof case can declare an HTTP control request
A `flag_proof` case MAY declare a `control` block describing one HTTP request
(method, URL, headers, body). When present, the flag-proof runner SHALL perform
that request before each leg to set the target feature to that leg's required
state — known-bad before the known-bad leg, known-good before the known-good leg —
using the HTTP adapter, which requires no credentials to install.

#### Scenario: The control request runs before each leg
- **WHEN** a flag-proof case with a `control` block executes
- **THEN** the control request is performed with the flag set to the known-bad state, the known-bad leg runs, the control request is performed again with the flag set to the known-good state, and the known-good leg runs — both legs against the same fixture hash and build identity

#### Scenario: No control block and no adapter controller
- **WHEN** a `flag_proof` case declares no `control` block and no installed adapter provides a feature-state controller
- **THEN** the run is reported as ineligible and neither leg executes — unchanged from today

### Requirement: The control request substitutes the feature key and the leg state
Within the `control` block the token `{{featureKey}}` SHALL be replaced with the
case's `feature_key`, `{{state}}` with `enabled` or `disabled`, and `{{enabled}}`
with `true` or `false`, according to the leg being prepared. `${ENV_VAR}`
references SHALL be resolved from the environment or the hosted `project-secrets`
capability at load time, exactly as `http.request` parameters are — the case file
SHALL NOT contain a literal credential.

#### Scenario: One template serves cases with different flag keys
- **WHEN** two flag-proof cases share a `control` block whose URL is `.../flags/{{featureKey}}` but declare different `feature_key` values
- **THEN** each case's control request targets its own flag key

#### Scenario: Credentials resolve from the environment, not the case
- **WHEN** a `control` block's `Authorization` header is `Bearer ${FLAGS_TOKEN}`
- **THEN** the request is sent with the resolved token and the case file holds only the `${FLAGS_TOKEN}` reference

### Requirement: Flag polarity is declarable
The `control` block MAY declare `known_bad_when: disabled` (the default) or
`known_bad_when: enabled`. The runner SHALL treat `disabled` as: the known-bad leg
drives the flag to its *off* state and the known-good leg to *on*; and `enabled`
as the reverse, for a flag whose *on* state is the buggy one. An unrecognised
`known_bad_when` value SHALL be a case-load error.

#### Scenario: Default polarity
- **WHEN** a `control` block does not declare `known_bad_when`
- **THEN** the known-bad leg's control request resolves `{{state}}` to `disabled` and the known-good leg's to `enabled`

#### Scenario: Inverted polarity
- **WHEN** a `control` block declares `known_bad_when: enabled`
- **THEN** the known-bad leg's control request resolves `{{state}}` to `enabled` and the known-good leg's to `disabled`

### Requirement: A failed control request fails the run, not misreports it
If a control request returns a non-2xx status or does not complete, the
flag-proof run SHALL be reported as failing — not as passing, weak, or
ineligible — and the leg whose state could not be set SHALL NOT be executed as
though the state had taken effect.

#### Scenario: The flag service rejects the control request
- **WHEN** the known-good control request returns 500
- **THEN** the flag-proof run is reported as failing, distinct from a weak-oracle or ineligible result, and names the control request as the cause
