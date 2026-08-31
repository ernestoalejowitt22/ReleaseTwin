## Purpose

Lets a new user create a working ReleaseTwin project — a runnable starter case, its fixture, and
a config file — in their own directory without cloning this repository or hand-authoring the case
and fixture layout.

## Requirements

### Requirement: `init` scaffolds a runnable project

The CLI SHALL provide an `init` command that, run in a directory, writes a starter project:
a `cases/` directory containing one commented starter case, a `fixtures/` directory containing
that case's fixture with a correct integrity hash, a commented `releasetwin.yaml` config file,
and `.gitignore` entries for local run output. The starter case SHALL exercise a real
`http.request` plus at least one assertion and SHALL require no credentials or configuration to
pass.

#### Scenario: Fresh directory is initialized and runs green

- **WHEN** `init` is run in a directory with no `cases/*.yaml`, then the CLI's run command is run
  in that same directory
- **THEN** `init` writes `cases/`, `fixtures/`, `releasetwin.yaml`, and `.gitignore` entries
- **AND** the run reports the starter case as passing with a zero exit code, using no
  environment variables or credentials

#### Scenario: Scaffolded fixture integrity hash is correct

- **WHEN** `init` writes the starter case and its fixture
- **THEN** the hash recorded in the case file matches the bytes of the written fixture, so case
  loading's fixture-integrity check passes without any manual step

### Requirement: `new` adds a case to an existing project

The CLI SHALL provide a `new <case-id>` command that adds one case file and its matching fixture
to an existing project, using the same starter template with the supplied id.

#### Scenario: Adding a second case

- **WHEN** `new ORDERS-1` is run in a project that already has a `cases/` directory
- **THEN** `cases/ORDERS-1.yaml` and `fixtures/ORDERS-1.json` are written from the starter
  template with the id substituted
- **AND** the existing cases and fixtures are untouched

### Requirement: Scaffolding never overwrites existing work

Scaffolding commands SHALL NOT modify or delete any file that already exists. `init` SHALL refuse
to run when the target `cases/` directory already contains a case file. `new` SHALL refuse when
its target case or fixture file already exists. On refusal the command SHALL write nothing,
print a single-line reason, and exit non-zero. `.gitignore` SHALL only be appended to (created
if absent) and only with lines not already present.

#### Scenario: init refuses on an already-initialized project

- **WHEN** `init` is run in a directory whose `cases/` directory already contains a `.yaml` file
- **THEN** the command exits non-zero with a message that the project is already initialized
- **AND** no file in the directory is created, modified, or removed

#### Scenario: new refuses to clobber a case

- **WHEN** `new ORDERS-1` is run and `cases/ORDERS-1.yaml` already exists
- **THEN** the command exits non-zero and neither `cases/ORDERS-1.yaml` nor
  `fixtures/ORDERS-1.json` is modified

### Requirement: Bundled examples are available offline in the container image

The published container image SHALL include the repository's `examples/` tree at a documented
path. An `init` option SHALL populate the new project from that bundled tree instead of the
single built-in starter. When that path is absent (for example running from source rather than
the image), the option SHALL fail with a message pointing to plain `init`, and plain `init`
SHALL still succeed with no filesystem dependency.

#### Scenario: init from bundled examples in the image

- **WHEN** the container image is run with the `init` command and its from-examples option
- **THEN** the new project is populated from the image's bundled `examples/` tree
- **AND** running the CLI against the scaffolded `cases/` executes those example cases
