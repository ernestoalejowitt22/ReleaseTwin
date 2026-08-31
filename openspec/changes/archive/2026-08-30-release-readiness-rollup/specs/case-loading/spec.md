## MODIFIED Requirements

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
