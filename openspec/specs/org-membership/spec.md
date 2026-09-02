# org-membership Specification

## Purpose
Lets more than one person work inside the same organization: a user can belong to
one or more organizations, an admin can invite teammates by email, and each
membership carries a role that decides which organization-level operations the
member may perform.
## Requirements
### Requirement: A user may belong to more than one organization
The system SHALL represent the link between a user and an organization as a
membership record, not as a single organization reference on the user. A user
SHALL be able to hold memberships in multiple organizations at once, and each
membership SHALL carry exactly one role.

#### Scenario: User accepts an invite while already in another organization
- **WHEN** a user who is already a member of organization A accepts an invite to organization B
- **THEN** the user holds active memberships in both A and B, each with its own role, and neither membership is altered by the other

#### Scenario: Removing a membership does not delete the user
- **WHEN** an admin removes a member from an organization
- **THEN** that user's memberships in any other organization are unaffected and the user account continues to exist

### Requirement: Every request resolves to exactly one active organization
Every authenticated web-session request SHALL act on exactly one organization,
chosen from the organizations the requesting user is a member of. A request that
resolves to no membership SHALL be rejected as unauthorized rather than falling
back to any default organization.

#### Scenario: User switches active organization
- **WHEN** a user who is a member of organizations A and B selects B as the active organization
- **THEN** subsequent requests read and write organization B's projects, tokens, and evidence, and never A's

#### Scenario: User with no membership is refused
- **WHEN** an authenticated user who is not a member of any organization makes a request for organization data
- **THEN** the request is rejected as unauthorized and no organization is auto-created for that request

### Requirement: Membership roles gate organization-level operations
Each membership SHALL have one of two roles: `admin` or `member`. Managing
billing, plan tier, API tokens, members, invitations, and notification targets
SHALL require the `admin` role. Using projects and viewing run history and
evidence SHALL be available to both roles. The last remaining `admin` of an
organization SHALL NOT be removable or demotable until another admin exists.

#### Scenario: Member cannot manage tokens or members
- **WHEN** a user whose role is `member` attempts to create or revoke an API token, invite a teammate, change a role, or change the plan tier
- **THEN** the request is rejected as forbidden and no change is made

#### Scenario: Member can use projects and view evidence
- **WHEN** a user whose role is `member` opens the dashboard, views run history, or opens an evidence document for a project in their organization
- **THEN** the data is shown

#### Scenario: Last admin is protected
- **WHEN** an admin attempts to remove or demote the only remaining admin of an organization
- **THEN** the request is rejected with a reason stating the organization must keep at least one admin

### Requirement: Admins invite teammates by verified email

An admin SHALL be able to issue an invitation to an email address for a chosen
role. An invitation SHALL be single-use, SHALL expire after a bounded time, and
SHALL be revocable by an admin before it is accepted.

Accepting the invitation SHALL create a membership for the accepting user in
that organization with the invited role, and SHALL be permitted only when the
accepting user's authenticated, provider-verified email address matches the
address the invitation was issued to. An acceptance attempt by a signed-in user
whose verified email does not match — or whose session carries no verified email
at all — SHALL be refused with no membership created, and SHALL be reported the
same way as an expired or revoked invitation so it discloses nothing about who
was invited. The accepting user SHALL NOT be able to change the invited role
during acceptance.

The invitation-preview surface (the data shown on the invitation landing page
before acceptance) SHALL NOT return the invited email address to a caller who is
not the invited user; it MAY confirm only the organization name and the offered
role.

#### Scenario: Invitee with matching verified email accepts and gains access

- **WHEN** a person signs in with a provider-verified email that matches a pending invitation and accepts it
- **THEN** they become a member of the inviting organization with the role named on the invitation, and the invitation is marked used and cannot be accepted again

#### Scenario: Expired or revoked invitation is rejected

- **WHEN** a person attempts to accept an invitation that has expired or been revoked
- **THEN** acceptance is refused, no membership is created, and the response states the invitation is no longer valid

#### Scenario: A non-matching or unverified email cannot accept

- **WHEN** a signed-in user whose verified email does not match the invited address (or whose session carries no verified email) opens the invitation link and attempts to accept
- **THEN** acceptance is refused, no membership is created, and the response is indistinguishable from that for an invalid invitation

#### Scenario: Invited role is fixed during acceptance

- **WHEN** an invitation issued for the `member` role is accepted by the matching user
- **THEN** the resulting membership has the `member` role and the accepting user cannot elevate it during acceptance

#### Scenario: The invitation preview does not disclose the invited email

- **WHEN** any authenticated user loads the invitation-preview surface for a token
- **THEN** the response contains the organization name and offered role but not the invited email address

