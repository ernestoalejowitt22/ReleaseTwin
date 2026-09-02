## MODIFIED Requirements

### Requirement: Connecting a project requires an authenticated web session

Starting or completing a GitHub connection for a project SHALL require the same authenticated web session the dashboard itself requires, and SHALL only operate on projects belonging to the signed-in customer's own organization.

The opaque `state` value carried through the authorization round trip SHALL be
bound to the user who started the flow, and SHALL remain tamper-evident and
time-limited. Completing the flow SHALL be refused when the presented `state`
was issued for a different user, even if that user shares the target project's
organization, and when the `state` is expired, altered, or unrecognized.

#### Scenario: Unauthenticated connection attempt is denied

- **WHEN** a connection flow is started without a valid web session
- **THEN** access is denied, consistent with any other dashboard action

#### Scenario: A project outside the signed-in organization cannot be connected

- **WHEN** a connection is attempted for a project belonging to a different organization
- **THEN** the request is rejected, regardless of what project ID is supplied

#### Scenario: A state minted for another user is rejected

- **WHEN** the callback step is completed with a `state` value that was issued to a different signed-in user
- **THEN** the flow is refused and no repository list is returned or connection recorded

#### Scenario: An expired or altered state is rejected

- **WHEN** the callback step is completed with a `state` value that is expired, modified, or not recognized
- **THEN** the flow is refused with the same generic "expired or invalid" outcome
