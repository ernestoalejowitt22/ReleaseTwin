# http-flag-control Specification

## Purpose
Lets flag proof run against any feature-flag system reachable by a single HTTP
request. A flag-proof case declares how to set its flag's state over HTTP — as
case data, not adapter code — and the always-installed HTTP adapter performs it
before each leg, so the known-bad/known-good mechanic works without an adapter per
flag vendor.
## Requirements
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
with `true` or `false`, according to the leg being prepared. When the `control`
block declares an `auth` section, the token `{{token}}` SHALL be replaced with
the access token captured from that exchange for the current leg. `${ENV_VAR}`
references SHALL be resolved from the environment or the hosted `project-secrets`
capability at load time, exactly as `http.request` parameters are — the case file
SHALL NOT contain a literal credential.

#### Scenario: One template serves cases with different flag keys
- **WHEN** two flag-proof cases share a `control` block whose URL is `.../flags/{{featureKey}}` but declare different `feature_key` values
- **THEN** each case's control request targets its own flag key

#### Scenario: Credentials resolve from the environment, not the case
- **WHEN** a `control` block's `Authorization` header is `Bearer ${FLAGS_TOKEN}`
- **THEN** the request is sent with the resolved token and the case file holds only the `${FLAGS_TOKEN}` reference

#### Scenario: A minted token is substituted for the token placeholder
- **WHEN** a `control` block declares an `auth` section and an `Authorization` header of `Bearer {{token}}`
- **THEN** the control request for each leg is sent with the token captured from that leg's exchange

### Requirement: The control request can obtain its own OAuth2 access token
The `control` block MAY declare an `auth` section describing an OAuth2
client-credentials exchange (token endpoint URL, client ID, client secret,
optional scope). When present, the flag-proof runner SHALL perform that exchange
before performing the control request for each leg, capture the resulting access
token, and make it available to the control request as the token `{{token}}`.
The `auth` section's `${ENV_VAR}` references SHALL resolve from the environment
or the hosted `project-secrets` capability at load time, exactly as the rest of
the `control` block — the case file SHALL NOT contain a literal client secret or
token. If the token exchange returns a non-2xx status or does not complete, the
flag-proof run SHALL be reported as failing under the same classification as a
failed control request, and the corresponding leg SHALL NOT execute.

#### Scenario: A flag API behind org OAuth is toggled with a minted token
- **WHEN** a `control` block declares an `auth` client-credentials section whose credentials are supplied through the environment, and a `body` or header that references `{{token}}`
- **THEN** before each leg the runner performs the token exchange, substitutes the captured token for `{{token}}` in the control request, and the flag is set for that leg

#### Scenario: The token endpoint rejects the exchange
- **WHEN** the `auth` token exchange returns 401 before the known-bad leg
- **THEN** the flag-proof run is reported as failing — distinct from weak-oracle and ineligible — names the token exchange as the cause, and the known-bad leg does not execute

#### Scenario: No auth section keeps today's behavior
- **WHEN** a `control` block declares no `auth` section
- **THEN** the control request is performed exactly as before, with no token exchange attempted

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

### Requirement: A control block MAY declare a read-back verify request
Within a `flag_proof` case's `control` block, a `verify` sub-block MAY declare
one HTTP read request (`method` defaulting to `GET`, `url`, optional `headers`,
optional `body`) together with a `jsonpath` expression and an `expected` value.
When `verify` is present, after the control request sets a leg's state and
before that leg runs, the flag-proof runner SHALL perform the read request and
evaluate the JSONPath expression against its response body, comparing the result
to `expected`.

The same substitutions applied to the `control` request SHALL apply within the
`verify` block: `{{featureKey}}` becomes the case's `feature_key`, `{{state}}`
becomes `enabled` or `disabled`, and `{{enabled}}` becomes `true` or `false`,
according to the leg being prepared — and `{{state}}` / `{{enabled}}` MAY appear
in `expected` so one template asserts the correct value for each leg.
`${ENV_VAR}` references SHALL be resolved from the environment or the hosted
`project-secrets` capability at load time, exactly as `control` and
`http.request` parameters are; the case file SHALL NOT contain a literal
credential. When the `verify` block omits `headers`, the runner SHALL send the
`control` block's headers, so shared auth need not be repeated.

#### Scenario: Read-back confirms the intended state and the leg runs
- **WHEN** a `flag_proof` case whose `control` block declares a `verify` read executes, and the read-back response satisfies the JSONPath assertion for the intended state
- **THEN** that leg runs against the same fixture hash and build identity as the other leg, unchanged from a case with no `verify` block

#### Scenario: One verify template serves both legs
- **WHEN** a `verify` block's `expected` is `{{enabled}}` and its `url` is `.../flags/{{featureKey}}`
- **THEN** the known-bad leg's read-back asserts the flag reports its known-bad value and the known-good leg's read-back asserts its known-good value, against the case's own `feature_key`

#### Scenario: Verify credentials resolve from the environment, not the case
- **WHEN** a `verify` block relies on the `control` block's `Authorization: Bearer ${FLAGS_TOKEN}` header
- **THEN** the read request is sent with the resolved token and the case file holds only the `${FLAGS_TOKEN}` reference

### Requirement: A failed read-back is a distinct condition, not a control failure
When a `verify` read request completes but its JSONPath assertion does not match
`expected` — the flag did not reach the intended state despite a successful
control request — the runner SHALL be signalled with a condition distinct from
a failed control request. When the `verify` read request itself returns a
non-2xx status or does not complete, that SHALL be treated as a control failure
(the state could not be confirmed), the same as a failed `control` request.

#### Scenario: Toggle accepted but flag unchanged
- **WHEN** the `control` request returns 2xx but the `verify` read-back shows the flag still at the previous state
- **THEN** the runner is signalled that the state was not verified, distinct from the `control` request having failed, and the corresponding leg is not executed as though the state had taken effect

#### Scenario: Read-back endpoint itself fails
- **WHEN** the `verify` read request returns 503
- **THEN** the runner is signalled the same as a failed `control` request — the state could not be confirmed

#### Scenario: No verify block is unchanged behavior
- **WHEN** a `flag_proof` case's `control` block declares no `verify` sub-block
- **THEN** each leg runs immediately after its `control` request returns 2xx, exactly as before this change

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
