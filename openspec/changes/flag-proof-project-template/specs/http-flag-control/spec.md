## ADDED Requirements

### Requirement: The control block MAY be supplied by the project manifest and inherited by a case

A `flag_proof` case's `control` block MAY be declared, in whole or in part, in
the project manifest's `flag_proof.control` section instead of in the case file.
The flag-proof runner SHALL operate on the **merged** control block, computed as:
start from the manifest's `flag_proof.control`, then apply the case's inline
`control` block over it by a **deep merge** — scalar and `headers` keys present
in the case override the manifest's; `auth` and `verify` sub-blocks present in
the case replace the manifest's corresponding sub-block; keys the case does not
mention are taken from the manifest.

A case that declares no `control` block and whose manifest declares a complete
`flag_proof.control` SHALL behave exactly as if that block had been written
inline. A case with neither an inline nor an inherited `control` block, and no
adapter-provided feature-state controller, SHALL remain ineligible — unchanged
from today.

All substitution and resolution rules SHALL apply to the merged result exactly as
they apply to an inline `control` block: `{{featureKey}}`, `{{state}}`,
`{{enabled}}`, `{{token}}` substitution; `${ENV_VAR}` / `project-secrets`
resolution at load time; `known_bad_when` polarity; and the failed / ineligible
classifications for a bad control request, a bad `verify` read-back, or a failed
`auth` exchange.

#### Scenario: A case inherits a complete control block from the manifest

- **WHEN** the manifest declares a full `flag_proof.control` (url, headers, auth) and a `flag_proof` case declares only `feature_key`
- **THEN** the flag-proof run performs the manifest's control request before each leg, with `{{featureKey}}` resolved to that case's `feature_key`, exactly as if the block were inline

#### Scenario: One manifest template serves cases with different flag keys

- **WHEN** the manifest's `control.url` is `.../flags/{{featureKey}}` and two cases declare different `feature_key` values and no inline `control`
- **THEN** each case's control request targets its own flag key

#### Scenario: A case overrides one field of the inherited block

- **WHEN** the manifest declares `control` with `auth` and base `headers`, and a case declares an inline `control` with only an added `verify` sub-block
- **THEN** the merged control block keeps the manifest's `url`, `headers`, and `auth` and adds the case's `verify`, and the run performs the read-back accordingly

#### Scenario: A case replaces the inherited auth section

- **WHEN** the manifest's `control.auth` names one token endpoint and a case's inline `control.auth` names a different one
- **THEN** the case's `auth` section is used in full for that case and the manifest's is not merged into it

#### Scenario: An incomplete merged control block is a load-time error

- **WHEN** neither the manifest nor a case supplies a `control.url`, but one of them supplies other `control` fields
- **THEN** the case is rejected at load time with an error naming the case and the missing `url`, before any case in the batch runs

#### Scenario: A failed manifest-sourced control request fails the run

- **WHEN** a case inherits its `control` block from the manifest and the known-good control request returns 500
- **THEN** the flag-proof run is reported as failing — distinct from weak-oracle and ineligible — and names the control request as the cause, identical to an inline `control` block failing
