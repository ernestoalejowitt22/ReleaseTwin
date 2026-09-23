## Context

The platform's `journey-builder-graph-and-choices` change defines the authoring schema
(`web/src/app/journeys/pipeline.ts`): a pipeline entry is a step `{operation, with, capture, when?}`
or a choice `{kind: "choice", when: {ref, op, value}, then: Step[], else: Step[]}`; operators are
`==`, `!=`, `exists`, `!exists` (value omitted for the unary ones). Its tasks 2.1–2.4 and its
evidence-store audit hand the execution contract and the "branch step index collision" question to
this repo. The CLI runs hosted journeys via `CliRunner.RunJourneyAsync` → `JourneyFetchClient` →
`CaseFileLoader.ParseYaml` → `CaseExecutor`, i.e. the same path as a local case file.

## Goals / Non-Goals

**Goals:** execute choices and guards per the platform's `journey-branching` contract; keep a linear
case's behavior and evidence unchanged; keep evidence positions stable across runs.

**Non-Goals (Phase 1, deferred):** recording which branch was taken / the evaluated condition in
evidence or the report; a per-branch evidence "leg"; richer operators (`>`, `<`, `contains`);
`expect_failure`/`retry` in YAML (not exposed today either); flag-proof-specific handling (flag-proof
legs just run the same executor); hosted evidence-runner parity (it has no pipeline-execution source).

## Decisions

### D1: `IPipelineEntry` marker interface on the existing `TestCase.Pipeline`
`Pipeline` changes type to `IReadOnlyList<IPipelineEntry>`; `PipelineStep` and `ChoiceStep`
implement it. Existing construction sites compile unchanged via `IReadOnlyList<out T>` covariance.
`ChoiceStep.Then/Else` are `IReadOnlyList<PipelineStep>`, so nesting is a type error.
**Alternative rejected:** keep `Pipeline` as a flat step list and add a parallel `Choices` side
table keyed by index — avoids the source break but splits one ordered structure across two
collections, and every consumer would have to reassemble it.

### D2: Evidence index = flattened pre-order position
`FlattenSteps()` orders a top-level step, then a choice's `then` steps, then its `else` steps. The
executor pre-allocates one evidence slot per flattened step; any slot not recorded when the walk ends
becomes `NotExecuted`. This one mechanism covers untaken branches, guarded-off steps, halts, and
pre-pipeline aborts (fixture/prerequisite/capability), and makes indices globally unique — resolving
the platform audit's `then` step 0 / `else` step 0 collision without an evidence schema change.

### D3: An absent capture in a condition is "absent", not an error
`==` → false, `!=` → true, `exists` → false, `!exists` → true. A condition's ref is in static scope
but may still be absent at run time (its producer was guarded off or failed to capture); a
condition is exactly the construct used to test for that, so failing the case would be hostile.
`{{name}}` parameter references keep their existing run-time `missing-capture` failure.

### D4: Static scope validation at load time, compatible with existing linear cases
A condition ref must be captured earlier in the same branch or before the enclosing choice. A
`{{name}}` parameter reference is rejected only when `name` is captured somewhere in the case but
outside the reference's scope; a reference to a name captured nowhere keeps today's run-time
behavior, so no previously-loadable linear case file starts failing to load.

## Risks / Trade-offs

- **[Risk] Older CLI versions silently ignore `when:`** (non-strict deserializer) and fail on a
  choice entry. A pinned journey version saved with guards and run by a pre-change CLI would run
  guarded steps unconditionally. → Not fixable retroactively in old binaries; flagged for the
  platform side (e.g. surface a minimum CLI version on journeys that use guards/choices).
- **[Trade-off] Source break for SDK consumers indexing `Pipeline`** → accepted per D1; documented.
- **[Trade-off] No "branch taken" marker in evidence** → a reader infers it from which slots are
  `NotExecuted`; an explicit marker is a deferred, additive follow-up.
