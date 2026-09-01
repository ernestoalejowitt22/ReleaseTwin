## ADDED Requirements

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
