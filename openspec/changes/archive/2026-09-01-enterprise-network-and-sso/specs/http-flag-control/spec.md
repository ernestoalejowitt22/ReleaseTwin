## ADDED Requirements

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

- **WHEN** a `control` block declares an `auth` client-credentials section whose
  credentials are supplied through the environment, and a `body` or header that
  references `{{token}}`
- **THEN** before each leg the runner performs the token exchange, substitutes
  the captured token for `{{token}}` in the control request, and the flag is set
  for that leg

#### Scenario: The token endpoint rejects the exchange

- **WHEN** the `auth` token exchange returns 401 before the known-bad leg
- **THEN** the flag-proof run is reported as failing — distinct from weak-oracle
  and ineligible — names the token exchange as the cause, and the known-bad leg
  does not execute

#### Scenario: No auth section keeps today's behavior

- **WHEN** a `control` block declares no `auth` section
- **THEN** the control request is performed exactly as before, with no token
  exchange attempted

## MODIFIED Requirements

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
