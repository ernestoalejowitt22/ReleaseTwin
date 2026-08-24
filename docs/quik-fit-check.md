# Quik conceptual fit-check

Read-only mapping of quik-testing's real operations, preconditions, cleanup handlers, and outcome model onto the `core-execution` / `adapter-sdk` contracts built in this change (phase1-core-extraction). No quik-testing code was copied; this document describes correspondence and gaps only, referencing quik-testing files by path for traceability.

Sources read: `src/Quik.Testing.Library/Declarative/QuikTestPipeline.cs`, `QuikTestRuntime.cs`, `QuikTestHostResult.cs`, `QuikTestOutcome.cs`, `Preconditions/QfemChecks.cs`, `Preconditions/PreconditionGate.cs`, `Declarative/QuikTestOperations.cs` (`DocuSignVoidEnvelopeCleanupOperation`), and `SuiteHost.cs` (referenced in design.md D1/D4 discovery).

## What fits without a core change

| Quik mechanic | Quik source | Maps to |
|---|---|---|
| Named operation registry, unknown-operation error | `QuikTestPipelineExecutor` (`QuikTestPipeline.cs:76-127`) | `IOperationCatalog` + `CaseExecutor.ValidateReferences` |
| Named cleanup registry, unknown-cleanup error | `QuikTestCleanupExecutor` (`QuikTestPipeline.cs:129-159`) | `ICleanupCatalog` + `CaseExecutor.ValidateReferences` |
| Pipeline steps run in declared order | `QuikTestPipelineExecutor.ExecuteAsync` foreach loop | `CaseExecutor.RunPipelineAsync` |
| Cleanup receives per-run state (envelope ID, credentials) without needing typed context fields | `DocuSignVoidEnvelopeCleanupOperation` reads `pipeline.EnvelopeId`/`pipeline.DocuSignCredentials` (`QuikTestOperations.cs:315-345`) | `CaseExecutionContext.AdapterState` — the create-record operation stashes an ID, the cleanup reads it back, exactly as `ReleaseTwin.Adapters.ToyHttp`'s `CreateRecordOperation`/`DeleteRecordCleanup` already do |
| Cleanup no-ops safely when there is nothing to clean up | `DocuSignVoidEnvelopeCleanupOperation` logs "cleanup skip" and returns when `EnvelopeId` is empty | `CleanupResult(true)` no-op pattern, already used by both toy adapters |
| Fixture hash verification | `FileQuikTestPayloadStore.LoadAsync` SHA-256 check (`QuikTestRuntime.cs:60-69`) | `CaseExecutor` fixture-integrity gate |
| Expected-failure / unexpected-pass as a named outcome | `QuikTestHostResult.Kind.ExpectedFail` / `Kind.UnexpectedPass` (`QuikTestHostResult.cs:9-15`) | `PipelineStep.ExpectFailure` handling in `CaseExecutor.RunPipelineAsync` |
| Precondition owner attribution | `PreconditionGate.DescribeUnmet` names an owner per unmet check | `PrerequisiteDeclaration.Owner` |

These confirm the core/adapter-sdk boundary holds for Quik's actual mechanics, not just the toy adapters — the toy adapters exercised the seam's shape, and this reading confirms the seam matches Quik's real shape too.

## Gaps found

### Gap 1 — Prerequisite results are three-state in Quik, boolean in this change's core

Quik's `PreconditionResult` (via `QfemCheckBase.RunAsync`, `Preconditions/QfemChecks.cs:29-52`) distinguishes three outcomes, not two:
- satisfied,
- not satisfied (a real red — the fixture is genuinely missing/wrong),
- **not evaluated** (the QFEM catalog was unreachable or misconfigured — "a catalog we cannot reach tells us nothing about the fixture; say so; do not invent a verdict in either direction").

`QuikTestHostResult.Kind` reflects this at the outcome level too: `Pass`, `SoftSkip` (inconclusive), `ExpectedFail`, `UnexpectedPass`, `Fail` — five kinds, where `SoftSkip` is categorically different from `Fail`.

This change's `core-execution` spec models `PrerequisiteResult(bool Passed, string? Detail)` — a strict boolean. A prerequisite check that cannot reach its backing system currently has no way to report "inconclusive" distinct from "failed," which would misclassify an infrastructure gap as a `FailureClassification.Prerequisite` result indistinguishable from a genuinely unmet prerequisite.

**Decision**: accept as future scope, not a spec revision now. Rationale: neither toy adapter needed a third state, and introducing it now would be designing ahead of a second real need (per the project's own build-what-you-need discipline). Record it here so the next capability slice that touches prerequisites (or the Quik adapter itself, whenever it is built) revisits `PrerequisiteResult` for a `Satisfied | NotSatisfied | NotEvaluated` shape before assuming boolean is sufficient.

### Gap 2 — Fixture path containment is a Quik concern this change's fixture model doesn't address

`FileQuikTestPayloadStore.ResolvePath` (`QuikTestRuntime.cs:74-103`) enforces that a fixture locator stays within a `payloads/` root (rejects `..`, absolute paths, and root-escaping paths) before ever reading the file, independent of hash verification.

This change's `FixtureReference` takes already-resolved `byte[] Content` — path resolution and containment happen before a `FixtureReference` is constructed, outside `core-execution`'s scope entirely. That is consistent with the spec (fixture *integrity*, not fixture *location*), but worth naming explicitly: any adapter or case-loading layer that resolves a fixture locator to bytes is responsible for its own path-containment check; the core provides no protection here and was never meant to.

**Decision**: no spec revision. This is correctly out of `core-execution`'s scope; note it as a requirement on whatever component loads `FixtureReference.Content` in a later phase (case loader / CLI), not on the core.

## Conclusion

No gap found requires a change to `ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` as currently specified — both gaps are either explicitly deferred (Gap 1) or belong to a different, not-yet-built component (Gap 2). Phase 1's specs and design stand as written. A literal Quik adapter, if built in a later phase, should expect to revisit Gap 1 before it can express QFEM's real precondition semantics faithfully.
