# project-secrets Specification

## Purpose

Lets a customer store arbitrary named secrets (customer-chosen name, not a fixed field manifest) per
project, through the dashboard, so a journey or case step can reference them the same way it already
references a local environment variable — without the customer having to separately wire matching
environment variables wherever the CLI runs.

## Requirements

### Requirement: A customer can store an arbitrary named secret per project
A signed-in customer on a Paid-tier organization SHALL be able to set a secret's value under a
customer-chosen name, scoped to a specific project, through the dashboard. The name is not drawn
from a fixed manifest — any name the customer chooses is valid.

#### Scenario: Setting a secret for a project
- **WHEN** a customer submits a name and value for a secret on one of their projects
- **THEN** that secret is stored for that project under that name, available to be fetched by the
  CLI afterward

#### Scenario: Setting a secret requires an authenticated web session
- **WHEN** a request to set a project secret is made without a valid web session, or for a project
  outside the signed-in organization
- **THEN** the request is denied and no secret is stored or changed

#### Scenario: Storing a secret requires the Paid tier
- **WHEN** a customer whose organization is on the Free tier attempts to set a project secret
- **THEN** the request is denied, distinguishably from an authentication failure, naming the
  Paid-tier requirement

### Requirement: Stored secret values are encrypted at rest
A stored project secret's value SHALL be encrypted at rest, never stored or logged in plaintext.

#### Scenario: Raw storage never contains a plaintext value
- **WHEN** a project secret is stored
- **THEN** the underlying stored representation of its value is ciphertext, not the original
  plaintext value

### Requirement: A customer can rotate or revoke a stored secret
A customer SHALL be able to replace (rotate) or remove (revoke) a project secret at any time,
through the dashboard, without operator involvement. Once revoked, that secret SHALL NOT be returned
by a subsequent CLI fetch.

#### Scenario: Rotating replaces the value entirely
- **WHEN** a customer sets a new value for a project secret that already exists under that name
- **THEN** a subsequent fetch returns only the new value — the previous value is no longer
  retrievable

#### Scenario: Revoking removes the secret from future fetches
- **WHEN** a customer revokes a project secret
- **THEN** a subsequent CLI fetch for that project returns no value for that name, as if it had
  never been set

### Requirement: Stored secret values are never redisplayed once set
After a secret value is submitted, the dashboard SHALL NOT redisplay that value in plaintext in any
later view — only that a secret exists under a given name for a project, and metadata about it
(e.g. when it was last set).

#### Scenario: Viewing a project's secrets after setting one
- **WHEN** a customer views a project's secrets after having set a value
- **THEN** the dashboard shows that a secret is set under that name (and non-sensitive metadata
  about it) but never the value itself

### Requirement: The CLI can fetch a project's stored secrets
The CLI SHALL be able to fetch the full set of a project's stored secrets (name and decrypted
value), authenticated by that project's own API token — the same authentication `ingest-api` and
`hosted-journeys` already require. A fetch using a token scoped to a different project, or an
invalid or missing token, SHALL be denied and SHALL NOT reveal whether any secret exists for the
requested project.

#### Scenario: A valid project token fetches that project's secrets
- **WHEN** the CLI fetches secrets using a valid API token scoped to the project those secrets
  belong to
- **THEN** the fetch succeeds and returns every currently stored name/value pair for that project

#### Scenario: A project with no stored secrets is a clear, distinct outcome
- **WHEN** the CLI fetches secrets for a project that has none stored
- **THEN** the fetch reports an empty set, distinguishable from an authentication failure or a
  network error

#### Scenario: A wrong-project token cannot fetch another project's secrets
- **WHEN** a fetch is attempted using a token scoped to a different project than the one the
  requested secrets belong to
- **THEN** the request is denied the same way as a fetch for a project with nothing stored — it
  does not reveal that a secret exists for the other project
