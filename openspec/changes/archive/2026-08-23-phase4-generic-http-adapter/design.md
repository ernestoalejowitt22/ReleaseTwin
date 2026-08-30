## Context

See proposal.md - Why. This design covers the parameter type threaded through `IOperation`, the JSONPath mechanism, environment-variable interpolation, and how the CLI decides which adapters to install now that a credential-free adapter exists alongside a credential-requiring one.

## Goals / Non-Goals

**Goals:**
- Any REST API testable from a case file alone, proven against a live public API.
- Mechanical, compiler-enforced update of every existing `IOperation` implementation to the new signature — no implementation silently left behind.
- The CLI runs cleanly with only the HTTP adapter configured (no Azure DevOps credentials required) and also with both adapters configured.

**Non-Goals:**
- A full JSONPath specification implementation — the subset Newtonsoft.Json's `SelectToken` supports is sufficient (same mechanism a prior suite already relied on).
- Templating beyond flat `${VAR_NAME}` substitution — no expressions, no default values, no nested interpolation syntax.
- Retrofitting parameters onto the Azure DevOps adapter's existing fixed operations — they stay fixed-shape; only the new HTTP adapter uses parameters for this change.

## Decisions

### D1: Parameter type and `IOperation` signature
`PipelineStep` gains `IReadOnlyDictionary<string, object?>? With`. `IOperation.ExecuteAsync` becomes:

```csharp
Task<OperationResult> ExecuteAsync(CaseExecutionContext context, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken);
```

Always passing a non-null (possibly empty) dictionary — `CaseExecutor` substitutes `step.With ?? empty` before calling, so no operation needs a null check. `object?` (not `string`) because YAML values can be nested maps/lists/numbers/bools, and the HTTP adapter's `headers` parameter, for instance, is naturally a nested map.

### D2: Existing operations updated mechanically
Every current `IOperation` implementation (`ToyHttp`, `ToyFile`, `AzureDevOps`) gets the new parameter added to its signature and ignores it — none of them need real parameters yet. The compiler enforces that nothing is missed; a full solution build after the interface change is the actual verification step, not a manual audit.

### D3: JSONPath via Newtonsoft.Json
`ReleaseTwin.Adapters.Http` takes a `Newtonsoft.Json` dependency and uses `JObject.Parse(responseBody).SelectToken(path)` for `http.assertJsonPath`. Alternative considered: a hand-rolled dotted-path evaluator — rejected because JSONPath edge cases (array indexing, wildcards) are exactly the kind of thing not worth re-implementing when a proven library is one dependency away, and a prior suite already validated this approach in production.

### D4: Environment-variable interpolation
`case-loading` applies a simple regex (`\$\{([A-Z0-9_]+)\}`) to every string value found in a step's `with:` block (recursively, for nested maps) at load time, replacing each match with `Environment.GetEnvironmentVariable`. A missing variable throws `CaseFileException` immediately — same "fail before any case executes" contract as the existing malformed-case-file requirement.

### D5: Conditional adapter installation in the CLI
`CliRunner` now checks Azure DevOps's 5 environment variables as a group:
- **All present** → install the Azure DevOps adapter.
- **None present** → skip it silently (HTTP-only run).
- **Some present, some missing** → startup error naming exactly which are missing (a half-configured adapter is a config mistake, not an intentional "skip it").

The HTTP adapter installs unconditionally — it takes no constructor arguments. This is the direct implementation of design.md D2 from phase3 ("revisit hardcoded single-adapter composition once a second real adapter exists") — that trigger condition is now met.

## Risks / Trade-offs

- **[Risk] `object?` parameters push type-checking into each operation, not the core.** → Mitigation: acceptable — the core's job is to carry data, not validate an individual operation's schema; each operation (here, only the HTTP adapter's two operations) is responsible for validating its own required parameters and failing clearly if one is missing or the wrong type.
- **[Risk] Demoing against a live public API means the example depends on a third party staying up.** → Mitigation: pick a stable, widely-used public test API (e.g. `https://jsonplaceholder.typicode.com`) and keep the unit-test suite's own HTTP adapter tests on a fake handler, same pattern as the Azure DevOps adapter — only the example/demo touches the real internet, not the test suite.
- **[Risk] Interpolation regex could silently under-match complex YAML structures.** → Mitigation: scenarios in specs/case-loading explicitly cover both the resolved and missing-variable cases; keep the regex intentionally simple rather than building a templating engine for a need that hasn't appeared yet.

## Open Questions

None — parameter shape, JSONPath mechanism, interpolation syntax, and adapter-installation logic were all decided above.
