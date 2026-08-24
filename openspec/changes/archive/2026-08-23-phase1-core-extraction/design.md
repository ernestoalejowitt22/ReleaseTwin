## Context

See proposal.md - Why. Two constraints shape this design beyond the functional requirements in specs/:

1. **Ownership.** quik-testing lives on ETI/Quik's Bitbucket and its latest history uses a `@quikforms.com` identity. To keep ReleaseTwin's IP story clean, this change writes new code informed by *reading* quik-testing's architecture (already summarized in quik-testing's `docs/commercialization-assessment.md`), never by copying its files, history, or literal implementation.
2. **Validate the abstraction before committing to it.** The commercialization assessment's own go/no-go criterion is that an adapter unrelated to Quik must ship without core changes. This design front-loads that test with toy adapters before any Quik-shaped work, rather than porting Quik's adapter first and hoping the boundary holds.

## Goals / Non-Goals

**Goals:**
- A C# core (`core-execution`, `adapter-sdk`, `flag-proof`) with zero adapter-specific references, satisfying the specs in this change.
- Empirical evidence that the adapter boundary holds across two structurally different toy adapters.
- A documented, read-only fit-check against quik-testing's real operations/preconditions/cleanup, recorded as a decision artifact, not as ported code.

**Non-Goals:**
- No literal Quik adapter implementation (conceptual fit-check only).
- No external-check connector (Playwright), CLI packaging/distribution, or hosted control plane — these are later phases per the assessment's validation path and are not designed here.
- No change to quik-testing itself.
- No TypeScript code in this phase; TS enters at the CLI/connector layer in a later change (see proposal.md - What Changes, "explicitly out of scope").

## Decisions

### D1: Fresh build in ReleaseTwin, not extract-in-place in quik-testing
The assessment's own Phase 1 describes refactoring quik-testing in place. We deviate: given the ownership constraint, ReleaseTwin is written fresh, and quik-testing is treated as read-only reference material throughout. Alternative considered: extract-in-place then lift the core out later — rejected because it entangles the new product's commit history with Quik-owned code and repository, and because quik-testing has 24 in-progress `openspec` changes where a core refactor would add risk to live ticket work.

### D2: Solution layout
```
ReleaseTwin/
  src/
    ReleaseTwin.Core/              # core-execution + flag-proof contracts and runtime
    ReleaseTwin.AdapterSdk/        # adapter-sdk composition-root contracts
    ReleaseTwin.Adapters.ToyHttp/  # toy adapter #1: generic REST + auth
    ReleaseTwin.Adapters.ToyFile/  # toy adapter #2: structurally different (file/CLI-shaped)
  tests/
    ReleaseTwin.Core.Tests/
    ReleaseTwin.AdapterSdk.Tests/
  docs/
    quik-fit-check.md              # conceptual fit-check notes (read-only reference, no ported code)
```
`ReleaseTwin.AdapterSdk` is a separate assembly from `ReleaseTwin.Core` so that "the core has zero adapter-specific references" (adapter-sdk spec, Requirement: Core has no adapter-specific references) is enforced at compile time for `ReleaseTwin.Core`, while `AdapterSdk` is allowed to know about the composition-root pattern itself.

### D3: Two toy adapters, deliberately unrelated to each other and to Quik
Adapter #1: a generic HTTP/REST adapter (auth, two operations, one precondition, one cleanup handler) — structurally closest to what a real second commercial adapter would look like. Adapter #2: a structurally different shape (e.g., file-system or process-invocation based, no HTTP auth) — chosen specifically to avoid both toy adapters accidentally sharing an implicit assumption (e.g., "every adapter has an HTTP client") that the real Quik adapter or a future customer adapter might violate. Both register purely through `adapter-sdk`; neither is referenced by `ReleaseTwin.Core`.

### D4: Quik fit-check is a document, not code
The fit-check (specs/core-execution, specs/adapter-sdk collectively) is performed by reading quik-testing's `QuikTestPipeline.cs`, `SuiteHost.cs`, `QuikTestValidator.cs`, and QFEM precondition/cleanup implementations, then writing `docs/quik-fit-check.md` in ReleaseTwin describing, requirement by requirement, whether each of Quik's real operations/preconditions/cleanup handlers could be expressed against the published core/adapter-sdk contracts without a core change. Any requirement that *can't* be satisfied is a signal to revise the spec before moving to Phase 2, not something to work around silently.

### D5: Failure classification as a closed enum, not adapter-extensible
`core-execution`'s failure classification (prerequisite / product / infrastructure / unstable) is fixed by the core, not extensible by adapters, so that reports are comparable across adapters. Adapters map their own error conditions onto these four classifications rather than inventing new top-level categories. Alternative considered: let adapters extend the classification — rejected because it would immediately reintroduce adapter-specific concepts into report consumers, undermining the vendor-neutral report contract.

## Risks / Trade-offs

- **[Risk] A conceptual fit-check (D4) is weaker evidence than actually porting Quik's adapter.** → Mitigation: treat any fit-check finding of "doesn't fit" as a required spec revision before Phase 2 begins; don't declare Phase 1 done on the strength of the toy adapters alone if the fit-check surfaces a real gap.
- **[Risk] Two toy adapters may still both happen to fit a common blind spot the real Quik adapter would expose (e.g., multi-step OAuth, envelope-style stateful resources).** → Mitigation: D3 deliberately makes the two toy adapters structurally different, and D4's fit-check specifically targets Quik's most stateful/complex mechanics (DocuSign envelope cleanup, QFEM preconditions) rather than its simplest ones.
- **[Risk] Building fresh (D1) means slower delivery than reusing proven quik-testing code.** → Mitigation: accepted trade-off given the ownership constraint; the assessment document itself (which is owned cleanly) already carries most of the design knowledge, reducing re-derivation cost.

## Open Questions

None — the choices above (language, repo location, adapter count/shape, classification model) were resolved during exploration rather than deferred.
