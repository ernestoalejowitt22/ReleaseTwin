## REMOVED Requirements

### Requirement: Admins can invite teammates by email

**Reason**: The prior requirement explicitly stated that a matching invited email
was *not* required to accept an invitation, which lets any holder of an invite
link join an organization at the invited role. This is replaced by a
verified-email-bound version below.

**Migration**: No data migration. Pending invitations remain valid; they now
require the accepting user's provider-verified email to match the address the
invitation was issued to. Invitations issued to an address whose owner never
signs in with a matching verified email will expire unused, as before.

## ADDED Requirements

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
