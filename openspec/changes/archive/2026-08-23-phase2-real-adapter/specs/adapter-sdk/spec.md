## ADDED Requirements

### Requirement: Adapter credentials are supplied externally
An adapter SHALL NOT contain a hardcoded credential (API token, key, password, or connection secret) in its source code. Credentials and other adapter-specific configuration SHALL be supplied at construction time via an external source (environment variable or a configuration object passed in), so the same adapter code works whether invoked from a test, a future CLI, or a future CI action without modification.

#### Scenario: Adapter is constructed with externally supplied credentials
- **WHEN** an adapter requiring authentication is constructed
- **THEN** its credential value comes from a parameter, environment variable, or configuration object supplied by the caller, not a literal embedded in the adapter's source

#### Scenario: Adapter source contains no credential literal
- **WHEN** an adapter's source code is inspected
- **THEN** no API token, key, password, or connection secret appears as a hardcoded literal
