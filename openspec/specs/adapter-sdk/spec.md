# adapter-sdk Specification

## Purpose

Defines the composition-root contract that lets one or more adapters contribute named operations, prerequisite checks, cleanup handlers, and capability declarations to the core without any adapter-specific code existing inside the core package.

## Requirements

### Requirement: Core has no adapter-specific references
The core package SHALL NOT reference any concrete operation, client, or vendor name belonging to a specific adapter (for example, no QFE, QFEM, eSign, or DocuSign references, and no references to any adapter built to validate this contract). All product-specific identifiers SHALL originate from adapter registration code.

#### Scenario: Core compiles and runs with zero adapters installed
- **WHEN** the core is composed with no adapters registered
- **THEN** it starts successfully and reports zero available operations, prerequisite checks, and cleanup handlers

### Requirement: Adapters register without editing core types
An adapter SHALL be able to contribute named operations, prerequisite checks, cleanup handlers, and capability declarations entirely through its own registration code, without modifying any core type or interface.

#### Scenario: New adapter installs without core changes
- **WHEN** a new adapter is implemented against the published core contracts and registered at composition time
- **THEN** its operations, prerequisite checks, and cleanup handlers become available to the core without any change to a core file

### Requirement: Multiple adapters compose without conflict
The composition root SHALL support installing more than one adapter at once. Two adapters contributing distinctly named operations SHALL both be available for cases to reference.

#### Scenario: Two adapters installed together
- **WHEN** two independently developed adapters are registered in the same composition
- **THEN** operations, prerequisite checks, and cleanup handlers from both are available, and neither adapter's registration overwrites the other's

### Requirement: Unknown operation is a reported error, not silent
When a case references an operation, prerequisite check, or cleanup handler name that no installed adapter has registered, the core SHALL report a configuration/validation error identifying the missing name before attempting execution — unless the case's required capabilities are themselves unavailable, in which case the missing-capability result takes priority and no reference validation is performed for that case.

#### Scenario: Case references an unregistered operation
- **WHEN** a case declares an operation name that no installed adapter provides, and the case's required capabilities (if any) are all available
- **THEN** the core reports an error naming the missing operation and does not attempt to execute the case's pipeline

#### Scenario: Missing capability takes priority over an unknown reference
- **WHEN** a case both requires an unavailable capability and references operation names that belong to no installed adapter
- **THEN** the core reports the missing-capability result, not an unknown-operation error

### Requirement: Adapters may expose a static known-operation-capability manifest
An adapter type MAY expose a static, instantiation-independent mapping from the operation, prerequisite, and cleanup names it would register to the capability each one requires. When an adapter type exposes this manifest, it SHALL be accessible without constructing an instance of that adapter, so a caller can reason about what a case needs without installing it.

#### Scenario: Caller consults a manifest without installing the adapter
- **WHEN** a caller has a reference to an adapter type that exposes this manifest, but has not constructed or installed an instance of it
- **THEN** the caller can still look up which capability a given operation, prerequisite, or cleanup name belongs to

### Requirement: Adapter capability declaration
An adapter SHALL be able to declare the capabilities it requires or provides (for example, network access to a specific service class) as data the core can inspect before execution, independent of the adapter's internal implementation.

#### Scenario: Missing required capability is a distinct result
- **WHEN** a case requires a capability that no installed adapter declares as available
- **THEN** the core reports a capability/configuration result distinct from an assertion failure, before executing the pipeline

### Requirement: Adapter credentials are supplied externally
An adapter SHALL NOT contain a hardcoded credential (API token, key, password, or connection secret) in its source code. Credentials and other adapter-specific configuration SHALL be supplied at construction time via an external source (environment variable or a configuration object passed in), so the same adapter code works whether invoked from a test, a future CLI, or a future CI action without modification.

#### Scenario: Adapter is constructed with externally supplied credentials
- **WHEN** an adapter requiring authentication is constructed
- **THEN** its credential value comes from a parameter, environment variable, or configuration object supplied by the caller, not a literal embedded in the adapter's source

#### Scenario: Adapter source contains no credential literal
- **WHEN** an adapter's source code is inspected
- **THEN** no API token, key, password, or connection secret appears as a hardcoded literal
