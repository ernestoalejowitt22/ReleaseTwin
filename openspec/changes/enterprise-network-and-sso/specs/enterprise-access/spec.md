## Purpose

Defines what ReleaseTwin guarantees when the target under test is network-isolated
(reachable only from inside a customer VPN or VPC) and identity-gated (Entra ID /
organization OAuth): where execution runs, what network paths are and are not
required, and which authentication patterns are supported from case data.

## ADDED Requirements

### Requirement: No inbound network path into the customer network is required

Running a test suite SHALL NOT require any inbound connection from ReleaseTwin
hosted infrastructure into the customer's network, nor any customer-side
allowlist entry, firewall rule, or reverse tunnel granting ReleaseTwin access to
the target. All communication between the CLI and the hosted platform SHALL be
initiated by the CLI as outbound HTTPS (evidence and verdict ingest); the hosted
platform SHALL NOT initiate a connection toward the customer's network or the
target under test.

#### Scenario: Suite runs with the hosted platform unreachable from the target

- **WHEN** the CLI executes a suite against a target that is reachable only from
  the runner's network, and the target and the hosted platform cannot reach each
  other
- **THEN** the suite runs to completion, and the only hosted interaction is the
  CLI's outbound push of results and evidence

#### Scenario: No hosted-to-target connection is ever attempted

- **WHEN** a suite executes against any target
- **THEN** the target under test only ever receives connections from the runner
  executing the CLI, never from ReleaseTwin hosted infrastructure

### Requirement: A network-isolated target is exercised from a co-located runner

The CLI SHALL run a suite to completion against a target reachable only from
within a private network when the CLI is executed on a runner that has access to
that network. Reaching the isolated network is the responsibility of where the
runner is placed (a self-hosted runner inside the VPN/VPC, or a runner with a
customer-operated tunnel); ReleaseTwin SHALL NOT require a ReleaseTwin-operated
network component to bridge to the target.

#### Scenario: Self-hosted runner inside the private network

- **WHEN** the CLI runs on a runner that can reach an isolated target, and the
  case files address the target by its private hostname
- **THEN** the operations execute against the target and the run is classified
  exactly as it would be for a publicly reachable target

#### Scenario: Runner without network access to the target

- **WHEN** the CLI runs on a runner that cannot reach the target's network
- **THEN** the affected operations fail as unreachable-dependency / inconclusive,
  not as product assertion failures

### Requirement: API authentication against Entra ID is expressible from case data

A case SHALL be able to authenticate requests to an Entra-ID-gated API by
obtaining an access token through an OAuth2 client-credentials exchange against
the organization's token endpoint and presenting it as a bearer token on
subsequent requests. The client credentials SHALL resolve from environment
variables or the hosted `project-secrets` capability; the case file SHALL NOT
contain a literal client secret or token. A worked example case demonstrating
this against the Entra v2 token endpoint SHALL ship with the product.

#### Scenario: Entra-gated API request succeeds with a client-credentials token

- **WHEN** a case performs an OAuth2 client-credentials exchange against an Entra
  token endpoint using credentials supplied through the environment, then issues
  a request carrying the captured token
- **THEN** the request is sent with the resolved bearer token and the case file
  holds only the credential references

### Requirement: SSO-gated browser journeys authenticate via the target app's test mode

The supported pattern for a browser journey against an app gated by interactive
organization SSO SHALL be seeding a test-mode session credential (a cookie) that
the target app honours, before the first navigation — using the UI adapter's
existing pre-navigation cookie step. ReleaseTwin SHALL NOT require, and the
documented guidance SHALL NOT recommend, automating the identity provider's
interactive login flow (password entry, MFA, Conditional Access prompts) as part
of a journey.

#### Scenario: Journey enters an SSO-gated app through a seeded test session

- **WHEN** a journey seeds a session cookie that the target app's test mode
  accepts, then navigates to a route that normally redirects unauthenticated
  users to the organization's SSO login
- **THEN** the navigation loads the authenticated view within the same run,
  without any identity-provider login step

#### Scenario: Guidance does not depend on identity-provider automation

- **WHEN** the enterprise-access documentation describes authenticating a browser
  journey against an SSO-gated app
- **THEN** it presents the target app's test-mode session as the recommended
  path and does not instruct the reader to script the identity provider's login
  page
