## 1. Core: reorder the checks

- [x] 1.1 In `CaseExecutor.ExecuteAsync` (src/ReleaseTwin.Core/CaseExecutor.cs), move the `RequiredCapabilities` availability loop ahead of the `ValidateReferences(testCase)` call, keeping both inside the existing `try`/`finally` around `resourceLock`.
- [x] 1.2 Add a Core test: a case with an unavailable required capability *and* pipeline/prerequisite/cleanup references to operations no installed adapter provides reports `missing-capability:<name>` and does not throw.
- [x] 1.3 Confirm the existing `UnknownOperationThrowsBeforeExecution` and `MissingRequiredCapabilityIsDistinctFromAssertionFailure` tests still pass unmodified.

## 2. Adapter-sdk: static known-operation-capability manifest

- [x] 2.1 Add `public static readonly IReadOnlyDictionary<string, string> KnownOperationCapabilities` to `AzureDevOpsAdapter` (src/ReleaseTwin.Adapters.AzureDevOps/AzureDevOpsAdapter.cs), mapping every operation/prerequisite/cleanup name it registers (`azdo.areaPathExists`, `azdo.createWorkItem`, `azdo.getWorkItem`, `azdo.transitionWorkItemState`, `azdo.readFeatureVariable`, `azdo.deleteWorkItem`) to `"http:azure-devops"`.
- [x] 2.2 Add a test asserting the manifest is accessible and correct without constructing an `AzureDevOpsAdapter` instance.
- [x] 2.3 Add a test (or assertion within an existing registration test) that every name in `KnownOperationCapabilities` matches a name `AzureDevOpsAdapter.Register` actually registers, and vice versa, so the manifest can't silently drift from the real registrations.

## 3. CLI: derive effective required capabilities

- [x] 3.1 In `CliRunner.RunAsync` (src/ReleaseTwin.Cli/CliRunner.cs), after loading each `LoadedCase`, compute the union of `loadedCase.Case.RequiredCapabilities` and any capability implied by `AzureDevOpsAdapter.KnownOperationCapabilities` for operation/prerequisite/cleanup names present in that case's pipeline/prerequisites/cleanup.
- [x] 3.2 Pass a `TestCase with { RequiredCapabilities = <union> }` to the executor (and to `FlagProofRunner` for flag-proof cases) instead of the raw loaded case.
- [x] 3.3 Add a `CliRunner` test: a case referencing `azdo.*` operations with no `requires:` declared and no Azure DevOps configured reports missing-capability (not a crash) and a non-zero exit code, without needing the case file to declare `requires:` at all.
- [x] 3.4 Add a `CliRunner` test confirming a case that already declares `requires:` for a capability no manifest would have inferred is unaffected (regression guard for the union, not a replacement).

## 4. Verify the actual fix

- [x] 4.1 Run `dotnet run --project src/ReleaseTwin.Cli -- examples/cases` with no Azure DevOps env vars set and confirm it no longer crashes — `example-claim.yaml` should report a graceful missing-capability result and `example-http.yaml` (and `example-flag-proof.yaml`) should still run normally, with a correct overall exit code.
- [x] 4.2 Update README.md if its current example-run instructions or output sample need correcting now that this actually works as documented.

## 5. Full regression

- [x] 5.1 `dotnet build ReleaseTwin.sln` and `dotnet test ReleaseTwin.sln` both clean.
