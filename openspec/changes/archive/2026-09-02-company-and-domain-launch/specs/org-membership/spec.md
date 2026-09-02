## MODIFIED Requirements

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

When a transactional-email provider is configured, issuing an invitation SHALL
send an email to the invited address containing the accept link. When no
provider is configured, issuing an invitation SHALL still succeed and the accept
link SHALL be returned in the API response so an admin can share it directly.
Email delivery SHALL NOT block or fail the invitation: a provider error SHALL be
recorded but the invitation SHALL remain valid and its link SHALL still be
available in the API response.

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

#### Scenario: Invited address receives an email when a provider is configured

- **WHEN** an admin issues an invitation and a transactional-email provider is configured
- **THEN** an email is sent to the invited address containing the accept link, and the same link is also present in the API response

#### Scenario: Invitation still succeeds when no provider is configured

- **WHEN** an admin issues an invitation and no transactional-email provider is configured
- **THEN** the invitation is created, no email is attempted, and the accept link is returned in the API response

#### Scenario: Email provider failure does not invalidate the invitation

- **WHEN** an admin issues an invitation, a provider is configured, and the send fails
- **THEN** the invitation is created and valid, the failure is logged, and the accept link is returned in the API response
