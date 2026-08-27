## ADDED Requirements

### Requirement: Case-file environment-variable references can resolve from hosted project secrets
When a case file's `${VAR_NAME}` reference is not satisfied by the local process environment and a
project API token is configured, the CLI SHALL attempt to resolve it from that project's stored
secrets (`project-secrets`) before treating the reference as missing. A local environment variable,
when present, SHALL always take precedence and SHALL NOT trigger a hosted fetch for that name.

#### Scenario: A hosted-stored secret resolves a reference the local environment doesn't have
- **WHEN** a case file references `${VAR_NAME}`, no local environment variable of that name is set,
  and a project API token is configured with that project having a stored secret under that name
- **THEN** the reference resolves to the hosted-stored secret's value

#### Scenario: A local environment variable takes precedence over a hosted-stored secret
- **WHEN** a case file references `${VAR_NAME}`, a local environment variable of that name is set,
  and the project also has a different stored secret under that name
- **THEN** the reference resolves to the local environment variable's value, and no hosted fetch is
  attempted for that name

#### Scenario: Neither source resolves the reference
- **WHEN** a case file references `${VAR_NAME}`, no local environment variable of that name is set,
  and either no project API token is configured or the project has no stored secret under that name
- **THEN** the reference is reported as missing, the same clear load-time error as today
