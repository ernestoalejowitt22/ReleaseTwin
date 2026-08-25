## Why

A customer cannot integrate ReleaseTwin into their own CI pipeline today without cloning this repo's source and building it with the .NET 8 SDK — no customer will vendor our source tree into their pipeline. Case authoring, credential handling, and the CI pass/fail gate (exit code) all already work end-to-end (`cli-runner`); packaging/distribution is the one remaining blocker between "the CLI works" and "a customer can actually install and run it."

## What Changes

- Publish a Docker image for the CLI (multi-stage .NET build → runtime image), runnable identically on any CI system without requiring the .NET SDK on the runner — the CI-agnostic default, and the only distribution path this change ships.
- Add a tag-triggered release workflow (build, verify, publish to GHCR) alongside the existing `ci.yml`/`hosted-ci.yml` build-and-test workflows, which already gate every push/PR but don't publish anything.
- Document the Docker install path and its invocation shape (volume-mounting the sibling `cases/`/`fixtures/` directories, passing `${ENV_VAR}`-style credentials into the container).
- (Deferred to a follow-up change: a `dotnet tool`/NuGet publish path for .NET-savvy environments that already have the SDK.)

## Capabilities

### New Capabilities
- `cli-packaging`: distribution of the CLI as an installable, versioned Docker image instead of source-only, including the release process that produces it. (A future `dotnet tool` path is a modification to this same capability, not a separate one.)

### Modified Capabilities

(none — `cli-runner`'s execution behavior is unchanged; this only changes how the same CLI is obtained)

## Impact

- New: a `Dockerfile` (multi-stage .NET build) for `src/ReleaseTwin.Cli`.
- New: `.github/workflows/release.yml`, tag-triggered, alongside the existing `ci.yml`/`hosted-ci.yml` build-and-test workflows.
- New: a GitHub Container Registry (GHCR) package to publish to. (A `dotnet tool`/NuGet publish path is deferred to a small follow-up change once the Docker path is proven out.)
- Docs: `README.md` and `docs/installation-model.md`'s "Packaging/distribution" deferred item gets resolved; install instructions added.
- No changes to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, or any adapter — packaging is purely how the existing, unmodified CLI binary reaches a customer.
