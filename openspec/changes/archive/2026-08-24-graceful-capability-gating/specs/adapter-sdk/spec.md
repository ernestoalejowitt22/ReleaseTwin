## MODIFIED Requirements

### Requirement: Unknown operation is a reported error, not silent
When a case references an operation, prerequisite check, or cleanup handler name that no installed adapter has registered, the core SHALL report a configuration/validation error identifying the missing name before attempting execution — unless the case's required capabilities are themselves unavailable, in which case the missing-capability result takes priority and no reference validation is performed for that case.

#### Scenario: Case references an unregistered operation
- **WHEN** a case declares an operation name that no installed adapter provides, and the case's required capabilities (if any) are all available
- **THEN** the core reports an error naming the missing operation and does not attempt to execute the case's pipeline

#### Scenario: Missing capability takes priority over an unknown reference
- **WHEN** a case both requires an unavailable capability and references operation names that belong to no installed adapter
- **THEN** the core reports the missing-capability result, not an unknown-operation error

## ADDED Requirements

### Requirement: Adapters may expose a static known-operation-capability manifest
An adapter type MAY expose a static, instantiation-independent mapping from the operation, prerequisite, and cleanup names it would register to the capability each one requires. When an adapter type exposes this manifest, it SHALL be accessible without constructing an instance of that adapter, so a caller can reason about what a case needs without installing it.

#### Scenario: Caller consults a manifest without installing the adapter
- **WHEN** a caller has a reference to an adapter type that exposes this manifest, but has not constructed or installed an instance of it
- **THEN** the caller can still look up which capability a given operation, prerequisite, or cleanup name belongs to
