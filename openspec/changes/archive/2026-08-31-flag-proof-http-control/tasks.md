## 1. Case model + parsing

- [x] 1.1 `ReleaseTwin.Cli` case-loading model: `FlagProofControl` record — `Method`, `Url`, `Headers` (`IReadOnlyDictionary<string,string>`), `Body` (`string?`), `KnownBadWhen` (`enum { Disabled, Enabled }`, default `Disabled`). Add `Control` (`FlagProofControl?`) to `FlagProofDeclaration`.
- [x] 1.2 `FlagProofDto` gains a `control` DTO; `CaseFileLoader.ResolveFlagProof` parses it. `method` required and one of GET/PUT/POST/PATCH/DELETE; `url` required; `known_bad_when` (if present) must be `disabled`/`enabled` else `CaseFileException`.
- [x] 1.3 `${VAR}` interpolation runs over the control block's `url` / `headers` values / `body` via the existing `InterpolateEnvVars` path (make `ResolveFlagProof` non-static / pass the interpolator). `{{featureKey}}` / `{{state}}` / `{{enabled}}` are left intact for execution-time substitution.
- [x] 1.4 Case-load tests: valid control block round-trips; missing `url`; bad `method`; bad `known_bad_when`; `${VAR}` resolved, `{{token}}` preserved.

## 2. Core

- [x] 2.1 `ReleaseTwin.Core/FlagProof.cs` — add `FlagProofOutcome.ControlFailed` ("the feature-state control request failed; the run could not be performed").
- [x] 2.2 `FlagProofRunner.RunAsync` — wrap both `SetStateAsync` calls in `try/catch`; on any exception return a `FlagProofResult` with `Outcome = ControlFailed`, both legs null, and a message. No leg executes after a failed control call.
- [x] 2.3 Core tests: a throwing `IFeatureStateController` → `ControlFailed` (not `Ineligible`, not `WeakOracle`); the known-good control failing after a clean known-bad leg still → `ControlFailed`.

## 3. HTTP feature-state controller

- [x] 3.1 `ReleaseTwin.Adapters.Http/HttpFeatureStateController.cs` — ctor takes the resolved `FlagProofControl`, the feature key, an `HttpClient`. `SetStateAsync(featureKey, enabled, ct)`: compute `flagOn` from `KnownBadWhen` + `enabled` (design D2); substitute `{{featureKey}}` / `{{state}}` (`enabled`/`disabled`) / `{{enabled}}` (`true`/`false`) in url/headers/body; send; throw `FlagControlException` on non-2xx or transport failure, message naming the status and URL.
- [x] 3.2 `HttpAdapter` — `.AddCapability("flag-control:runtime")` (design D3); `IFeatureStateControllerSource` stays absent (documented inline): per-case controllers are built by the CLI, not the adapter composition.
- [x] 3.3 `FlagControlException` — in `ReleaseTwin.Adapters.Http` (Core's `FlagProofRunner` catches it as a plain `Exception`, so its contract need not name it).
- [x] 3.4 Controller tests (fake `HttpMessageHandler`): default polarity sends `disabled` then `enabled`; `known_bad_when: enabled` inverts; `{{featureKey}}` lands in the URL; a 500 → `FlagControlException`.

## 4. CLI wiring

- [x] 4.1 `CliRunner` flag-proof loop: `var controller = flagProof.Control is { } c ? new HttpFeatureStateController(c, flagProof.FeatureKey, httpAdapterClient) : featureStateController;` — use the HTTP adapter's own `HttpClient` (respect `httpAdapterHandlerForTesting`).
- [x] 4.2 `if (controller is null)` → `Ineligible` (unchanged). Otherwise build `FlagProofRunner` with `controller`.
- [x] 4.3 Print `FLAGPROOF <id> (ControlFailed): <message>` and count it as failed; `summary?.AddCase(..., flagProofOutcome: "ControlFailed")`; the hosted upload carries `Outcome` through unchanged (it is already a free string).
- [x] 4.4 CLI integration tests: a case with a `control` block hitting a fake flag endpoint → `Passed` when the legs discriminate; the same with the fake endpoint 500ing → `ControlFailed`; a case with neither `control` nor an adapter controller → `Ineligible` (existing `FlagProofCaseWithNoAzureDevOpsConfiguredIsIneligible`).

## 5. Docs + example

- [x] 5.1 `examples/cases-flag-proof-http/example-flag-proof-http.yaml` + `examples/fixtures/example-flag-proof-http.json` — a `flag_proof.control` placeholder (`${FLAGS_API}` / `${FLAGS_TOKEN}`) with a real read-back pipeline. In its own dir so the credential-less `examples/cases/` end-to-end run stays green.
- [x] 5.2 `docs/flag-proof.md` (new) — outcomes table, the `control` block shape, `{{featureKey}}`/`{{state}}`/`{{enabled}}` + `${ENV}`, `known_bad_when` polarity, credentials-from-env rule, `ControlFailed`.
- [x] 5.3 `README.md` — flag-proof bullet rewritten (adapter controller *or* `control` block), Ineligible sample line + "what's not built yet" bullet updated.

## 6. Verification

- [x] 6.1 `dotnet build ReleaseTwin.sln` (0 errors) + `dotnet test ReleaseTwin.sln` green: 228 passed, 0 failed. Delta +9: Core +2 (`ControlFailed`), Http +3 (`HttpFeatureStateController`), Cli +4 (case-load + integration).
- [x] 6.2 `openspec validate flag-proof-http-control --strict` — valid.
- [x] 6.3 CI green on the branch — PR #61, all checks pass (build-and-test, release-proof, annotate, gitleaks, Vercel).
- [ ] 6.4 **Needs the user to run this:** point `example-flag-proof-http.yaml` at a real flag toggle (a throwaway LaunchDarkly REST call, a Flagsmith/Unleash sandbox, or a tiny self-hosted endpoint) and confirm a real `Passed` and a real `ControlFailed`.
