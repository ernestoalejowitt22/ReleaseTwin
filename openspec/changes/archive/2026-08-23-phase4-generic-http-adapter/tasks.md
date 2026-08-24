## 1. Core: operation parameters (`core-execution`)

- [x] 1.1 Add `With` parameter dictionary to `PipelineStep`
- [x] 1.2 Change `IOperation.ExecuteAsync` signature to accept parameters (design.md D1)
- [x] 1.3 Update `CaseExecutor` to pass `step.With ?? empty` into each operation call
- [x] 1.4 Update `ToyHttp` adapter's operations to the new signature (ignore parameters)
- [x] 1.5 Update `ToyFile` adapter's operations to the new signature (ignore parameters)
- [x] 1.6 Update `AzureDevOps` adapter's operations to the new signature (ignore parameters)
- [x] 1.7 Full solution build to confirm no `IOperation` implementation was missed
- [x] 1.8 Unit tests for the new core-execution scenarios (parameters passed through; empty-parameter step still executes)

## 2. Generic HTTP adapter (`http-adapter`)

- [x] 2.1 Create `ReleaseTwin.Adapters.Http` project referencing `ReleaseTwin.AdapterSdk` and `ReleaseTwin.Core`, plus a `Newtonsoft.Json` dependency (design.md D3)
- [x] 2.2 Create `ReleaseTwin.Adapters.Http.Tests` project
- [x] 2.3 Implement `http.request`: method/URL/headers/body from parameters, storing status + body for later assertion
- [x] 2.4 Implement `http.assertJsonPath`: JSONPath + expected value from parameters, checked against the last response
- [x] 2.5 Implement the adapter's `IAdapterModule.Register`, requiring no constructor configuration
- [x] 2.6 Unit tests for each requirement and scenario in specs/http-adapter/spec.md, using a fake `HttpMessageHandler`

## 3. Case loading: parameters and interpolation (`case-loading`)

- [x] 3.1 Parse a step's `with:` block into `PipelineStep.With`
- [x] 3.2 Implement `${ENV_VAR}` interpolation for string parameter values, including nested maps (design.md D4)
- [x] 3.3 Implement the missing-environment-variable load-time error
- [x] 3.4 Unit tests for each new requirement and scenario in specs/case-loading/spec.md

## 4. CLI: multi-adapter composition (`cli-runner`)

- [x] 4.1 Implement conditional Azure DevOps installation: all-5-vars-present → install, none-present → skip, partial → clear startup error (design.md D5)
- [x] 4.2 Install the HTTP adapter unconditionally
- [x] 4.3 Unit tests for each new requirement and scenario in specs/cli-runner/spec.md

## 5. End-to-end demo against a live public API

- [x] 5.1 Author `examples/cases/example-http.yaml` against a stable public test API (e.g. jsonplaceholder), using `http.request` + `http.assertJsonPath`
- [x] 5.2 Confirm it runs successfully via the real CLI, against the real internet (not a fake handler) — this is the actual end-to-end proof
- [x] 5.3 Confirm the Azure DevOps example (Phase 3) still passes unchanged, and that both examples run together in one CLI invocation with only HTTP-adapter credentials-free config

## 6. Change closeout

- [x] 6.1 Update docs/customer-pilot-guide.md: Tier 1/Tier 2 distinction is now partially closed — any REST API is testable without new adapter code; note what's still fixed-shape (Azure DevOps operations) versus parameterized (HTTP)
- [x] 6.2 Update README.md's "What's not built yet" and case-file-format sections
- [x] 6.3 Confirm zero changes were needed in `ReleaseTwin.AdapterSdk`
- [x] 6.4 Run `openspec validate phase4-generic-http-adapter --strict` and resolve any findings
