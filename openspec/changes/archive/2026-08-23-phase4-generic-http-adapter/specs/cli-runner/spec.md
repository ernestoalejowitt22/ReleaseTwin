## ADDED Requirements

### Requirement: Multiple adapters compose in the CLI
The CLI SHALL be able to install more than one adapter into the same composition, so a case can reference operations from any installed adapter. An adapter that requires no credentials (e.g. a generic HTTP adapter) SHALL install successfully without any credential environment variables being set.

#### Scenario: Cases from two different adapters run in the same invocation
- **WHEN** the CLI is run with a cases directory containing one case using Azure DevOps operations and one case using generic HTTP operations
- **THEN** both cases execute successfully in the same run, using their respective adapters
