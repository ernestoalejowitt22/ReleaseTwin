## 1. Solution scaffolding

- [x] 1.1 Create the .NET solution and projects per design.md D2 (`ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, `ReleaseTwin.Adapters.ToyHttp`, `ReleaseTwin.Adapters.ToyFile`, test projects)
- [x] 1.2 Configure `ReleaseTwin.Core` to have no project reference to any adapter or to `ReleaseTwin.AdapterSdk`'s composition-root implementation, only to shared contract types
- [x] 1.3 Set up CI (build + test) for the new solution

## 2. Core execution kernel (`core-execution`)

- [x] 2.1 Define case/assertion model: case ID, oracle reference, fixture reference with SHA-256 hash
- [x] 2.2 Implement fixture integrity verification gate before any operation runs
- [x] 2.3 Define prerequisite check contract (owner, check) and evaluate-before-execution behavior
- [x] 2.4 Implement ordered pipeline execution with halt-on-unexpected-failure semantics
- [x] 2.5 Implement expected-failure / unexpected-pass handling
- [x] 2.6 Implement cleanup execution in a finally-equivalent path, independent of pipeline outcome
- [x] 2.7 Implement bounded retry and timeout handling with distinct timeout classification
- [x] 2.8 Implement resource-key serialization for concurrent case execution
- [x] 2.9 Implement the four-way failure classification (prerequisite / product / infrastructure / unstable) per design.md D5
- [x] 2.10 Implement the machine-readable report contract (case ID, oracle, fixture hash, outcome, classification, cleanup status, timing)
- [x] 2.11 Unit tests for each requirement and scenario in specs/core-execution/spec.md

## 3. Adapter SDK (`adapter-sdk`)

- [x] 3.1 Define the composition-root contract: register core services, then invoke adapter registration modules
- [x] 3.2 Define adapter contribution surface: named operations, prerequisite checks, cleanup handlers, capability declarations
- [x] 3.3 Implement unknown-operation/check/handler detection as a pre-execution validation error
- [x] 3.4 Implement capability declaration and the missing-required-capability result path
- [x] 3.5 Verify multiple adapters can register in the same composition without collision
- [x] 3.6 Unit tests for each requirement and scenario in specs/adapter-sdk/spec.md

## 4. Flag proof (`flag-proof`)

- [x] 4.1 Implement paired known-bad/known-good execution against the same build identity and fixture hash
- [x] 4.2 Implement the single combined release-proof result (not two independent case reports)
- [x] 4.3 Implement pass/weak-oracle/fail outcome logic per the four leg-result combinations
- [x] 4.4 Implement feature-state eligibility check and the deferred/ineligible result path
- [x] 4.5 Unit tests for each requirement and scenario in specs/flag-proof/spec.md

## 5. Toy adapters (seam validation)

- [x] 5.1 Implement `ReleaseTwin.Adapters.ToyHttp`: auth, two operations, one precondition, one cleanup handler, against `adapter-sdk` only
- [x] 5.2 Implement `ReleaseTwin.Adapters.ToyFile`: structurally different (no HTTP auth), against `adapter-sdk` only
- [x] 5.3 Run an end-to-end case through each toy adapter independently
- [x] 5.4 Run an end-to-end case exercising both toy adapters composed together in one host
- [x] 5.5 Confirm zero changes were needed in `ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` to add either toy adapter; record any exception found and revise specs/design before proceeding

## 6. Quik conceptual fit-check

- [x] 6.1 Read quik-testing's `QuikTestPipeline.cs`, `QuikTestRuntime.cs`, `SuiteHost.cs`, `QuikTestValidator.cs`, and the QFEM precondition/DocuSign cleanup implementations (read-only, no code copied)
- [x] 6.2 Write `docs/quik-fit-check.md` in ReleaseTwin mapping each real Quik operation, precondition, and cleanup handler to the `adapter-sdk` contribution surface, per design.md D4
- [x] 6.3 Flag any Quik mechanic that cannot be expressed without a core change (e.g., stateful envelope handling, multi-step DocuSign auth) as a named gap
- [x] 6.4 For each flagged gap, decide and record: revise the spec now, or explicitly accept as future scope

## 7. Change closeout

- [x] 7.1 Confirm all specs/core-execution, specs/adapter-sdk, specs/flag-proof scenarios have passing tests
- [x] 7.2 Confirm docs/quik-fit-check.md contains no ported Quik code or Quik-owned identifiers beyond operation/concept names needed for the mapping
- [x] 7.3 Run `openspec validate --change phase1-core-extraction --strict` and resolve any findings
