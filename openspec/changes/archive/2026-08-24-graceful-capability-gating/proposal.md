## Why

Running the CLI against `examples/cases/` with no Azure DevOps environment variables set — the README's own documented first step, and the "smoke check" onboarding tier `docs/installation-model.md` claims is achievable in 5-15 minutes with zero credentials — crashes with an unhandled `UnknownReferenceException` instead of skipping the Azure DevOps case gracefully. `CaseExecutor.ExecuteAsync` validates a case's operation/prerequisite/cleanup references against the installed catalog *before* checking whether the case's declared `RequiredCapabilities` are even available, so a correctly-authored case (`example-claim.yaml` already declares `requires: [http:azure-devops]`) still crashes the whole CLI process. Separately, `openspec/specs/core-execution/spec.md` documents no requirement at all for this capability-gating behavior, even though `CaseExecutor` implements a version of it and a passing unit test (`MissingRequiredCapabilityIsDistinctFromAssertionFailure`) already exercises it — the ordering bug lives in a genuine spec gap, not just an implementation slip.

Fixing only the ordering leaves a second, related gap: a case that references a gated operation (e.g. `azdo.createWorkItem`) but *forgets* to declare `requires: [http:azure-devops]` still crashes, because nothing today lets the CLI know that operation name belongs to a capability that might not be installed — that knowledge only exists inside `AzureDevOpsAdapter.Register()`, which never runs when the adapter isn't installed. This change closes both: the ordering bug in Core, and the authoring gap in the CLI via a static, instantiation-independent capability manifest adapters can expose.

## What Changes

- `CaseExecutor.ExecuteAsync` (Core) checks `RequiredCapabilities` before validating operation/prerequisite/cleanup references, so a case with an unavailable required capability reports `missing-capability:<name>` instead of throwing and crashing the whole CLI run.
- Adapters gain an optional, static (no-instantiation-required) manifest mapping their known operation/prerequisite/cleanup names to the capability they require — e.g. `AzureDevOpsAdapter.KnownOperationCapabilities` — so a caller can reason about what a case needs without installing the adapter.
- The CLI's case loader derives each case's effective required capabilities as the union of its explicit `requires:` declarations and whatever `AzureDevOpsAdapter.KnownOperationCapabilities` implies from the operation/prerequisite/cleanup names actually referenced in that case — so a case that forgets `requires:` is caught the same way as one that declares it correctly.
- A case referencing a truly unknown operation name (a real typo, or one no known manifest explains) still throws `UnknownReferenceException` — that failure mode is intentionally preserved, not softened.

**Explicitly out of scope**: whether one bad case file should abort the entire CLI run rather than just that case (a separate, real question, noted in design.md but not resolved here) — this is a real observation but is not what "the crash" refers to in this change's Why: it's specifically about a *correctly-authored* case being unable to run at all, not about blast radius once a case is genuinely malformed.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `core-execution`: adds the requirement that a required-capability check runs before reference validation (currently undocumented anywhere).
- `adapter-sdk`: adds the static known-operation-capability manifest convention for adapters.
- `cli-runner`: case loading derives (not just trusts) each case's required capabilities.

## Impact

- `ReleaseTwin.Core.CaseExecutor`: reorders its existing checks; no new public surface.
- `ReleaseTwin.Adapters.AzureDevOps.AzureDevOpsAdapter`: gains a static `KnownOperationCapabilities` manifest.
- `ReleaseTwin.Cli.CaseLoading.CaseFileLoader` (or `CliRunner`): derives effective `RequiredCapabilities` per case instead of using the case file's `requires:` list verbatim.
- `examples/cases/example-claim.yaml` and any Azure-DevOps-gated example now skip gracefully instead of crashing when Azure DevOps isn't configured — this is the concrete, user-visible fix.
- No hosted-platform, OAuth, or billing changes.
