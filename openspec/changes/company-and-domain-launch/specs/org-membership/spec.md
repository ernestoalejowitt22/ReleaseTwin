## MODIFIED Requirements

### Requirement: Admins can invite teammates by email
An admin SHALL be able to issue an invitation to an email address for a chosen
role. Accepting the invitation SHALL create a membership for the accepting user
in that organization with the invited role. An invitation SHALL be single-use,
SHALL expire after a bounded time, and SHALL be revocable by an admin before it
is accepted.

When a transactional-email provider is configured, issuing an invitation SHALL
send an email to the invited address containing the accept link. When no
provider is configured, issuing an invitation SHALL still succeed and the
accept link SHALL be returned in the API response so an admin can share it
directly. Email delivery SHALL NOT block or fail the invitation: a provider
error SHALL be recorded but the invitation SHALL remain valid and its link
SHALL still be available in the API response.

#### Scenario: Invitee accepts and gains access
- **WHEN** a person signs in (or signs up) using the link from a pending invitation and accepts it
- **THEN** they become a member of the inviting organization with the role named on the invitation, and the invitation is marked used and cannot be accepted again

#### Scenario: Expired or revoked invitation is rejected
- **WHEN** a person attempts to accept an invitation that has expired or been revoked
- **THEN** acceptance is refused, no membership is created, and the response states the invitation is no longer valid

#### Scenario: Invitation email must match is not required, role is fixed
- **WHEN** an invitation issued for the `member` role is accepted
- **THEN** the resulting membership has the `member` role and the accepting user cannot elevate it during acceptance

#### Scenario: Invited address receives an email when a provider is configured
- **WHEN** an admin issues an invitation and a transactional-email provider is configured
- **THEN** an email is sent to the invited address containing the accept link, and the same link is also present in the API response

#### Scenario: Invitation still succeeds when no provider is configured
- **WHEN** an admin issues an invitation and no transactional-email provider is configured
- **THEN** the invitation is created, no email is attempted, and the accept link is returned in the API response

#### Scenario: Email provider failure does not invalidate the invitation
- **WHEN** an admin issues an invitation, a provider is configured, and the send fails
- **THEN** the invitation is created and valid, the failure is logged, and the accept link is returned in the API response
