# account-provisioning Specification

## Purpose

Lets a customer create an organization, a project, and an API token entirely on their own, so the path from "never heard of this" to "has credentials to upload results" requires no human on the other end.
## Requirements
### Requirement: Signup requires no human approval
A prospective customer SHALL be able to create an account via a managed auth provider offering at least one sign-in method that does not presuppose an account on a specific unrelated third-party platform, and have it immediately usable, without any manual review, approval, or human interaction from the operator.

#### Scenario: New signup is immediately usable
- **WHEN** a prospective customer completes signup
- **THEN** they can create an organization and project immediately, without waiting for manual approval

#### Scenario: Signup does not require an unrelated platform account
- **WHEN** a prospective customer signs up
- **THEN** at least one available sign-in method does not require them to already hold an account on a platform unrelated to ReleaseTwin's own adapters (e.g. it does not require a GitHub account as the only path in)

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

### Requirement: API tokens are self-serve issued and scoped to a project
A customer SHALL be able to generate an API token for a project through self-service, and that token SHALL only grant access to data within the project it was issued for.

#### Scenario: Token is scoped to its own project
- **WHEN** an API token issued for project A is used to upload or read data
- **THEN** it cannot access or affect data belonging to any other project, including other projects in the same organization

### Requirement: API tokens are self-serve revocable
A customer SHALL be able to revoke an API token through self-service, and a revoked token SHALL be rejected by the ingest API immediately.

#### Scenario: Revoked token is rejected
- **WHEN** a customer revokes an API token and it is subsequently used to call the ingest API
- **THEN** the request is rejected as unauthenticated

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

### Requirement: An organization records its Merchant-of-Record billing linkage
An organization SHALL carry the identifiers linking it to its Merchant-of-Record customer
and subscription, its billing status, and its billing cadence, alongside its tier. These
SHALL default to an unlinked, active state at creation and for any organization that has
never checked out, so that a newly signed-up or operator-set organization is fully usable
with no billing linkage. Reading an organization stored before these fields existed SHALL
succeed and yield the unlinked, active defaults.

#### Scenario: A new organization has no billing linkage
- **WHEN** an organization is created at first signup
- **THEN** it has no Merchant-of-Record customer or subscription identifier, its billing
  status is active, and it is fully usable on the Free tier

#### Scenario: An operator-set organization needs no billing linkage
- **WHEN** an operator sets an organization to `Enterprise`
- **THEN** the organization functions with its Enterprise entitlements and no
  Merchant-of-Record subscription

#### Scenario: An organization stored before billing fields existed still loads
- **WHEN** an organization row that predates the billing fields is read
- **THEN** it loads successfully with an unlinked customer/subscription and active billing
  status

### Requirement: Web-session tokens are validated for their intended audience

A web-session credential presented to the hosted API SHALL be accepted only when
it was issued for this API as its audience, in addition to the existing issuer,
signature, and expiry checks. A token that is otherwise valid but was minted for
a different audience SHALL be rejected as unauthenticated.

#### Scenario: A token for another audience is rejected

- **WHEN** a request presents a correctly-signed, unexpired session token whose audience is not this API
- **THEN** the request is rejected as unauthenticated and no user or organization is provisioned or resolved from it

#### Scenario: A token for this API is accepted

- **WHEN** a request presents a valid session token issued with this API as its audience
- **THEN** the request proceeds and the user/organization are resolved as normal

### Requirement: Provisioning binds the account to a provider-verified email

When the auth provider supplies a verified email address for a signing-in user,
provisioning SHALL record that address on the user, and downstream checks that
compare a user's email (such as invitation acceptance) SHALL use that
provider-verified value. Provisioning SHALL NOT treat the absence of a verified
email as equivalent to a match for any such check.

#### Scenario: A verified email is recorded at first sign-in

- **WHEN** a user signs in and the auth provider supplies a verified email address
- **THEN** the provisioned user record carries that email address

#### Scenario: A missing verified email is not a wildcard

- **WHEN** a user whose session carries no verified email performs an operation gated on an email match
- **THEN** the operation is treated as a non-match, not as permitted

