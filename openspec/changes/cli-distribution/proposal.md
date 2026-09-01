## Why

Two of the three CLI install paths named in the funnel plan are done: the
Docker image (`cli-packaging`) and the PR-annotations GitHub Action
(`ci-pr-integration`). Two gaps remain, and both are friction on the first rung
of the self-serve funnel:

1. **No `dotnet tool` install.** Someone who already has .NET and wants the CLI
   on their laptop or a non-Docker runner still has to clone this repo and
   `dotnet run`. `docs/installation-model.md` says this is "still deferred."
   `dotnet tool install -g releasetwin` is the standard answer and does not
   depend on the repo being public (nuget.org is a separate artifact).

2. **The GitHub Action isn't consumable by a stable ref.** `action.yml` and its
   README document `uses: …/integrations/github-action@v1`, but no `v1` tag
   exists or moves on release — a consumer pinning `@v1` gets nothing. The
   Action's default `image` input also points at a GHCR package that inherits
   this repo's (private) visibility, so it is unusable externally until the
   image is made public.

Several docs (`installation-model.md`, `docs/ci.md` with literal `OWNER` /
`VERSION` placeholders, `docs/ideas/deferred-backlog.md`, README "What's not
built yet") disagree about what exists. This change closes the two gaps and
makes the docs consistent.

## What Changes

- **`dotnet tool` package.** `ReleaseTwin.Cli.csproj` gains `PackAsTool` +
  `ToolCommandName=releasetwin` + package metadata. `release.yml` runs
  `dotnet pack` and `dotnet nuget push` to nuget.org on a version tag, after
  the existing build+test gate. New `docs/` section: `dotnet tool install -g
  releasetwin`, the `${ENV_VAR}` / exit-code contract is identical to Docker,
  and a note that UI-journey cases need `playwright install` (HTTP and
  flag-proof work without it — same graceful degradation as the container).

- **Stable major-version ref for the Action.** A release step (in `release.yml`
  or a small dedicated workflow) force-updates a floating `v<major>` tag (and
  `v<major>.<minor>`) to each `v*.*.*` release, so `uses: …@v1` resolves and
  tracks patches. The Action's README/quickstart show a pinned `@v1.2.3` as the
  recommended form and `@v1` as the convenience form.

- **Doc reconciliation.** `docs/installation-model.md`, `docs/ci.md` (real
  owner/repo, a real version), `docs/ideas/deferred-backlog.md`, and the README
  "What's not built yet" bullet updated to state the three real install paths
  (Docker, `dotnet tool`, the Action) and what genuinely remains (Homebrew).

- **`docs/ci.md` + the Action README** get a "run-only" recipe: the existing
  Action with `comment: false` is already a pure CI gate (the check run blocks
  the merge) — documented rather than building a second Action.

## Capabilities

### Modified Capabilities

- `cli-packaging`: adds a requirement that the CLI is also published as a
  .NET global tool on a public NuGet feed, installable with `dotnet tool
  install -g` and preserving the same case/fixture, env-var, and exit-code
  contract as the container. The existing container requirements are unchanged.

- `ci-pr-integration`: adds a requirement that the Action is consumable by a
  stable `v<major>` reference that is updated to point at each verified
  release, so a workflow pinning `@v1` resolves to the latest compatible
  version.

## Impact

- **`src/ReleaseTwin.Cli/ReleaseTwin.Cli.csproj`:** `PackAsTool`,
  `ToolCommandName`, `PackageId`, `PackageLicenseExpression` (matches the
  engine's `AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception`),
  description, repository URL. No code change.
- **`.github/workflows/release.yml`:** `dotnet pack` + `dotnet nuget push`
  steps; a floating-tag update step for the Action. New repo secret
  `NUGET_API_KEY` (manual — see below).
- **`integrations/github-action/README.md`, `action.yml` comments:** pinning
  guidance; note the `image` input must be a public registry ref.
- **`docs/`:** `installation-model.md`, `ci.md`, `ideas/deferred-backlog.md`;
  a new install section (in `quickstart.md` or a short `docs/install.md`).
- **`README.md`:** the packaging bullet under "What's not built yet".
- **`REUSE.toml` / SPDX:** any new file gets the standard header; the tool
  nupkg carries the engine license.
- **no** change to the engine, adapters, hosted platform, or the CLI's runtime
  behavior.

## Manual steps (no code alternative)

1. Create a **nuget.org** account; generate a scoped **API key** (push, glob
   `ReleaseTwin.*` or `releasetwin`); add it as the `NUGET_API_KEY` repo
   secret. First push may need the package id pre-reserved or pushed once
   interactively to claim the id.
2. After `go-public-sequence` makes the repo public: set the GHCR `cli`
   package visibility to public, and publish the Action to the GitHub
   Marketplace from a Release (checkbox in the Release UI). Neither blocks the
   `dotnet tool` path.

## Explicitly deferred

- **Homebrew** tap / formula. `dotnet tool` covers cross-platform CLI install
  for anyone with .NET; Docker covers the no-SDK case. Revisit if asked.
- A **self-contained single-file binary** (no .NET runtime needed) per-RID.
  Larger build matrix; the two paths above cover the demand.
- Publishing the Action as its **own repository** (vs the `integrations/`
  subdirectory ref). The subdir ref works; a split is only worth it for
  Marketplace vanity.
