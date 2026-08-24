## Purpose

Lets a customer create an organization, a project, and an API token entirely on their own, so the path from "never heard of this" to "has credentials to upload results" requires no human on the other end.

## ADDED Requirements

### Requirement: Signup requires no human approval
A prospective customer SHALL be able to create an account (via email or an OAuth provider) and have it immediately usable, without any manual review, approval, or human interaction from the operator.

#### Scenario: New signup is immediately usable
- **WHEN** a prospective customer completes signup
- **THEN** they can create an organization and project immediately, without waiting for manual approval

### Requirement: Organization and project creation
A signed-up customer SHALL be able to create an organization and at least one project within it, entirely through self-service.

#### Scenario: Customer creates their first project
- **WHEN** a signed-up customer creates a project within their organization
- **THEN** the project exists and is ready to receive an API token, with no operator action required

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
