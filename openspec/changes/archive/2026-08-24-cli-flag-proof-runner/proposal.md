## Why

`ReleaseTwin.Core.FlagProofRunner` and `AzureDevOpsAdapter`'s `IFeatureStateController` already exist and work, but `CliRunner` never constructs a `FlagProofRunner` — it only ever calls `CaseExecutor.ExecuteAsync` in a flat per-case loop. Flag proof is the product's most differentiated mechanic (per `openspec/specs/flag-proof/spec.md` and `docs/customer-pilot-guide.md`), yet nothing lets a customer actually trigger it from the CLI today. This is the last gap before the project has a testable end-to-end path — local CLI only, no hosted/OAuth work involved.

## What Changes

- Case files gain an optional way to declare flag-proof mode: a feature key to toggle and a build identity, following the existing YAML style of `requires`/`preconditions` in `examples/cases/example-claim.yaml`.
- `CliRunner` runs any case that declares flag-proof mode through `FlagProofRunner` instead of a plain `ExecuteAsync`, using the `IFeatureStateController` exposed by whichever installed adapter provides one (today: `AzureDevOpsAdapter.FeatureStateController`).
- CLI console output reports the `FlagProofOutcome` (Passed/WeakOracle/BothFailed/Inverted/Ineligible) per flag-proof case, distinct from plain PASS/FAIL.
- The exit-code contract extends to flag-proof cases: any outcome other than `Passed` counts as a failure for the overall exit code.
- The optional upload path starts using `IngestClient.UploadFlagProofReportAsync` (already implemented but currently dead code — no caller builds a `FlagProofResult` to pass it), matching the "and flag-proof result, where applicable" language already present in `openspec/specs/cli-runner/spec.md`'s upload requirement.
- A new example case file under `examples/cases/` demonstrates flag-proof end-to-end against the existing Azure DevOps example, so the path is provably testable, not just unit-tested.

**Explicitly out of scope**: any hosted-platform, dashboard, OAuth app registration, or billing work. This change touches only `ReleaseTwin.Cli`, `ReleaseTwin.Core` (if needed for wiring, not new mechanics), and the existing `ReleaseTwin.Adapters.AzureDevOps` adapter.

## Capabilities

### New Capabilities
(none — flag-proof's core mechanics are already fully specified in `openspec/specs/flag-proof/spec.md`; this change only wires an existing capability into the CLI)

### Modified Capabilities
- `cli-runner`: gains the ability to run cases in flag-proof mode (declared in the case file), report `FlagProofOutcome` per case, fold flag-proof outcomes into the overall exit code, and upload `FlagProofResult` data when a token is configured.

## Impact

- `ReleaseTwin.Cli.CaseLoading.CaseFileDto` / `CaseFileLoader`: new optional case-file fields for flag-proof mode (feature key, build identity).
- `ReleaseTwin.Cli.CliRunner`: branches per case between `CaseExecutor.ExecuteAsync` and `FlagProofRunner.RunAsync`; must decide how to react when a case declares flag-proof mode but no installed adapter exposes an `IFeatureStateController` (distinct from `FlagProofRunner`'s own capability-based `Ineligible` outcome).
- `ReleaseTwin.Cli.Upload.IngestClient`: its existing but currently-unused `UploadFlagProofReportAsync` gets its first real caller — no contract change needed.
- `examples/cases/`: new or updated example case file demonstrating flag-proof against Azure DevOps.
- No changes to `ReleaseTwin.Core.FlagProofRunner` itself, `ReleaseTwin.AdapterSdk`, or the hosted platform.
