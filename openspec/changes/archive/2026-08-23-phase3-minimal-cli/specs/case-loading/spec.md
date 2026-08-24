## Purpose

Defines how a declarative case file on disk is parsed into the core's `TestCase` model, so cases can be authored external to any C# code and executed by the CLI.

## ADDED Requirements

### Requirement: Case file parses into the core case model
A case file SHALL declare, at minimum, a case ID, an oracle reference, and a fixture reference, and SHALL parse into a valid `TestCase` usable by `CaseExecutor` without modification.

#### Scenario: Well-formed case file loads successfully
- **WHEN** a case file declares a case ID, oracle reference, fixture reference, and a pipeline of operation names
- **THEN** the loader produces a `TestCase` with those exact values, ready for execution

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
