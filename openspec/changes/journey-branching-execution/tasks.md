## 1. Core model

- [x] 1.1 Add `IPipelineEntry`, `Condition`, `ConditionOperator`, `ChoiceStep` (branches typed as
      `IReadOnlyList<PipelineStep>`); add `When` to `PipelineStep`; retype `TestCase.Pipeline`.
- [x] 1.2 Add `PipelineEntries.FlattenSteps()` (pre-order, then before else).
- [x] 1.3 Add `ConditionEvaluator` with the absent-capture rule (design D3).

## 2. Executor

- [x] 2.1 Replace the linear loop in `CaseExecutor.RunPipelineAsync` with a recursive entry walk;
      untaken branch / guarded-off / post-halt slots finish as `NotExecuted` (design D2).
- [x] 2.2 `ValidateReferences` and pre-pipeline aborts (`AllNotExecuted`) cover branch steps.

## 3. Case loading / CLI

- [x] 3.1 Extend `PipelineStepDto` with `kind`, `when`, `then`, `else`; add `ConditionDto`.
- [x] 3.2 New `PipelineEntryLoader`: choice/step parsing, no-nesting, unknown kind, operator set,
      missing value, `then`/`else` on a plain step, static scope validation (design D4).
- [x] 3.3 `CliRunner` capability inference walks `FlattenSteps()`.

## 4. Specs

- [x] 4.1 New `journey-branching` spec; MODIFIED `cli-runner` pinned-hosted-journey requirement.

## 5. Tests and verification

- [x] 5.1 Core: `JourneyBranchingTests` (both branches, stable positions, halt inside branch, guard
      on/off, empty branch, unknown op in branch, fixture mismatch, evaluator truth table).
- [x] 5.2 CLI: `CaseFileLoaderBranchingTests` (model mapping, linear unchanged, every rejection,
      every scope scenario); `CliRunnerJourneyTests` hosted journey with a choice + guard, and a
      hosted journey with a nested choice as a parse error.
- [x] 5.3 Update three existing loader tests that indexed `Pipeline[0]` as a step.
- [x] 5.4 `dotnet build ReleaseTwin.sln`, `dotnet test ReleaseTwin.sln`,
      `openspec validate journey-branching-execution --strict`.

## 6. Deferred (not Phase 1)

- [ ] 6.1 Record the taken branch / evaluated condition in evidence and the report.
- [ ] 6.2 Additional operators (`>`, `<`, `contains`) — only with a matching platform change.
- [ ] 6.3 Platform-side guard for older CLIs that silently ignore `when:` (e.g. a minimum CLI
      version on journeys using guards/choices) — cross-repo, platform decision.
- [ ] 6.4 Live run of a real pinned hosted journey with a choice against the hosted API
      (**Needs the user to run this** — requires a real project API token and a saved version).
