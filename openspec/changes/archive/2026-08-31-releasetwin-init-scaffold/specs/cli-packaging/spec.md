## MODIFIED Requirements

### Requirement: The container runs against a documented default directory without arguments
Running the container with no additional arguments SHALL execute cases from a documented default path; supplying an explicit path argument SHALL override that default, matching the source CLI's positional-argument behavior. The container image SHALL also include the repository's `examples/` tree at a documented read-only path so the CLI's scaffolding `init` command can populate a new project from it without network access or a source checkout.

#### Scenario: No-argument invocation uses the documented default
- **WHEN** a customer runs the container with no arguments, having mounted their case files at the documented default path
- **THEN** the CLI executes cases found at that default path

#### Scenario: An explicit argument overrides the default
- **WHEN** a customer runs the container with a path argument pointing at a different mounted location
- **THEN** the CLI executes cases found at that supplied location instead of the default

#### Scenario: Bundled examples are present for offline scaffolding
- **WHEN** a customer runs the container image with the `init` command's from-examples option, having mounted a writable working directory
- **THEN** the CLI copies the bundled `examples/` tree from the documented image path into the working directory
- **AND** no network access or source checkout is required
