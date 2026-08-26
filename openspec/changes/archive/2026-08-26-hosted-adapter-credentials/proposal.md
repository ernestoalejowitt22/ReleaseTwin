## Why

Every adapter that actually executes against a real third-party system (Azure DevOps, LaunchDarkly,
and any future one) invents its own flat list of required environment variables that `CliRunner`
reads straight from the process environment. A customer's only way to discover what's required is
documentation or `CliRunner`'s own partial-config error message — there is no dashboard UI, no
hosted storage, nothing product-side. This was flagged as an explicit, deliberately-unsolved gap
during the `chained-journeys` change (its design.md's "No customer-facing story for adapter
credential setup" risk entry) once a second credentialed adapter (LaunchDarkly) existed to make the
per-adapter-env-var-list pattern's cost visible. The customer has now asked for the real fix rather
than leaving it as a documented gap.

## What Changes

- A customer can store adapter execution credentials (e.g. Azure DevOps's org/project/PAT/area
  path/variable group, LaunchDarkly's API token/project key/environment key) per project, through
  the dashboard, instead of setting them as local/CI environment variables by hand.
- The CLI can fetch a project's stored adapter credentials at startup, authenticated by the same
  project API token `hosted-journeys` already uses — the same trust boundary, extended to a second
  kind of thing the CLI fetches and uses (credentials, not pipeline content).
- Environment variables remain a fully valid, and take-precedence, alternative — this is additive,
  not a replacement. A customer who never touches the new dashboard feature sees no behavior change.
- **A genuinely new trust boundary, not a smaller version of an existing one**: unlike
  `project-connections`' GitHub OAuth token (fetched once, used once, by explicit requirement never
  persisted) and unlike the existing API token (only a SHA-256 hash is ever stored; the raw value is
  shown once at issuance and never retrievable again), this hosted platform must persist a live,
  recoverable third-party credential and be able to hand it back to an authenticated CLI request
  repeatedly, indefinitely. Encryption at rest and rotation/revocation are first-class requirements
  here, not incidental.

## Capabilities

### New Capabilities
- `adapter-credentials`: hosted, per-project, per-adapter storage of adapter execution credentials
  (encrypted at rest), a dashboard UI to set/rotate/revoke them, and an API-token-authenticated CLI
  fetch endpoint.

### Modified Capabilities
- `cli-runner`: gains the ability to resolve adapter configuration (Azure DevOps, LaunchDarkly) from
  a hosted fetch in addition to environment variables, with environment variables taking precedence
  when both are present.

## Impact

- `hosted/ReleaseTwin.Hosted.Api`: new entity/entities for stored adapter credentials, an encryption
  mechanism (and key-management story) for values at rest, new dashboard-facing (ClerkJwt) endpoints
  to set/list/rotate/revoke, and a new CLI-facing (ApiToken) fetch endpoint.
- `web/`: a new dashboard section for managing per-project adapter credentials (set, rotate, revoke;
  values never redisplayed after being set, same "shown once" convention the API token issuance flow
  already uses, even though — unlike the API token — the backend itself does still hold the
  recoverable value for the CLI to fetch later).
- `src/ReleaseTwin.Cli/CliRunner.cs`: adapter-config resolution generalizes from "read these env
  vars" to "read these env vars, else fetch from the hosted API, else neither installs and the
  adapter is unavailable" — for both Azure DevOps and LaunchDarkly.
- Encryption/key-management approach for at-rest credential values is a real design decision, not
  assumed here — see design.md.
