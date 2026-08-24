## Why

Phase 1 (`phase1-core-extraction`) proved the core/adapter-sdk seam against two adapters we invented ourselves — a weak test, since we could unconsciously shape them to fit. The commercialization assessment's actual go/no-go criterion for the abstraction requires an adapter built against something that was **not** designed with this core in mind: "if the adapter can be added without modifying the core model or runner, the abstraction has demonstrated commercial leverage."

This change builds that real adapter — against Azure DevOps — and, per [docs/installation-model.md](../../../docs/installation-model.md), keeps it compatible with how a customer would eventually need to install and configure it, even though no CLI or distribution mechanism is built yet. That awareness applies to every phase going forward, not just this one; this change makes it concrete for the first time as a testable requirement.

## What Changes

- Build a real Azure DevOps adapter (`ReleaseTwin.Adapters.AzureDevOps`) implementing `IAdapterModule` against the published `adapter-sdk` contracts only — no changes to `ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` permitted for ordinary behavior.
- The adapter SHALL cover, using Azure DevOps's real REST API: PAT-based authentication, multiple operations (e.g. create work item, read work item, transition work item state), one real prerequisite check (e.g. a project/area path exists), one cleanup handler (e.g. delete/close the created work item), and resource coordination (serializing operations against a shared work item or pipeline resource).
- The adapter SHALL demonstrate feature-state proof against a real Azure DevOps construct — a pipeline environment or variable-group toggle standing in for known-bad/known-good — exercised through `FlagProofRunner` from Phase 1.
- Add a new `adapter-sdk` requirement, informed by docs/installation-model.md: adapter credentials/configuration SHALL be supplied externally (environment variable or config object) at construction time, never hardcoded in adapter source. This is the one installability constraint that bites at the code level before any CLI exists.
- Explicitly out of scope: any CLI, Docker image, GitHub Action, npm package, or hosted control plane. Those remain deferred until this change's adapter proves the seam holds.
- Gap 1 from Phase 1's fit-check (docs/quik-fit-check.md: prerequisite results are boolean but Quik's real ones are three-state) is resolved in this change: implementation confirmed the misclassification with a real HTTP failure path, so `PrerequisiteResult` in `ReleaseTwin.Core` is escalated to a three-state (`Satisfied` / `NotSatisfied` / `Inconclusive`) shape — a deliberate, tracked core change per design.md D5, not an unplanned one forced by ordinary adapter behavior.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `adapter-sdk`: add a requirement that adapter credentials/configuration must be externally supplied, not hardcoded — see What Changes.
- `core-execution`: `PrerequisiteResult` becomes three-state (`Satisfied` / `NotSatisfied` / `Inconclusive`), with `Inconclusive` classified as `Infrastructure` rather than `Prerequisite` — resolves Gap 1.

## Impact

- New project `ReleaseTwin.Adapters.AzureDevOps` under `src/`, with a corresponding test project under `tests/`.
- Requires an Azure DevOps organization/project and a PAT for real integration testing; secrets supplied via environment variables, never committed. Integration tests are scaffolded but skip gracefully until that org exists (deferred to the user; not blocking this change).
- `ReleaseTwin.Core`: `PrerequisiteResult`'s shape changes from a boolean to a three-state enum (breaking change to that type, deliberately). All existing `IPrerequisiteCheck` implementations (both Phase 1 toy adapters and this change's Azure DevOps adapter) updated accordingly.
- No impact to quik-testing.
