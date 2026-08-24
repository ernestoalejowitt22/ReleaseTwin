## Context

See proposal.md - Why. This design covers how the Azure DevOps adapter maps onto the six things the assessment's Phase 2 criterion requires (auth, multi-op, precondition, cleanup, resource coordination, feature-state proof), and how it stays compatible with docs/installation-model.md without building any installation mechanism yet.

## Goals / Non-Goals

**Goals:**
- A real `ReleaseTwin.Adapters.AzureDevOps` adapter, built only against `adapter-sdk`, that exercises Azure DevOps's actual REST API.
- Zero *unplanned* changes to `ReleaseTwin.Core` or `ReleaseTwin.AdapterSdk` — see D5 for the one change this design deliberately keeps open as a tracked, foreseen possibility rather than an incidental one.
- Adapter credentials externally supplied, satisfying the new adapter-sdk requirement.

**Non-Goals:**
- A meaningful business flag-proof scenario. See D3: this validates plumbing (does a real known-bad/known-good toggle round-trip through the core), not a realistic customer use case. Selling that realism is future work, not this change's job.
- Any CLI, packaging, or hosted execution — deferred per docs/installation-model.md.
- Resolving Gap 1 (three-state prerequisites) unconditionally — see D5.

## Decisions

### D1: Azure DevOps Work Items API as the operation surface
Use the Work Items REST API (`_apis/wit/workitems`) for the adapter's operations, since it's stateful, supports create/read/update, and doesn't require standing up a real pipeline run (expensive and slow to validate against repeatedly):
- `azdo.createWorkItem` — POST with a JSON Patch body, PAT auth.
- `azdo.getWorkItem` — GET by ID.
- `azdo.transitionWorkItemState` — PATCH `System.State`.

Alternative considered: Pipelines/Runs API (closer to "real" CI usage) — rejected for this change because triggering and polling real pipeline runs is slow and adds flakiness risk to what's supposed to be a clean go/no-go signal; work items give the same auth/CRUD/state shape without that cost.

### D2: Credential resolution
The adapter's constructor takes a PAT as a plain string parameter (matching the new adapter-sdk requirement); the *caller* (test harness, later a CLI) is responsible for resolving that value from an environment variable or secret store. The adapter itself never reads environment variables directly, keeping it agnostic to where it's eventually invoked from (local test, future CLI, future CI action) — consistent with docs/installation-model.md's "no adapter assumes a specific host process."

### D3: Feature-state proof via a variable group toggle
`IFeatureStateController.SetStateAsync` is implemented by writing a value to an Azure DevOps variable group's variable via the Distributed Task API. The "product" operation under test reads that same variable group's value back (through a work item field set by a preceding operation, or directly) as its assertion target.

This is an honest limitation, stated plainly: the known-bad/known-good toggle and the thing being asserted on are both Azure DevOps API calls we control end-to-end, not an independent product behavior reacting to a real flag the way a real customer's application would. That's acceptable for this change's actual goal — proving `FlagProofRunner` round-trips correctly through a real external API's auth and state model — but should not be cited later as evidence of a realistic flag-proof customer scenario. A later phase, once there's a design partner, should replace this with a real application reacting to a real flag.

### D4: Resource coordination target
Use a shared area path as the `ResourceKey` — concurrent cases creating work items under the same area path serialize through `CaseExecutor`'s resource-key mechanism, mirroring a real contention point (teams sharing an area path in production Azure DevOps usage).

### D5: Gap 1 (three-state prerequisites) — conditional, not scheduled

**Resolved: escalated.** `azdo.areaPathExists` was implemented against the existing boolean `PrerequisiteResult` first, per the original plan below. A test simulating a real HTTP failure path (401 during the area-path check, vs. a confirmed 404) confirmed the misclassification directly: both collapsed to `FailureClassification.Prerequisite`, which is exactly the "couldn't check" vs. "confirmed absent" conflation Gap 1 named. The user chose to fix it immediately rather than wait for a live integration test.

`PrerequisiteResult` in `ReleaseTwin.Core` is now `(PrerequisiteStatus Status, string? Detail)` with `Status ∈ {Satisfied, NotSatisfied, Inconclusive}`. `Inconclusive` maps to `FailureClassification.Infrastructure` in `CaseExecutor` — reusing the existing closed four-value enum (preserving D5's original closed-set decision) rather than adding a fifth classification value. `NotSatisfied` still maps to `FailureClassification.Prerequisite`, unchanged. All three `IPrerequisiteCheck` implementations in the codebase (Phase 1's `ToyHttp`/`ToyFile` adapters, this change's Azure DevOps adapter) were updated; the toy adapters only ever report `Satisfied`/`NotSatisfied` since they're fully in-memory and have no real "couldn't check" case.

Original plan (superseded by the above): implement against boolean `PrerequisiteResult` first; only escalate if implementation hits a real misclassification, treating that as a **deliberate, tracked** core change rather than evidence the go/no-go test failed — the criterion is about *unplanned* core changes forced by ordinary adapter behavior, not about every core evolution being permanently forbidden.

## Risks / Trade-offs

- **[Risk] Real API dependency makes tests slower/flakier than Phase 1's fully in-memory toy adapters.** → Mitigation: keep operation scope minimal (3 operations, 1 precondition, 1 cleanup); consider recording/replaying HTTP responses for the unit-test suite while keeping a separate, explicitly-tagged integration-test path that hits the real API.
- **[Risk] D3's flag-proof scenario could be mistaken for more meaningful than it is.** → Mitigation: stated explicitly above as a non-goal; call it out again in the adapter's own code-level documentation and in the change's final summary.
- **[Risk] Escalating to D5's core change mid-implementation could balloon scope.** → Mitigation: the decision above scopes it to "only if forced," with an explicit recorded outcome either way, so it can't silently expand without being visible in tasks.md.

## Open Questions

- Which Azure DevOps organization/project to use for real integration testing (a free/sandbox org is sufficient) — operational detail, doesn't change the spec, approach, or task breakdown; resolve when standing up the integration-test environment.
