# case-loading Specification

## Purpose

Defines how a declarative case file on disk is parsed into the core's `TestCase` model, so cases can be authored external to any C# code and executed by the CLI.

## Requirements

### Requirement: Case file parses into the core case model
A case file SHALL declare, at minimum, a case ID, an oracle reference, and a fixture
reference, and SHALL parse into a valid `TestCase` usable by `CaseExecutor` without
modification.

A case file MAY declare an optional `release` label — a free-form short string naming the
release, sprint, or epic the case belongs to. When present it SHALL parse into an optional
field on the core case model. The label SHALL NOT affect execution, capability eligibility,
flag-proof behavior, or exit code; it is carried for grouping only. Its absence SHALL be
valid and SHALL behave exactly as before this field existed.

#### Scenario: Well-formed case file loads successfully
- **WHEN** a case file declaring a case ID, an oracle reference, and a fixture reference is loaded
- **THEN** it parses into a valid `TestCase` with those values populated

#### Scenario: A release label is parsed when present
- **WHEN** a case file additionally declares a `release` label
- **THEN** the parsed case model carries that label as an optional string

#### Scenario: A case with no release label is unaffected
- **WHEN** a case file declares no `release` label
- **THEN** it parses and executes exactly as it did before this field existed

### Requirement: Fixture content is resolved relative to the case file's fixture root
A case file's fixture reference SHALL name a fixture by a relative locator; the loader SHALL resolve that locator to file content and compute its SHA-256 hash itself, matching against the fixture reference's own declared hash if one is present.

#### Scenario: Fixture content is loaded and hash-verified at load time
- **WHEN** a case file references a fixture by relative path and declares its expected SHA-256 hash
- **THEN** the loader reads the fixture content from disk and produces a `FixtureReference` whose content matches what `CaseExecutor`'s own fixture-integrity check will verify

#### Scenario: Fixture locator cannot escape the fixture root
- **WHEN** a case file's fixture locator contains `..` or an absolute path
- **THEN** the loader rejects the case file with a clear error rather than reading a file outside the intended fixture root

### Requirement: Malformed case files are reported clearly
A case file that is missing a required field, references a field with the wrong type, or is not valid YAML SHALL be rejected with an error naming the file and the specific problem, before any case in the batch is executed.

#### Scenario: Missing required field is rejected before execution
- **WHEN** a case file omits its case ID or oracle reference
- **THEN** the loader reports an error naming the file and the missing field, and no case in the same load batch is executed

### Requirement: Pipeline step parameters are parsed from the case file
A pipeline step in a case file MAY declare a `with:` block of named parameters. The loader SHALL parse that block into the parameters carried by the resulting `PipelineStep`.

#### Scenario: Step parameters load into the pipeline step
- **WHEN** a case file's pipeline step declares a `with:` block containing named values
- **THEN** the loaded `PipelineStep` carries exactly those named values as its parameters

### Requirement: Environment variable interpolation in parameter values
A string parameter value in a case file MAY reference an environment variable using `${VAR_NAME}` syntax. The loader SHALL resolve that reference to the environment variable's value at load time, so real endpoints and credentials never need to be committed to a case file.

#### Scenario: Environment variable reference resolves to its value
- **WHEN** a parameter value contains `${VAR_NAME}` and the environment variable `VAR_NAME` is set
- **THEN** the loaded parameter value has `${VAR_NAME}` replaced with the environment variable's actual value

#### Scenario: Missing environment variable is a clear load-time error
- **WHEN** a parameter value references `${VAR_NAME}` and that environment variable is not set
- **THEN** the loader rejects the case file with an error naming the file and the missing variable, before any case in the batch is executed

### Requirement: Captured-value references are distinct from environment-variable interpolation
A parameter value MAY reference a name captured by an earlier pipeline step, using syntax distinct
from the existing `${VAR_NAME}` environment-variable interpolation. Unlike environment-variable
interpolation, which resolves once at load time, a captured-value reference SHALL resolve at
pipeline-execution time, when the referencing step actually runs — it cannot resolve at load time,
since the value doesn't exist until an earlier step has already executed.

#### Scenario: A captured-value reference is left unresolved at load time
- **WHEN** a case file is loaded and a parameter value references a captured name
- **THEN** loading succeeds without error even though the referenced value doesn't exist yet, and
  resolution happens later, when the pipeline reaches the referencing step

#### Scenario: Environment-variable interpolation is unaffected
- **WHEN** a case file uses `${VAR_NAME}` environment-variable interpolation elsewhere
- **THEN** it continues to resolve at load time exactly as before, unaffected by the addition of
  captured-value references

### Requirement: A project manifest at the cases-directory root is discovered and parsed

The loader SHALL look for a file named `releasetwin.yml` (a `releasetwin.yaml`
spelling SHALL also be accepted) at the root of the directory of case files being
loaded — the cases directory itself. When the file is present it SHALL be parsed
into project-level defaults before any case in the batch is loaded, and SHALL NOT
itself be loaded as a case. When the file is absent, loading SHALL behave exactly
as it did before this capability existed.

The manifest SHALL support at least a `flag_proof.control` section holding the
same fields a case's inline `control` block accepts (`method`, `url`, `headers`,
`body`, `known_bad_when`, `auth`, `verify`). The manifest SHALL NOT carry
`feature_key` or `build_identity` — those remain per-case. A `${ENV_VAR}`
reference in the manifest SHALL resolve at load time from the environment or the
injected secret resolver, on the same terms as a `${ENV_VAR}` in a case file; the
manifest SHALL NOT contain a literal credential.

#### Scenario: A manifest is discovered and applied

- **WHEN** a directory of case files contains a `releasetwin.yml` declaring a `flag_proof.control` section
- **THEN** the manifest is parsed once before the batch loads and its `control` fields are available as defaults to every `flag_proof` case in that directory

#### Scenario: No manifest is unchanged behavior

- **WHEN** a directory of case files contains no `releasetwin.yml`
- **THEN** every case loads and executes exactly as it did before this capability existed

#### Scenario: A malformed manifest is rejected before any case runs

- **WHEN** `releasetwin.yml` is not valid YAML, contains an unknown key, or declares a field of the wrong type
- **THEN** the loader reports an error naming `releasetwin.yml` and the specific problem, and no case in the batch is executed

#### Scenario: A manifest env-var reference resolves at load time

- **WHEN** the manifest's `flag_proof.control` declares an `Authorization` header of `Bearer ${FLAGS_TOKEN}` and `FLAGS_TOKEN` is set
- **THEN** the resolved token is used and the manifest file holds only the `${FLAGS_TOKEN}` reference

#### Scenario: A missing manifest env var is a clear load-time error

- **WHEN** the manifest references `${FLAGS_TOKEN}` and that variable is not set (and no project secret provides it)
- **THEN** the loader rejects loading with an error naming `releasetwin.yml` and the missing variable, before any case in the batch runs
