# feature-flags Specification

## Purpose

Lets ReleaseTwin evaluate vendor-neutral feature flags across the web app, the hosted
API, and the CLI/engine from one shared registry and one shared evaluation-context
shape, so that deploying code and releasing a feature can be decoupled without coupling
the codebase to any single flag vendor.

## Requirements

### Requirement: A single flag registry is the source of truth

The system SHALL define every feature flag in one registry file (`flags.json` at the
repository root). Each entry SHALL declare a `key`, a value `type` (one of `boolean`,
`string`, `number`, `object`), a `default` value matching that type, a human-readable
`description`, the `surfaces` the flag is evaluated on (any of `web`, `hosted`, `cli`),
and an `owner`.

Flag keys SHALL be kebab-case.

#### Scenario: Registry is well-formed

- **WHEN** the continuous integration build runs
- **THEN** it verifies `flags.json` parses, every entry has all required fields, every
  `default` matches its declared `type`, every `surfaces` value is a recognized surface,
  and every `key` is kebab-case
- **AND** the build fails if any check does not hold

#### Scenario: Code and registry cannot drift

- **WHEN** a surface (`web`, `hosted`, or `cli`) evaluates a flag key
- **THEN** an automated check on that surface fails the build if the key is absent from
  `flags.json` or its expected type does not match the registry

### Requirement: Flags are evaluated through a vendor-neutral interface

Each surface SHALL evaluate flags through a provider-agnostic flag interface such that
replacing the flag value source with a different provider requires only registering a
different provider at process startup and no change to any evaluation call site.

#### Scenario: Evaluating a boolean flag

- **WHEN** application code requests the value of a boolean flag key, supplying a coded
  default and an evaluation context
- **THEN** it receives the boolean value resolved by the active provider for that
  context

#### Scenario: Provider swap does not touch call sites

- **WHEN** the active provider for a surface is changed
- **THEN** no code that requests a flag value needs to be modified

### Requirement: Flag evaluation fails open to the coded default

Flag evaluation SHALL never raise an error to the caller. If the active provider errors,
is unavailable, does not recognize the key, or returns a value of the wrong type, the
evaluation SHALL return the caller-supplied coded default.

#### Scenario: Provider error

- **WHEN** the active provider throws or times out while resolving a flag
- **THEN** the evaluation returns the coded default and the calling request or CLI run
  continues normally

#### Scenario: Unknown key

- **WHEN** code evaluates a flag key the active provider has no value for
- **THEN** the evaluation returns the coded default

### Requirement: Every surface supplies a shared evaluation-context shape

When evaluating a flag, each surface SHALL supply an evaluation context with a
`targetingKey` set to the organization identifier (where an organization is known) and
attributes `userId`, `plan`, `projectId`, `surface`, and `env`, using a null/absent
value for any attribute not applicable in that runtime. The attribute names and meanings
SHALL be identical across all three surfaces.

#### Scenario: Web request for a signed-in organization

- **WHEN** the web app evaluates a flag during a request from a signed-in user in an
  organization
- **THEN** the context carries that organization as `targetingKey`, the user id as
  `userId`, the organization's plan as `plan`, `surface` = `web`, and the deployment
  environment as `env`

#### Scenario: CLI run

- **WHEN** the CLI evaluates a flag
- **THEN** the context carries the configured organization as `targetingKey`, the
  configured project as `projectId`, `userId` absent, `plan` set to the known plan or
  `unknown`, and `surface` = `cli`

#### Scenario: Anonymous marketing page

- **WHEN** the web app evaluates a flag with no signed-in user
- **THEN** the context carries no `targetingKey`, `userId` absent, and `surface` = `web`,
  and evaluation still returns a value

### Requirement: Default flag values are served without any external service

In its default configuration the system SHALL resolve all flag values from local
sources only — the registry defaults, local configuration, and local overrides — with
no network call and no third-party account required.

#### Scenario: Offline CLI run

- **WHEN** the CLI runs with no network access
- **THEN** every flag evaluation returns a value (registry default, or an override from
  local project configuration) without error

#### Scenario: Hosted API with no flag service configured

- **WHEN** the hosted API starts with no external flag provider configured
- **THEN** flag evaluation resolves from local configuration and registry defaults

### Requirement: Local overrides can change a flag value per surface

Each surface SHALL allow a flag's resolved value to be overridden locally without a code
change: the web app and hosted API via environment/configuration entries keyed by flag
key, and the CLI via a `featureFlags` map in `releasetwin.yaml`. An override SHALL take
precedence over the registry default.

#### Scenario: Environment override on the hosted API

- **WHEN** a configuration/environment entry sets a value for a known flag key on the
  hosted API
- **THEN** evaluations of that key return the overridden value instead of the registry
  default

#### Scenario: Project config override in the CLI

- **WHEN** `releasetwin.yaml` sets a value for a known flag key under `featureFlags`
- **THEN** the CLI's evaluations of that key return that value

### Requirement: Feature flags are separate from plan entitlements

Feature flags SHALL NOT be the mechanism that grants or denies access to a plan-gated
feature; that remains the responsibility of the existing entitlement system. A flag MAY
read the `plan` attribute from the evaluation context to vary a rollout, but a disabled
flag SHALL NOT be used to enforce plan limits, and enabling a flag SHALL NOT grant an
entitlement the plan does not include.

#### Scenario: Flag does not override an entitlement

- **WHEN** a feature is gated by a plan entitlement and its rollout flag is enabled for
  an organization whose plan lacks the entitlement
- **THEN** the entitlement system still denies the feature

### Requirement: Adding and flipping a flag is documented

The repository SHALL include documentation describing how to add a flag to the registry,
how to read it on each surface, how to change its value in the current (local-only)
configuration, and precisely which registration points change when an external provider
is later adopted.

#### Scenario: Documentation present

- **WHEN** a contributor needs to add or flip a flag
- **THEN** `docs/feature-flags.md` describes the registry entry, the per-surface read
  API, the local override mechanism per surface, the naming convention, and the
  provider-adoption steps
