# cli-packaging Specification

## Purpose

Distribution of the ReleaseTwin CLI as an installable, versioned artifact a customer can run in their own CI pipeline without cloning or building this repo's source.

## Requirements

### Requirement: The CLI is published as a versioned container image
Each tagged release SHALL be published as a container image to a public registry, installable and runnable without a local .NET SDK.

#### Scenario: Pulling and running the image needs no .NET install
- **WHEN** a customer with Docker (but no .NET SDK) pulls the published image and runs it against a mounted case directory
- **THEN** the CLI executes and reports results identically to running it from source with the .NET SDK

#### Scenario: A release is only published after verification
- **WHEN** a release is triggered by pushing a version tag
- **THEN** the image is published only if the build and full test suite pass; a failing build or test SHALL NOT produce a published image

### Requirement: The container preserves the CLI's case/fixture directory contract
The container SHALL resolve case and fixture files the same way the source CLI does: fixtures are located relative to the mounted cases directory's sibling `fixtures` directory, not the cases directory itself.

#### Scenario: A case referencing a fixture resolves it from the sibling fixtures directory
- **WHEN** a customer mounts a directory containing sibling `cases/` and `fixtures/` subdirectories and runs the container against the mounted `cases/` path
- **THEN** fixture locators in case files resolve against the mounted `fixtures/` directory, identical to source-CLI behavior

### Requirement: The container passes environment variables through unchanged
The container SHALL make environment variables supplied to it available to the CLI process unmodified, preserving the existing `${ENV_VAR}` interpolation and adapter-credential contract.

#### Scenario: A credential env var reaches case file interpolation
- **WHEN** a customer runs the container with an environment variable set that a case file references via `${ENV_VAR}` interpolation
- **THEN** the case executes using that variable's value, identical to source-CLI behavior

### Requirement: The container's exit code reflects the run's pass/fail outcome
The container SHALL exit non-zero if any case fails, and zero if all cases pass, matching the source CLI's exit-code contract so it can be used as a CI gate without adaptation.

#### Scenario: Any case failure produces a non-zero container exit code
- **WHEN** a run against the mounted case directory includes at least one failing case
- **THEN** the container process exits with a non-zero status code

### Requirement: The container runs against a documented default directory without arguments
Running the container with no additional arguments SHALL execute cases from a documented default path; supplying an explicit path argument SHALL override that default, matching the source CLI's positional-argument behavior.

#### Scenario: No-argument invocation uses the documented default
- **WHEN** a customer runs the container with no arguments, having mounted their case files at the documented default path
- **THEN** the CLI executes cases found at that default path

#### Scenario: An explicit argument overrides the default
- **WHEN** a customer runs the container with a path argument pointing at a different mounted location
- **THEN** the CLI executes cases found at that supplied location instead of the default

### Requirement: Each release is identified by an unambiguous version tag
Every published image SHALL be tagged with a semantic version derived from the triggering release tag, in addition to a mutable `latest` tag.

#### Scenario: A version-pinned pull is reproducible
- **WHEN** a customer pulls the image using a specific semantic-version tag
- **THEN** they receive the exact image published for that release, unaffected by later releases
