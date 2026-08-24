## ADDED Requirements

### Requirement: Required-capability check precedes reference validation
The core SHALL check a case's required capabilities before validating that its declared operation, prerequisite, and cleanup references exist in the installed catalog. A case whose required capability is unavailable SHALL report a missing-capability result and SHALL NOT be checked for unknown references, even if it has some.

#### Scenario: Capability-gated case with an uninstalled adapter does not crash
- **WHEN** a case declares a required capability that no installed adapter provides, and its pipeline references operation names that likewise belong to no installed adapter
- **THEN** the core reports a missing-capability result for that case, and the CLI run continues to the next case rather than raising an unhandled exception

#### Scenario: Reference validation still runs for capability-satisfied cases
- **WHEN** a case's required capabilities are all available, but its pipeline references an operation name no installed adapter has registered
- **THEN** the core reports an unknown-reference error identifying the missing operation, exactly as before this change
