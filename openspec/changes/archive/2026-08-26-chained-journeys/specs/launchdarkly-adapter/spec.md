## Purpose

Provides a LaunchDarkly-backed feature-state controller so flag-proof mode can toggle a real
LaunchDarkly flag around a paired known-bad/known-good run, the same way the Azure DevOps adapter
already does for its own variable-group flags — making flag-proof usable against systems whose real
feature flags live in LaunchDarkly rather than Azure DevOps.

## ADDED Requirements

### Requirement: LaunchDarkly flag state can be toggled for flag-proof
When installed, this adapter SHALL expose the ability to set a named LaunchDarkly flag's on/off
state for a given project and environment, satisfying the core's feature-state control contract
used by flag-proof mode.

#### Scenario: Flag-proof toggles a real LaunchDarkly flag
- **WHEN** a flag-proof case runs with this adapter installed and configured
- **THEN** the named LaunchDarkly flag is set on for the known-good leg and off for the known-bad
  leg (or vice versa, per the case's declared expectation), via LaunchDarkly's own API

### Requirement: Adapter credentials are supplied externally
This adapter's LaunchDarkly API access token, project key, and environment key SHALL be supplied
via the environment, never hardcoded or embedded in a case file.

#### Scenario: Missing credentials are a clear startup error
- **WHEN** this adapter's required environment variables are not fully set
- **THEN** the CLI reports a clear configuration error rather than attempting to call LaunchDarkly
  without credentials
