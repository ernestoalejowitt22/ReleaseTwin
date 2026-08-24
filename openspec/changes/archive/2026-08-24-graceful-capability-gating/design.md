## Context

`CaseExecutor.ExecuteAsync` (src/ReleaseTwin.Core/CaseExecutor.cs) calls `ValidateReferences(testCase)` first, which throws `UnknownReferenceException` for any operation/prerequisite/cleanup name not in the installed catalog — before the `RequiredCapabilities` loop a few lines later ever runs. `openspec/specs/core-execution/spec.md` documents neither check's existence today; the capability-gate behavior only shows up in `openspec/specs/adapter-sdk/spec.md`'s "Adapter capability declaration" requirement and one Core unit test (`MissingRequiredCapabilityIsDistinctFromAssertionFailure`), which already assumes the capability check wins. See proposal.md for the concrete failure this causes (`example-claim.yaml` crashing the whole CLI run with no Azure DevOps configured).

`CliRunner` (src/ReleaseTwin.Cli/CliRunner.cs) already imports `AzureDevOpsAdapter` by name and special-cases it (constructs it only when its 5 env vars are present) — it is not adapter-agnostic today, so adding a second place where it knows about that specific type is consistent with its existing shape. `CaseFileLoader` (src/ReleaseTwin.Cli/CaseLoading/CaseFileLoader.cs), by contrast, knows nothing about adapters — it only parses YAML into `TestCase`/`LoadedCase` values, including the `requires:` list verbatim into `TestCase.RequiredCapabilities`.

## Goals / Non-Goals

**Goals:**
- A case that correctly declares `requires:` for a capability that isn't installed reports `missing-capability`, never throws.
- A case that references a known-adapter operation but forgets to declare the matching `requires:` gets the same protection, via a static manifest the CLI can consult without installing the adapter.
- Keep `ReleaseTwin.Core` adapter-agnostic — it still just trusts whatever `RequiredCapabilities` it's given; it does no manifest lookups itself.

**Non-Goals:**
- Whether one malformed case should abort the whole CLI run rather than just that case. Real, but separate — this change is about capability-gated cases running at all, not about blast radius for cases that are still genuinely broken (unknown operation, no manifest explains it).
- A generic, adapter-agnostic mechanism for the CLI to discover *all* installable adapter types automatically (e.g. via reflection over loaded assemblies). `CliRunner` already hardcodes which adapter types exist; this change extends that existing pattern, it doesn't replace it.
- Applying manifest-based inference to the HTTP adapter — it's installed unconditionally, so none of its operations can ever be "missing" for capability reasons.

## Decisions

**Reorder in `CaseExecutor.ExecuteAsync`**: move the `RequiredCapabilities` availability loop ahead of `ValidateReferences`, keeping both inside the existing `try`/`finally` around `resourceLock`. A case with an unavailable required capability now returns the existing `missing-capability:<name>` report before any reference validation runs; a case with satisfied capabilities still gets full reference validation, unchanged. This matches what `MissingRequiredCapabilityIsDistinctFromAssertionFailure` already assumes and what `UnknownOperationThrowsBeforeExecution` still requires (that test declares no `requires:`, so it's unaffected).

**Manifest shape**: `AzureDevOpsAdapter` gains `public static readonly IReadOnlyDictionary<string, string> KnownOperationCapabilities`, mapping each operation/prerequisite/cleanup name it registers (`azdo.createWorkItem`, `azdo.areaPathExists`, etc.) to `"http:azure-devops"`. A flat dictionary, not three separate ones per reference kind — none of today's adapters reuse a name across operations/prerequisites/cleanups, and keeping it flat matches how simple the lookup needs to be. Built as a `static readonly` field literal, not computed from `Register()`, so it exists without ever constructing `AzureDevOpsAdapter` (no options, no HTTP client).
- *Alternative considered*: a static-abstract interface member (C# 11 static abstract members, available on net8.0) so this is an enforced adapter-sdk contract rather than a per-type convention. Rejected for now — it forces every adapter, including toy ones with no gated operations, to implement it, and it only pays off once more than one adapter needs it; today only Azure DevOps does. Worth revisiting in adapter-sdk if a second adapter needs the same thing.

**Where the union happens**: `CliRunner`, not `CaseFileLoader`. `CliRunner` already imports and special-cases `AzureDevOpsAdapter`; `CaseFileLoader` stays adapter-agnostic, matching its current design. After loading, `CliRunner` computes each case's effective capabilities as `testCase.RequiredCapabilities` unioned with `AzureDevOpsAdapter.KnownOperationCapabilities` lookups for every operation/prerequisite/cleanup name actually present in that case's `Pipeline`/`Prerequisites`/`Cleanup`, and passes a `TestCase with { RequiredCapabilities = ... }` to the executor instead of the raw loaded one.
- *Alternative considered*: do the union inside `CaseFileLoader`. Rejected — would make the loader depend on `ReleaseTwin.Adapters.AzureDevOps`, an adapter-specific reference the loader has never had, for a concern that's really about composition (which adapters exist), which is `CliRunner`'s job already.

**Unknown-reference priority**: when both an unavailable capability and an unknown reference are present on the same case, the capability result wins and no reference validation happens at all for that case (see the `adapter-sdk` delta's new scenario). This isn't a new judgment call — it falls directly out of the reorder — but it's worth being explicit that "unknown operation" errors are now scoped to cases whose declared/derived capabilities are all satisfied.

## Risks / Trade-offs

- [A case with a genuinely unrecognized operation name still throws and, per current (unchanged) behavior, aborts the whole CLI run, not just that case] → Mitigation: explicitly out of scope (Non-Goals) — the manifest closes the specific "forgot `requires:` for a *known* operation" gap, not blast radius for actually-malformed cases. Worth its own change if it turns out to matter in practice.
- [The static-manifest convention isn't enforced by any interface, so a future adapter's author could simply not add one, silently reintroducing the crash for their operations] → Mitigation: acceptable for a single-adapter convention; revisit as an enforced adapter-sdk contract (static abstract interface member) once a second adapter needs the same protection.
- [Flat dictionary assumes no name collisions between operation/prerequisite/cleanup names within one adapter] → Mitigation: true of every adapter built so far (names are already dot-namespaced and hand-picked to be unique); not worth three dictionaries for a property that's held by convention today anyway.
