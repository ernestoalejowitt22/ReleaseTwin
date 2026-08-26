## ADDED Requirements

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
