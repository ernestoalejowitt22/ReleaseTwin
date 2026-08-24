## 1. Case-file loading

- [x] 1.1 Add `FlagProofDto` (`feature_key`, `build_identity`) to `CaseFileDto` (src/ReleaseTwin.Cli/CaseLoading/CaseFileDto.cs), with an optional `FlagProof` property.
- [x] 1.2 Add a `FlagProofDeclaration(string FeatureKey, string BuildIdentity)` record and a `LoadedCase(TestCase Case, FlagProofDeclaration? FlagProof)` wrapper (new file or alongside `CaseFileLoader`).
- [x] 1.3 Update `CaseFileLoader.LoadAll()` (or add a new method) to return `IReadOnlyList<LoadedCase>`, parsing the `flag_proof` block into `FlagProofDeclaration` when present, validating both fields are non-empty when the block exists.
- [x] 1.4 Add loader tests: case with a valid `flag_proof` block, case without one, and a case with a malformed block (e.g. missing `build_identity`) producing a clear `CaseFileException`.

## 2. CLI execution wiring

- [x] 2.1 Update `CliRunner.RunAsync` to consume `LoadedCase` instead of `TestCase` from the loader.
- [x] 2.2 For a case with no `FlagProof` declaration, keep the existing `CaseExecutor.ExecuteAsync` + `PASS`/`FAIL` path unchanged.
- [x] 2.3 For a case with a `FlagProof` declaration and `azureDevOpsAdapter is null`, report it directly as ineligible (no `FlagProofRunner` construction) and count it as non-passing.
- [x] 2.4 For a case with a `FlagProof` declaration and `azureDevOpsAdapter` present, construct a `FlagProofRunner` from `executor`, the composed `ICapabilityCatalog`, and `azureDevOpsAdapter.FeatureStateController`, then call `RunAsync` with the case, feature key, and build identity.
- [x] 2.5 Print a `FLAGPROOF <case-id> (<outcome>)` line for each flag-proof case; treat `FlagProofOutcome.Passed` as a pass and every other outcome as a failure in the passed/failed counters and exit code.

## 3. Upload wiring

- [x] 3.1 When `ingestClient` is configured and a case ran in flag-proof mode, call `ingestClient.UploadFlagProofReportAsync` with the `FlagProofResult` (or the ineligible-case data, if constructable) instead of `UploadCaseReportAsync`.
- [x] 3.2 Keep the existing warning-not-failure behavior for upload exceptions (`HttpRequestException`/`TaskCanceledException`) on this new path.

## 4. Example and end-to-end verification

- [x] 4.1 Add a `flag_proof` block to a copy of `examples/cases/example-claim.yaml` (or a new `examples/cases/example-flag-proof.yaml`) targeting the existing Azure DevOps variable-group demo shape.
- [~] 4.2 Run the CLI against it with real Azure DevOps env vars set (manually, once) and confirm the known-bad/known-good toggle and outcome line behave as designed — this is the actual "testable e2e" proof, not just unit tests. **Partially done**: no real Azure DevOps org/PAT is available in this environment. Verified instead: (a) `dotnet run` against the new example with no Azure DevOps env vars set reports `FLAGPROOF FLAGPROOF-DEMO-1 (Ineligible): no installed adapter exposes feature-state control` and exits non-zero, not a crash; (b) `CliRunnerFlagProofTests` exercises the full known-bad/known-good toggle end-to-end against a fake Azure DevOps handler that actually tracks variable-group state across both legs, producing `FLAGPROOF FP-1 (Passed)`. Running against a real org is still outstanding — flagging for the user to do once real credentials are available.
- [x] 4.3 Update `README.md`'s flag-proof-related section (if any) and `docs/customer-pilot-guide.md`'s "still can't do" / "worth building" lists to reflect that flag-proof is now CLI-runnable for Azure DevOps.

## 5. Tests

- [x] 5.1 Add `CliRunner` tests: flag-proof case with Azure DevOps configured (mocked handler) covering at least one outcome other than `Passed`.
- [x] 5.2 Add a `CliRunner` test: flag-proof case declared with no Azure DevOps configured → reported ineligible, non-zero exit code.
- [x] 5.3 Add a `CliRunner` test: mixed run (one ordinary case, one flag-proof case) produces correct combined pass/fail counts and exit code.
- [x] 5.4 Add a test confirming `UploadFlagProofReportAsync` is called (not `UploadCaseReportAsync`) for a flag-proof case when a token is configured.
