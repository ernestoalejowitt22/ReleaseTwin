## Why

`adapter-credentials` solved hosted, encrypted, per-project credential storage — but only for two
named adapters with fixed field manifests (Azure DevOps's five fields, LaunchDarkly's three). A
journey or case step can reference any other secret a customer's own target system needs — an API
key, a shared HTTP header secret, a webhook token — only via `${VAR_NAME}` environment-variable
interpolation, resolved locally by whatever machine runs the CLI. That's a real gap for the visual
builder specifically: a customer composes a journey entirely in the dashboard, and only discovers at
run time that they also need to go set matching environment variables by hand, locally or in CI,
before it will actually run — the "compose visually, run anywhere" pitch breaks at the first
non-adapter secret. This surfaced concretely while wiring a real journey against NAHA's own
`/v1/e2e/login` endpoint, which needs an arbitrary `x-e2e-secret` header value with no adapter, and
no hosted home, to hold it.

## What Changes

- A customer can store arbitrary named secrets (customer-chosen name → value, not a fixed field
  manifest) per project, through the dashboard — the same trust mechanism `adapter-credentials`
  already proved (encrypted at rest, set/rotate/revoke, never redisplayed once set).
- A journey or case step references a hosted-stored secret exactly the way it already references a
  local environment variable — `${VAR_NAME}` syntax, resolved at CLI run time. A hosted-stored value
  and a local env var of the same name are interchangeable from the case file's point of view; the
  local value wins when both are present, mirroring `adapter-credentials`' own precedence rule.
- Commercial framing: gated to the Paid tier via the existing `plan-tier-gating` capability — a
  hosted secret vault for journeys is a concrete reason to upgrade beyond the project-count cap, not
  a Free-tier give-away.
- **Explicit Non-Goal, load-bearing for the trust story**: this does not add hosted execution.
  Resolution still happens entirely inside the CLI process on the customer's own machine or CI —
  ReleaseTwin's hosted platform fetches nothing and calls nothing on the customer's behalf beyond
  handing back the decrypted value to an authenticated CLI request, exactly as `adapter-credentials`
  already does. A breach here means encrypted-at-rest secrets were exposed, never that an attacker
  executed a live call as the customer.
- **Explicit Non-Goal**: not a general-purpose secrets manager for the customer's other tooling —
  scoped strictly to values a journey or case step can reference. Not a replacement for
  `adapter-credentials`'s two existing structured vendor forms, which keep their dedicated fields and
  UI; this is additive for everything else.

## Capabilities

### New Capabilities
- `project-secrets`: hosted, per-project, arbitrary-name secret storage (encrypted at rest), a
  dashboard UI to set/rotate/revoke individual secrets, and an API-token-authenticated CLI fetch
  endpoint returning the full set of a project's stored secrets by name.

### Modified Capabilities
- `cli-runner`: `${VAR_NAME}` resolution gains a hosted fallback — when a case file references a
  name not present in the local process environment and a project API token is configured, the CLI
  resolves it from that project's stored secrets before treating it as missing. Local environment
  values continue to take precedence and never trigger a hosted fetch when already present.

## Impact

- `hosted/ReleaseTwin.Hosted.Api`: new entity for stored project secrets (reusing the Data
  Protection encryption mechanism `adapter-credentials` already established), new dashboard-facing
  (ClerkJwt) endpoints to set/list/rotate/revoke, and a new CLI-facing (ApiToken) fetch endpoint
  returning a project's full decrypted secret set.
- `web/`: a new dashboard section, alongside the existing per-adapter credential forms, for managing
  arbitrary named project secrets (add/rotate/revoke; values never redisplayed once set).
- `src/ReleaseTwin.Cli/CliRunner.cs` and/or `case-loading`'s `${VAR_NAME}` resolution path: gains a
  hosted-secret fallback for names not found in the local environment — the exact mechanics (fetch
  the whole project secret set upfront vs. fetch on demand per missing name) are a real design
  decision, not assumed here — see design.md.
- `plan-tier-gating`: this feature is Paid-tier-gated; exact enforcement point (dashboard write,
  hosted fetch, or both) is a design decision — see design.md.
