## Why

The Quik-Testing suite (`quik-testing`) has real commercial value as a "release-proof testing" product — evidence-linked cases, immutable fixtures, prerequisite ownership, deterministic JSONPath assertions, cleanup/resource coordination, failure classification, and paired known-bad/known-good flag proof — but that value is currently fused into a Quik-specific schema, validator, and pipeline context (stages `produce/convert/workflow/live`, operations namespaced to `qfe`/`qfem`/`esign`/`docusign`). Per the commercialization assessment (quik-testing `docs/commercialization-assessment.md`), the path to a sellable product runs through extracting a vendor-neutral core with an adapter boundary, proven by an adapter unrelated to Quik.

ReleaseTwin is a new, independent repository for that extraction. It is written fresh — informed by reading quik-testing as a reference, not by copying its code or history — to keep the product's IP clearly separate from Quik/ETI-owned work. This change defines Phase 1: build the vendor-neutral core and adapter seam and prove it against a Quik-shaped adapter conceptually, without touching the quik-testing repository.

## What Changes

- Introduce a vendor-neutral execution core: case/assertion model, ordered pipeline execution, operation/prerequisite/cleanup contracts, payload store with SHA-256 integrity verification, retry/timeout handling, resource-key serialization, and failure classification. The core must not reference QFE, QFEM, eSign, DocuSign, LaunchDarkly, Jira, or any Quik-specific identifier.
- Introduce an adapter SDK: a composition-root pattern where core services register first and adapters contribute named operations, authentication/clients, prerequisite checks, cleanup handlers, and capability declarations without editing core types.
- Introduce known-bad/known-good flag proof as a first-class core capability (the most differentiated mechanic per the assessment), reporting a paired result as one "release proof."
- Build two deliberately unrelated toy adapters (not Quik-shaped) to stress the adapter seam from two different angles before any Quik-shaped work begins.
- Perform a conceptual fit-check against quik-testing's real operations, QFEM preconditions, and DocuSign cleanup (by reading, not porting, quik-testing source) to validate that a Quik-shaped adapter could be built on these contracts without core changes.
- Explicitly out of scope for this change: external-check connector SDK (Playwright/TS), CLI packaging and distribution (npm/NuGet/Docker/GitHub Action), hosted control plane, billing, and any literal Quik adapter implementation. These are later phases per the assessment's validation path.

## Capabilities

### New Capabilities
- `core-execution`: case and assertion model, ordered pipeline execution lifecycle, operation/prerequisite/cleanup contracts, payload integrity verification, retry/timeout handling, resource-key serialization, failure classification, and the report contract.
- `adapter-sdk`: the composition-root contract for registering core services and installing one or more adapters (named operations, clients, prerequisite checks, cleanup handlers, capability declarations) without modifying core types.
- `flag-proof`: paired known-bad/known-good execution against the same immutable inputs, reported as a single release-proof result with weak-oracle detection (both states passing when they should differ).

### Modified Capabilities
(none — greenfield repository)

## Impact

- New C# solution/projects in ReleaseTwin implementing `core-execution`, `adapter-sdk`, and `flag-proof`.
- Two new toy adapter projects (illustrative, not customer-facing) used only to validate the adapter seam.
- No impact to quik-testing: it is read-only reference material for this change, never modified or depended upon.
- No impact to distribution, packaging, or hosted infrastructure — none exists yet and none is introduced by this change.
