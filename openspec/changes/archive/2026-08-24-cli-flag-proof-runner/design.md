## Context

`FlagProofRunner.RunAsync(TestCase, featureKey, buildIdentity, requiredCapability, ...)` (src/ReleaseTwin.Core/FlagProof.cs) already takes the feature key and build identity as plain arguments, not as fields on `TestCase` — Core has no notion of "this case is a flag-proof case." `TestCase` itself (src/ReleaseTwin.Core/Model.cs) stays unchanged by this design; flag-proof-ness is purely a CLI-level fact about how a loaded case should be run. `CliRunner` already holds a typed `AzureDevOpsAdapter?` reference before composition (see src/ReleaseTwin.Cli/CliRunner.cs) — it's the only adapter today with an `IFeatureStateController`. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Let a case file opt into flag-proof mode and have the CLI run it correctly end-to-end against Azure DevOps.
- Keep `ReleaseTwin.Core` and `ReleaseTwin.AdapterSdk` untouched — this is a CLI-only wiring change.

**Non-Goals:**
- A generic, adapter-agnostic way to discover `IFeatureStateController` from *any* installed adapter. Only `AzureDevOpsAdapter` provides one today; building a registration mechanism for a single implementation is premature (matches `docs/installation-model.md`'s existing deferred item: "generic (non-Azure-DevOps) flag-proof mechanism").
- Hosted platform, dashboard, or OAuth changes of any kind.

## Decisions

**Case-file shape**: add an optional `flag_proof` block to case YAML, sibling to `requires`/`preconditions`:
```yaml
flag_proof:
  feature_key: release-proof-feature
  build_identity: build-123
```
Parsed into a new `FlagProofDto` on `CaseFileDto` (src/ReleaseTwin.Cli/CaseLoading/CaseFileDto.cs). `CaseFileLoader` returns a small CLI-local wrapper, e.g. `LoadedCase(TestCase Case, FlagProofDeclaration? FlagProof)`, instead of loading straight to `IReadOnlyList<TestCase>` — `TestCase`/Core stay unaware flag-proof exists at the case-file level.
- *Alternative considered*: encode flag-proof mode inside `requires` (e.g. `requires: [flag-proof]`) reusing the existing string-list field. Rejected — feature key and build identity are structured data, not a capability name, and cramming them into a string would need ad-hoc parsing.

**Controller discovery**: `CliRunner` passes its own already-typed `azureDevOpsAdapter?.FeatureStateController` directly to a `FlagProofRunner` it constructs per flag-proof case. If `azureDevOpsAdapter` is `null` (not installed/configured), the CLI reports that case as ineligible immediately, without calling into `FlagProofRunner` at all — there's no controller instance to hand it. When the adapter is present, `FlagProofRunner`'s own existing capability check (`flag-control:runtime`, already advertised by `AzureDevOpsAdapter.Register`) still applies as the inner safety net; the CLI-level check only covers the case where the adapter isn't installed at all.
- *Alternative considered*: an `IAdapterModule` extension interface (e.g. `IFeatureStateControllerProvider`) that any adapter could implement, with `CompositionRoot` collecting them generically. Rejected for now — one implementation doesn't justify a new adapter-sdk contract; revisit when a second adapter needs it (matches the Non-Goals above).

**Exit code and output**: flag-proof cases are tracked in the same passed/failed counters as ordinary cases — `FlagProofOutcome.Passed` counts as a pass, everything else (`WeakOracle`, `BothFailed`, `Inverted`, `Ineligible`) counts as a failure. Output line format mirrors the existing `PASS`/`FAIL` lines but names the outcome explicitly, e.g. `FLAGPROOF <case-id> (<outcome>)`, so CI logs and humans can distinguish it from a plain case result at a glance.

**Upload**: `IngestClient.UploadFlagProofReportAsync(FlagProofResult, ...)` already exists (src/ReleaseTwin.Cli/Upload/IngestClient.cs) but is currently unused — no code path constructs a `FlagProofResult` to pass it. `CliRunner` calls it for flag-proof cases instead of `UploadCaseReportAsync`, once it has a result to give it. Same warning-not-failure behavior as today applies to both.

## Risks / Trade-offs

- [Only Azure DevOps can run flag-proof cases from the CLI, same as today's flag-proof demo scope] → Mitigation: this is explicitly the existing state (see docs/customer-pilot-guide.md), not a regression; a generic HTTP-based flag controller is tracked as future work, not blocking this change.
- [A case declaring `flag_proof` in an environment with no Azure DevOps configured always reports ineligible] → Mitigation: matches `FlagProofRunner`'s own designed behavior for the missing-capability case; the CLI-level short-circuit just avoids constructing a runner with a null controller, it doesn't change the observable outcome.
