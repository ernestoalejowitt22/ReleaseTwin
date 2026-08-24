## Why

The assessment's Phase 3 ("design partners") assumes something a partner can actually run — "run the extracted tool in two or three external CI environments." Nothing runnable exists yet: Phase 1 and Phase 2 deliberately deferred CLI/packaging, per docs/installation-model.md, in favor of proving the core/adapter-sdk seam first. That seam is now proven (a real, non-toy Azure DevOps adapter shipped without unplanned core changes). This change builds the minimal CLI needed to give a design partner something concrete, in parallel with the user pursuing design-partner conversations directly (business development, not part of this change).

## What Changes

- Build `ReleaseTwin.Cli`, a local CLI (not packaged for npm/NuGet/Docker/GitHub Action yet — those stay deferred per docs/installation-model.md) that:
  - Loads declarative case files (YAML) from a directory into `ReleaseTwin.Core.TestCase`.
  - Composes a `CompositionRoot` with the Azure DevOps adapter (the only real adapter that exists), configured from environment variables per the adapter-sdk external-credentials requirement.
  - Executes all loaded cases, prints a per-case report and an overall pass/fail summary, and exits non-zero on any failure (so it's usable as a CI gate the moment a design partner scripts it into their own pipeline — the "CI runner" installation type from docs/installation-model.md).
- Explicit scope limit, stated up front rather than discovered later: this slice's operations (`azdo.createWorkItem`, etc.) take no per-case parameters from the YAML — a case selects *which* named operations run, not what data they act on. A design partner's own workflow will very likely need parameterized operations; that is real, separate future work this change does not attempt to guess at.
- Explicitly out of scope: npm/NuGet/Docker/GitHub Action packaging, hosted control plane, any second adapter beyond Azure DevOps, operation parameterization.

## Capabilities

### New Capabilities
- `case-loading`: parsing a YAML case file into the core's `TestCase` model, including fixture content/hash resolution from disk.
- `cli-runner`: composing adapters, executing loaded cases, reporting results, and setting a CI-usable exit code.

### Modified Capabilities
(none)

## Impact

- New project `ReleaseTwin.Cli` under `src/`, with a corresponding test project under `tests/`.
- No changes to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, or any existing adapter.
- Introduces a YAML case-file format for the first time — a real, user-facing surface, even though it's only consumed locally for now.
