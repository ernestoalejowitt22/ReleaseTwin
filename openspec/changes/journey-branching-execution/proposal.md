## Why

The hosted journey builder (platform change `journey-builder-graph-and-choices`) now saves journey
versions whose `pipeline:` may contain `kind: "choice"` entries and step `when:` guards. Its
`journey-branching` spec was written as a contract for the engine to implement, and that change
explicitly defers the execution semantics to this repo. Today the engine cannot run such a version:
a choice entry fails to load ("a pipeline step is missing 'operation'"), and — worse — a step's
`when:` guard is silently dropped by the non-strict YAML deserializer, so a guarded step would run
unconditionally. `ReleaseTwin run --journey <id> --version <n>` goes through the same
`CaseFileLoader.ParseYaml` + `CaseExecutor` as a local case, so both paths need the same support.

## What Changes

- **Core model**: `TestCase.Pipeline` becomes `IReadOnlyList<IPipelineEntry>`, where an entry is a
  `PipelineStep` (gaining an optional `When` guard) or a new `ChoiceStep(When, Then, Else)` whose
  branches are typed `IReadOnlyList<PipelineStep>` — a nested choice is unrepresentable.
  New `Condition(Ref, Operator, Value)` with operators `==`, `!=`, `exists`, `!exists`, and a
  `ConditionEvaluator`. `PipelineEntries.FlattenSteps()` gives every step's structural position.
- **Executor**: the pipeline walk is recursive over entries. A choice runs only the taken branch;
  the untaken branch's steps, a guarded-off step, and everything after a halt record `NotExecuted`.
  Evidence indices are the flattened pre-order position (then-steps before else-steps), so every run
  of the same pipeline records the same step positions — no index collisions between branches.
- **Case loading**: `kind: choice` / `when` / `then` / `else` parse into the model; load-time
  rejection of nested choices, unknown kinds, unsupported operators, a missing `value` for `==`/`!=`,
  and capture references that are out of static scope (sibling branch, or branch-local after the join).
- **Specs**: new `journey-branching` capability (engine side); `cli-runner`'s pinned-hosted-journey
  requirement gains a scenario that a fetched version with choices/guards runs under these semantics.

## Capabilities

### New Capabilities
- `journey-branching`: choice entries, step guards, condition operators, `NotExecuted` recording for
  untaken branches and guarded-off steps, stable evidence positions, and load-time scope validation.

### Modified Capabilities
- `cli-runner`: "The CLI can run a pinned hosted journey" — a fetched version containing choices and
  guards executes with `journey-branching` semantics.

## Impact

- `src/ReleaseTwin.Core/Model.cs`, `ConditionEvaluator.cs` (new), `CaseExecutor.cs`
- `src/ReleaseTwin.Cli/CaseLoading/CaseFileDto.cs`, `CaseFileLoader.cs`, `PipelineEntryLoader.cs` (new),
  `CliRunner.cs` (capability inference walks flattened steps)
- **Source-breaking for SDK consumers that index `TestCase.Pipeline[i]` as a `PipelineStep`** —
  construction still compiles (covariance), reading needs `FlattenSteps()` or a type test. Three
  existing tests were updated for this. No adapter needed a change (core/adapter boundary held).
- No evidence JSON schema change; a linear case's evidence is byte-identical to before.
