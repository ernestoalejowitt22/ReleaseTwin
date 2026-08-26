## Purpose

Lets a customer store the execution credentials a real adapter (Azure DevOps, LaunchDarkly, and any
future one) needs, per project, through the dashboard, so the CLI can fetch and use them instead of
requiring the customer to set and manage raw environment variables themselves in every place the CLI
runs.

## ADDED Requirements

### Requirement: A customer can store adapter credentials per project
A signed-in customer SHALL be able to set the execution credentials for a specific adapter (e.g.
Azure DevOps's organization/project/personal-access-token/area-path/variable-group, or
LaunchDarkly's API token/project key/environment key) on a specific project, through the dashboard.

#### Scenario: Setting credentials for a project
- **WHEN** a customer submits a complete set of credential fields for an adapter on one of their
  projects
- **THEN** those credentials are stored for that project and adapter, available to be fetched by
  the CLI afterward

#### Scenario: Setting credentials requires an authenticated web session
- **WHEN** a request to set adapter credentials is made without a valid web session, or for a
  project outside the signed-in organization
- **THEN** the request is denied and no credential is stored or changed

### Requirement: Stored credential values are encrypted at rest
A stored adapter credential's sensitive field values (tokens, secrets, personal access tokens) SHALL
be encrypted at rest, never stored or logged in plaintext.

#### Scenario: Raw storage never contains a plaintext secret
- **WHEN** an adapter credential is stored
- **THEN** the underlying stored representation of its sensitive fields is ciphertext, not the
  original plaintext value

### Requirement: A customer can rotate or revoke stored credentials
A customer SHALL be able to replace (rotate) or remove (revoke) a project's stored credentials for
an adapter at any time, through the dashboard, without operator involvement. Once revoked, those
credentials SHALL NOT be returned by a subsequent CLI fetch.

#### Scenario: Rotating replaces the value entirely
- **WHEN** a customer sets new credential values for a project and adapter that already has stored
  credentials
- **THEN** a subsequent fetch returns only the new values — the previous values are no longer
  retrievable

#### Scenario: Revoking removes the credential from future fetches
- **WHEN** a customer revokes a project's stored credentials for an adapter
- **THEN** a subsequent CLI fetch for that project and adapter returns no credential, as if none had
  ever been set

### Requirement: Stored credential values are never redisplayed once set
After a credential value is submitted, the dashboard SHALL NOT redisplay that value in plaintext in
any later view — only whether a credential is currently set for a given project and adapter, and
metadata about it (e.g. when it was last set).

#### Scenario: Viewing a project's credentials after setting them
- **WHEN** a customer views a project's adapter-credential settings after having set a value
- **THEN** the dashboard shows that a credential is set (and non-sensitive metadata about it) but
  never the value itself

### Requirement: The CLI can fetch a project's stored adapter credentials
The CLI SHALL be able to fetch a project's stored credentials for a specific adapter, authenticated
by that project's own API token — the same authentication `ingest-api` and `hosted-journeys` already
require. A fetch using a token scoped to a different project, or an invalid or missing token, SHALL
be denied and SHALL NOT reveal whether any credential exists for the requested project.

#### Scenario: A valid project token fetches that project's credentials
- **WHEN** the CLI fetches an adapter's credentials using a valid API token scoped to the project
  those credentials were stored under
- **THEN** the fetch succeeds and returns the currently stored values for that adapter

#### Scenario: A fetch with no stored credential is a clear, distinct outcome
- **WHEN** the CLI fetches an adapter's credentials for a project that has none stored for that
  adapter
- **THEN** the fetch reports clearly that no credential is configured, distinguishable from an
  authentication failure or a network error

#### Scenario: A wrong-project token cannot fetch another project's credentials
- **WHEN** a fetch is attempted using a token scoped to a different project than the one the
  requested credentials belong to
- **THEN** the request is denied the same way as a fetch for a project with nothing stored — it does
  not reveal that the credential exists for the other project
