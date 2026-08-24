## Purpose

Defines the composition-root contract that lets one or more adapters contribute named operations, prerequisite checks, cleanup handlers, and capability declarations to the core without any adapter-specific code existing inside the core package.

## ADDED Requirements

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
When a case references an operation, prerequisite check, or cleanup handler name that no installed adapter has registered, the core SHALL report a configuration/validation error identifying the missing name before attempting execution.

#### Scenario: Case references an unregistered operation
- **WHEN** a case declares an operation name that no installed adapter provides
- **THEN** the core reports an error naming the missing operation and does not attempt to execute the case's pipeline

### Requirement: Adapter capability declaration
An adapter SHALL be able to declare the capabilities it requires or provides (for example, network access to a specific service class) as data the core can inspect before execution, independent of the adapter's internal implementation.

#### Scenario: Missing required capability is a distinct result
- **WHEN** a case requires a capability that no installed adapter declares as available
- **THEN** the core reports a capability/configuration result distinct from an assertion failure, before executing the pipeline
