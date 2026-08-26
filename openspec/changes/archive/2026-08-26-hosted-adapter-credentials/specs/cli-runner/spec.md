## MODIFIED Requirements

### Requirement: Adapter credentials come from the environment or a hosted fetch
The CLI SHALL resolve each adapter's credentials (e.g. an Azure DevOps PAT, a LaunchDarkly API
token) from environment variables at startup, never from a command-line argument or a committed
file, consistent with the adapter-sdk requirement that adapters never hardcode credentials. Partial
environment configuration for an adapter (some, but not all, of its required variables set) SHALL be
reported as a clear startup error, exactly as before this capability existed. When an adapter's
environment variables are entirely unset and a project API token is configured, the CLI SHALL
additionally attempt to fetch that adapter's credentials from the hosted `adapter-credentials`
capability before treating the adapter as not installed. When an adapter's full environment
configuration is present, the CLI SHALL use it without attempting a hosted fetch.

#### Scenario: Partial environment configuration is a clear startup error
- **WHEN** some, but not all, of an adapter's required credential environment variables are set
- **THEN** the CLI reports which variables are missing and exits without attempting to load or run
  any case, regardless of whether a project API token is configured

#### Scenario: Full environment configuration is used without a hosted fetch
- **WHEN** an adapter's full set of credential environment variables is set
- **THEN** the CLI installs that adapter using those values, without attempting a hosted fetch —
  unchanged from this capability's absence

#### Scenario: A hosted-fetched credential is used when the environment has none
- **WHEN** an adapter's credential environment variables are entirely unset, a project API token is
  configured, and that project has stored credentials for that adapter
- **THEN** the CLI fetches and installs the adapter using the hosted-stored credentials

#### Scenario: An environment-supplied credential takes precedence over a hosted one
- **WHEN** an adapter's credential environment variables are fully set and that project also has
  different stored credentials for the same adapter
- **THEN** the CLI uses the environment-supplied values, not the hosted-stored ones

#### Scenario: Neither source configures the adapter
- **WHEN** an adapter's credential environment variables are entirely unset, and either no project
  API token is configured or the project has no stored credentials for that adapter
- **THEN** the CLI proceeds without installing that adapter — the same as today's behavior when
  credentials are entirely absent, not a startup error, since installing a credentialed adapter is
  optional
