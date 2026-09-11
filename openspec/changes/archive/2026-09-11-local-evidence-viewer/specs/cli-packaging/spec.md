## ADDED Requirements

### Requirement: Both published distribution forms expose the view subcommand

The `view` subcommand SHALL be available in every published form of the CLI — the container
image and the .NET global tool — with the same behavior, and SHALL NOT require any dependency
beyond what that form already installs. In particular it SHALL NOT require a browser, a Node
toolchain, an SDK, or a separate download of its rendering assets: everything the report needs
SHALL be carried in the published artifact.

Adding this subcommand SHALL NOT change the runtime prerequisites either form already
documents, so an existing installation gains it on upgrade with no new install step.

#### Scenario: The global tool serves a report

- **WHEN** a user who installed the CLI as a .NET global tool runs the view subcommand against
  a local evidence directory
- **THEN** the report is served with no further installation

#### Scenario: The container image serves a report

- **WHEN** a user runs the published container image's view subcommand against a mounted
  evidence directory
- **THEN** the report is served with no further installation and no additional image

#### Scenario: No new runtime prerequisite is introduced

- **WHEN** the runtime prerequisites of a published form are compared before and after this
  subcommand is added
- **THEN** they are unchanged

### Requirement: The container's documented usage covers publishing the viewer's port

Because a served report is only reachable from the host when the container's port is
published, the container image's documentation SHALL show the view subcommand with a port
published, and SHALL state that the container has no browser of its own so the printed URL is
opened on the host.

The documentation SHALL also name the single-file export as the alternative that needs no
published port.

#### Scenario: The documented container command is runnable as written

- **WHEN** a reader copies the documented container view command
- **THEN** it includes the port publication needed to reach the served report from the host

#### Scenario: The export alternative is documented

- **WHEN** a reader reads the container image's view documentation
- **THEN** it names the single-file export as the way to get the report without publishing a port
