## ADDED Requirements

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
