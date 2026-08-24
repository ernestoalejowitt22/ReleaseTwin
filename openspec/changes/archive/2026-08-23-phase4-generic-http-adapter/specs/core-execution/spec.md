## ADDED Requirements

### Requirement: Per-step operation parameters
A pipeline step MAY declare a set of named parameters. The core SHALL pass those parameters to the operation's execution, unmodified, so an operation's behavior can be data-driven by the case rather than fixed at adapter-construction time. A step with no declared parameters SHALL still execute correctly, receiving an empty parameter set.

#### Scenario: Operation receives its step's declared parameters
- **WHEN** a pipeline step declares parameters (for example, a URL and an HTTP method)
- **THEN** the operation executing that step receives exactly those parameters, unchanged

#### Scenario: Step with no parameters still executes
- **WHEN** a pipeline step declares no parameters
- **THEN** the operation executing that step receives an empty parameter set and executes normally
