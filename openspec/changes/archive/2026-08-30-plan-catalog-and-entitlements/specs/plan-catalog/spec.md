## ADDED Requirements

### Requirement: A single plan catalog defines every tier and its entitlements
The system SHALL define its commercial plans in one declarative catalog containing, for
each tier, a stable id, a display name, price metadata (amount, unit, and whether the
amount is a placeholder), a support description, and a complete set of entitlement values.
The catalog SHALL be the only source of tier and entitlement data — no tier limit is
compared or hardcoded anywhere else in the system.

The entitlement set for a tier SHALL include at minimum: a maximum project count
(a number, or unlimited), whether the hosted evidence viewer is available, a maximum
evidence retention window in days (a number, or custom/unbounded), whether custom
redaction rules are available, whether project secret storage is available, whether trend
analytics is available, whether the release-readiness rollup is available, whether CI/PR
integration is available, whether SSO is available, and whether the audit log is
available.

#### Scenario: The catalog is loaded and validated at startup
- **WHEN** the API starts
- **THEN** it loads the catalog from its embedded definition and validates that every
  tier has a complete entitlement set
- **AND** a malformed or incomplete catalog fails startup rather than yielding an empty or
  partial entitlement set

#### Scenario: Three tiers are defined
- **WHEN** the catalog is read
- **THEN** it contains exactly the tiers `free`, `team`, and `enterprise`, in that order

### Requirement: The catalog is served unauthenticated at GET /plans
The system SHALL expose the plan catalog at `GET /plans` with no authentication required,
returning the catalog verbatim. The response SHALL be cacheable and SHALL NOT vary by
caller or include any caller-specific data such as the caller's current tier.

#### Scenario: Anyone can read the catalog
- **WHEN** `GET /plans` is called with no credentials
- **THEN** the full catalog is returned with a success status

### Requirement: An entitlement service resolves an organization to its entitlements
The system SHALL provide a service that, given an organization (or a tier), returns that
tier's entitlement set from the catalog. Every feature gate SHALL make its allow/deny
decision by consulting this service, not by comparing a plan tier value directly.

#### Scenario: A Free organization resolves to Free entitlements
- **WHEN** the entitlement service is asked for a Free-tier organization's entitlements
- **THEN** it returns the `free` tier's entitlement set from the catalog

#### Scenario: An unknown or unparseable stored tier degrades safely
- **WHEN** an organization's stored tier value does not match a catalog tier
- **THEN** the service resolves it to the least-privileged tier (`free`) rather than
  throwing, and the condition is logged

### Requirement: An operator-only endpoint can set any organization's tier
The system SHALL expose an operator-only endpoint that sets a named organization's plan tier
to any catalog tier. It SHALL authenticate as a normal web session and additionally require
the caller to be in a configured operator allowlist; a caller not in the allowlist SHALL be
refused indistinguishably from the endpoint not existing. This is the supported path for the
tier transition that is not self-serve (granting `enterprise`) — no direct data-store edit is
required.

#### Scenario: An operator sets an organization to Enterprise
- **WHEN** a caller in the operator allowlist sets an organization's tier to `enterprise`
- **THEN** the organization's tier becomes `enterprise` and its entitlements resolve accordingly

#### Scenario: A non-operator cannot reach the endpoint
- **WHEN** an authenticated caller who is not in the operator allowlist calls the endpoint
- **THEN** the request is refused and the organization's tier is unchanged

#### Scenario: The admin surface is closed when no operators are configured
- **WHEN** the operator allowlist is empty or unset
- **THEN** no caller can set a tier through the endpoint
