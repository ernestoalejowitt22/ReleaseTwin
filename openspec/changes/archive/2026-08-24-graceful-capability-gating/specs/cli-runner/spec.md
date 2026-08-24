## ADDED Requirements

### Requirement: Effective required capabilities are derived, not just declared
The CLI SHALL compute each loaded case's effective required capabilities as the union of the case file's explicit `requires:` declarations and any capability implied by a known adapter manifest for the operation, prerequisite, and cleanup names actually referenced in that case — so a case is protected from crashing on a missing capability even if its author forgot to declare `requires:` for an operation a known manifest explains.

#### Scenario: Case forgets to declare requires: for a manifest-known operation
- **WHEN** a case's pipeline references an operation name a known adapter manifest maps to a capability, and the case file does not declare that capability in `requires:`
- **THEN** the CLI still includes that capability in the case's effective required capabilities, so a missing installation is reported as missing-capability rather than crashing

#### Scenario: Explicit requires: declarations are preserved
- **WHEN** a case file declares a `requires:` capability that no known manifest would have inferred
- **THEN** that capability remains part of the case's effective required capabilities, unchanged from today's behavior

#### Scenario: Operation unknown to any manifest is unaffected
- **WHEN** a case's pipeline references an operation name that no known adapter manifest explains
- **THEN** the CLI does not infer any additional required capability for it, and existing unknown-reference behavior applies unchanged
