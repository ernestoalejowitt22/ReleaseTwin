## 1. Core: the ControlUnverified outcome

- [x] 1.1 Add `FlagProofOutcome.ControlUnverified` to `src/ReleaseTwin.Core/FlagProof.cs` with an XML-doc line matching the spec's definition (set accepted, read-back contradicted the intended state).
- [x] 1.2 In `FlagProofRunner.RunAsync`, add a `catch (FlagStateUnverifiedException ex)` ahead of the existing `FlagControlException`/generic catch, mapping to a `FlagProofResult` with `Outcome = ControlUnverified` and a message naming the leg ("known-bad" / "known-good") whose state could not be confirmed.
- [x] 1.3 Define `FlagStateUnverifiedException` — decide placement: Core (next to `IFeatureStateController`) so the runner can catch it without referencing the HTTP adapter. Add it in `FlagProof.cs`.
- [x] 1.4 Core unit tests: read-back-unverified on the known-good set → `ControlUnverified` (not `WeakOracle`/`ControlFailed`); verified sets + discriminating legs → `Passed`; no exception path unchanged.

## 2. HTTP adapter: read-back execution

- [x] 2.1 Extract the JSONPath compare core of `JsonPathAssertOperation` (`JToken.Parse(body).SelectToken(path)` → `actual?.ToString()` → ordinal equals) into an internal shared helper in `ReleaseTwin.Adapters.Http`; refactor `JsonPathAssertOperation` to call it; keep its behavior identical (existing Http adapter tests stay green).
- [x] 2.2 Extend `HttpFeatureStateController` with an optional verify config (method default `GET`, url, headers defaulting to the control block's headers, optional body, jsonpath, expected).
- [x] 2.3 In `SetStateAsync`, after the control request returns 2xx and when verify config is present: substitute `{{featureKey}}`/`{{state}}`/`{{enabled}}` into the verify url/headers/body/expected, issue the read request, and evaluate the assertion via the 2.1 helper.
- [x] 2.4 On verify read non-2xx / unsendable → throw `FlagControlException` (→ `ControlFailed`). On assertion mismatch → throw `FlagStateUnverifiedException` with method, URL, jsonpath, expected, and scalar actual only (no full body).
- [x] 2.5 Http adapter tests (stub `HttpMessageHandler`): matching read-back → returns normally; mismatched read-back → `FlagStateUnverifiedException`; 503 read-back → `FlagControlException`; verify headers fall back to control headers; `{{enabled}}` in `expected` matches a JSON boolean; inverted polarity asserts the right per-leg value.

## 3. CLI: parse and validate `flag_proof.control.verify`

- [x] 3.1 Extend the case loader's `flag_proof.control` parsing to read the optional `verify` sub-block into the model passed to `HttpFeatureStateController`.
- [x] 3.2 Validate `verify` on the same path as `control`/`http.request`: `url` required non-empty, `method` a known verb, `jsonpath` and `expected` required non-empty; `${ENV_VAR}` resolved at load time (same passive contract as `control` — no separate literal-credential scanner exists in the loader). A `verify` block is structurally nested under `control`, so it cannot exist without one.
- [x] 3.3 CLI case-loading tests for each validation rule above and for a well-formed `verify` block round-tripping into the controller config.

## 4. Reporting

- [x] 4.1 `CliRunner`: add the `ControlUnverified` console line (`FLAGPROOF <id> (ControlUnverified): ...` with the message) and ensure `summary?.AddCase(..., flagProofOutcome: "ControlUnverified", passed: false, ...)` is emitted.
- [x] 4.2 Confirm the machine-readable run summary schema/tests accept the new `flagProofOutcome` string; update any enum/fixture that lists the allowed values. The GitHub Action's `render.mjs` passes the string through — verify it renders a failing verdict for `ControlUnverified` (add a case to its fixture if one enumerates outcomes).

## 5. Docs and examples

- [x] 5.1 Add a `verify` block to `examples/**/example-flag-proof-http.yaml` (or wherever the flag-proof HTTP example lives) with a brief comment.
- [x] 5.2 Document the `verify` block and the `ControlUnverified` outcome in the flag-proof docs (`docs/`), including the "only for read-your-writes-consistent flag services" caveat from design.md.

## 6. Verification

- [x] 6.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the engine/hosted test counts.
- [x] 6.2 `openspec validate flag-proof-control-readback --strict` passes.
- [x] 6.3 Covered by `web/cypress/e2e/launchdarkly-http-flag-control.cy.ts` + `.github/workflows/ld-http-flag-control-e2e.yml` (change `flag-control-verify-ld-e2e`): a real `control` PATCH + `verify` GET round trip against LaunchDarkly's REST API asserting a real `FLAGPROOF … (Passed)`. The deterministic real-endpoint `ControlUnverified` leg was found not reliably reproducible and stays unit-covered (that change's proposal, Non-Goals).
