## ADDED Requirements

### Requirement: The CLI is published as a .NET global tool
Each tagged release SHALL also be published as a .NET global tool package to a
public NuGet feed, installable with `dotnet tool install --global` under the
command name `releasetwin`, for users who have the .NET runtime but do not want
to run a container or check out this repository's source.

The tool SHALL be published only after the same build-and-test gate that guards
the container image: a failing build or test SHALL NOT produce a published
package.

The tool's package SHALL carry the engine's license identifier.

#### Scenario: Installing the tool needs no source checkout
- **WHEN** a user with the .NET SDK but no clone of this repository runs `dotnet tool install --global releasetwin` and then invokes `releasetwin` against a case directory
- **THEN** the CLI executes and reports results identically to running it from source

#### Scenario: The tool preserves the case/fixture, env-var, and exit-code contract
- **WHEN** the same case suite is run via the installed tool and via the container image
- **THEN** fixture resolution (sibling `fixtures/` directory), `${ENV_VAR}` interpolation, and the pass/fail exit code are identical between the two

#### Scenario: A release only publishes the tool after verification
- **WHEN** a release is triggered by pushing a version tag and the build or test suite fails
- **THEN** no tool package is pushed to the feed

#### Scenario: A version-pinned tool install is reproducible
- **WHEN** a user installs the tool pinning a specific released version
- **THEN** they receive the exact package published for that release, unaffected by later releases
