## Why

docs/customer-pilot-guide.md named the real gap: every operation shipped so far is hardcoded (`azdo.createWorkItem` always creates a fixed-shape work item), so no case can test an arbitrary business workflow — only the fixed Azure DevOps demo shape. A usable pilot needs cases that carry real data. Rather than wait for a specific design partner to define that data (which the user has chosen to defer — no partner conversations yet), this change closes the gap generically: operation parameters in Core, plus a vendor-neutral HTTP adapter that can test *any* REST API from the case file alone. This can be demonstrated end-to-end against a live public API today, without needing a real customer's system.

## What Changes

- **`ReleaseTwin.Core`** (deliberate, tracked change — same pattern as the Gap 1 fix): `PipelineStep` gains a `With` parameter dictionary; `IOperation.ExecuteAsync` gains a `parameters` argument. All existing operations (toy adapters, Azure DevOps adapter) are updated to the new signature — none of them need real parameters, so the change is mechanical for them.
- **New `ReleaseTwin.Adapters.Http`**: a generic, vendor-neutral adapter with two parameterized operations:
  - `http.request` — method, URL, headers, body from `with:`; stores the response (status + body) for later assertion.
  - `http.assertJsonPath` — JSONPath expression + expected value from `with:`, checked against the last `http.request` response.
- **`case-loading`**: parses a `with:` block per pipeline step into typed parameters, and supports `${ENV_VAR}` interpolation inside string parameter values (e.g. `url: ${API_BASE_URL}/orders`) so real endpoints/credentials never need to be committed to a case file — matching the syntax an early design note already illustrated.
- **`cli-runner`**: composes both the Azure DevOps adapter and the new HTTP adapter together (the first real test of multi-adapter composition in the actual CLI, not just unit tests) — this is also the trigger condition design.md D2 (phase3) named for revisiting hardcoded single-adapter composition.
- A new example case (`examples/cases/example-http.yaml`) against a live public test API, demonstrating the full loop — authored case, real HTTP call, real JSONPath assertion — with no bespoke adapter code for that API.

## Capabilities

### New Capabilities
- `http-adapter`: parameterized HTTP request execution and JSONPath assertion against any REST API, driven entirely by case-file data.

### Modified Capabilities
- `core-execution`: `PipelineStep` carries per-step parameters; `IOperation` receives them.
- `case-loading`: parses `with:` blocks and resolves `${ENV_VAR}` interpolation in parameter values.
- `cli-runner`: composes multiple adapters (Azure DevOps + HTTP) instead of one hardcoded adapter.

## Impact

- `ReleaseTwin.Core`: breaking change to `IOperation`'s signature and `PipelineStep`'s shape. All existing `IOperation` implementations across `ReleaseTwin.Adapters.ToyHttp`, `ReleaseTwin.Adapters.ToyFile`, and `ReleaseTwin.Adapters.AzureDevOps` updated accordingly.
- New project `ReleaseTwin.Adapters.Http` under `src/`, with a corresponding test project.
- `ReleaseTwin.Cli` and `ReleaseTwin.Cli.CaseLoading` updated for parameters, interpolation, and multi-adapter composition.
- No impact to any prior system.
