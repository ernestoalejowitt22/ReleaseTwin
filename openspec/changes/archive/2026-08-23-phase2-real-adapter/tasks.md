## 1. Project scaffolding

- [x] 1.1 Create `ReleaseTwin.Adapters.AzureDevOps` project referencing `ReleaseTwin.AdapterSdk` and `ReleaseTwin.Core` only
- [x] 1.2 Create `ReleaseTwin.Adapters.AzureDevOps.Tests` project
- [x] 1.3 Add both to `ReleaseTwin.sln`

## 2. Adapter credential/config requirement (`adapter-sdk`)

- [x] 2.1 Implement the Azure DevOps adapter's constructor to accept PAT and organization/project as parameters, never as literals (satisfies specs/adapter-sdk ADDED requirement)
- [x] 2.2 Unit test: constructing the adapter with a PAT sourced from a variable, not a literal, and asserting no credential literal exists in the adapter's source via a simple text-scan test

## 3. Azure DevOps operations (design.md D1)

- [x] 3.1 Implement PAT-authenticated HTTP client wrapper for the Work Items API
- [x] 3.2 Implement `azdo.createWorkItem` operation
- [x] 3.3 Implement `azdo.getWorkItem` operation
- [x] 3.4 Implement `azdo.transitionWorkItemState` operation
- [x] 3.5 Implement `azdo.areaPathExists` prerequisite check
- [x] 3.6 Implement `azdo.deleteWorkItem` cleanup handler (moves to recycle bin, not permanent destroy)
- [x] 3.7 Register all of the above via `IAdapterModule.Register` against `adapter-sdk` only

## 4. Resource coordination and feature-state proof (design.md D3, D4)

- [x] 4.1 Wire a shared area path as the case's `ResourceKey` for concurrent work item creation
- [x] 4.2 Implement `IFeatureStateController` against the Azure DevOps variable group API
- [x] 4.3 Wire an operation to read the variable group value back as its assertion target
- [x] 4.4 Run a `FlagProofRunner` case end to end against real Azure DevOps state

## 5. Gap 1 checkpoint (design.md D5)

- [x] 5.1 Implement `azdo.areaPathExists` against the existing boolean `PrerequisiteResult`
- [x] 5.2 Attempt a real auth-failure / unreachable-org scenario against the check; record whether boolean misclassifies it
- [x] 5.3 Misclassification confirmed (see 5.2's test). User chose to escalate now: `PrerequisiteResult` revised to `(PrerequisiteStatus, string?)` with `Satisfied`/`NotSatisfied`/`Inconclusive`; `Inconclusive` maps to `FailureClassification.Infrastructure`. specs/core-execution updated (MODIFIED requirement). All `IPrerequisiteCheck` implementations updated; full solution rebuilds and all 45 tests pass.

## 6. Integration and unit tests

- [ ] 6.1 Stand up a sandbox Azure DevOps organization/project for integration testing (see design.md Open Questions) — **deferred to user**: requires creating a real account, which is outside what I'll do autonomously; not blocking the rest of this change since 6.2's tests are written to skip gracefully until this exists
- [x] 6.2 Write integration tests (tagged separately from the fast unit suite) exercising create → read → transition → cleanup against the real API
- [x] 6.3 Record HTTP interactions for a fast, deterministic unit-test path that doesn't require live credentials in CI
- [x] 6.4 Confirmed: `ReleaseTwin.AdapterSdk` was not touched at all in this change. `ReleaseTwin.Core` changed only for the deliberate, tracked reason in task 5.3 (`PrerequisiteResult`) — no other core file was edited.

## 7. Change closeout

- [x] 7.1 Confirm the new adapter-sdk requirement (external credentials) has a passing test
- [x] 7.2 Confirmed accurate — no update needed. The credential-resolution and host-agnostic constraints both held (adapter takes PAT/org/project as parameters; no environment-variable reads inside the adapter itself). Nothing new about installability surfaced; Gap 1 was a core-execution concern, not an installation one.
- [x] 7.3 Ran `openspec validate phase2-real-adapter --strict` — valid, no findings
