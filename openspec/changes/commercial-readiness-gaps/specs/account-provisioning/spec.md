## MODIFIED Requirements

### Requirement: Organization and project creation
A signed-up customer SHALL be able to create an organization and at least one
project within it, entirely through self-service. Signup SHALL provision a
membership for the new user; a new default organization SHALL be created only
when the user has no existing organization membership (for example, when they
accepted a teammate's invitation before completing signup). A user SHALL be able
to create additional organizations through self-service.

#### Scenario: Customer creates their first project
- **WHEN** a signed-up customer creates a project within their organization
- **THEN** the project exists and is ready to receive an API token, with no operator action required

#### Scenario: Signup after accepting an invitation does not create a second organization
- **WHEN** a person accepts an invitation to an existing organization and then completes signup
- **THEN** they have a membership in the inviting organization and no additional empty organization is created for them

#### Scenario: Signup with no prior membership creates a default organization
- **WHEN** a person completes signup without any pending or accepted invitation
- **THEN** a new organization is created and they are its first admin member

## ADDED Requirements

### Requirement: Self-serve access is scoped by organization membership and role
All self-serve organization, project, token, and evidence operations SHALL be
authorized by the requesting user's membership in the active organization and by
that membership's role. A user without a membership in the active organization
SHALL be treated as unauthenticated for its data.

#### Scenario: Non-member cannot reach another organization's data
- **WHEN** a signed-up user requests projects, tokens, or run data for an organization they hold no membership in
- **THEN** the request is rejected and no data is returned

#### Scenario: Role governs token management
- **WHEN** a user acts on an API token within an organization where their membership role does not permit token management
- **THEN** the request is rejected as forbidden
