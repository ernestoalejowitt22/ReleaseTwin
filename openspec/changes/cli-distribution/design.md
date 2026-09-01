## Context

See `proposal.md` — Why. Current state: `Dockerfile` + `release.yml` publish
`ghcr.io/<repo>/cli` on a `v*.*.*` tag after the full test suite; only `v0.1.0`
is tagged and no GitHub Release exists. `integrations/github-action/` is a
composite action (Docker-based CLI run + `render.mjs` PR comment/check) from
`ci-pr-integration`, Apache-2.0, referenced as `…@v1` — but no `v1` tag moves.
`src/ReleaseTwin.Cli` is a plain `Exe` (net8.0) with six adapter project
references, `YamlDotNet`, `Newtonsoft.Json`, `SixLabors.ImageSharp` (carries
open NU19xx advisories — warnings, not errors).

## Goals / Non-Goals

**Goals:**
- `dotnet tool install -g releasetwin` works for anyone with the .NET SDK, on
  the next tagged release, without the repo being public.
- `uses: …@v1` resolves and tracks patch releases.
- One consistent story across the install docs.

**Non-Goals:**
- Any change to how the CLI resolves cases, fixtures, env vars, or exit codes —
  the spec requires parity with the container, achieved by shipping the *same
  build*, not re-implementing anything.
- A second GitHub Action. The existing one with `comment: false` is already a
  pure gate.
- Self-contained per-RID binaries, Homebrew — deferred in the proposal.

## Decisions

### nuget.org, not GitHub Packages
A public `dotnet tool` must be installable with a bare
`dotnet tool install -g releasetwin` — no feed config, no auth. GitHub Packages'
NuGet feed requires a PAT even for public packages and a `nuget.config` entry.
nuget.org is the only feed that gives the frictionless install the funnel needs,
and its packages are public immediately regardless of this repo's visibility.
**Alternative rejected:** GitHub Packages — keeps everything in one place but
breaks the zero-config install that is the entire point.

### Package id `releasetwin`, command `releasetwin`
The tool command is what users type; it must be `releasetwin`. The nuget.org
package id is best kept identical for discoverability
(`dotnet tool install -g releasetwin`). `ReleaseTwin.Cli` would also work as the
id but reads as an internal assembly name. The id must be claimed on first push
(the user does this once — manual step 1).

### Package license expression is plain `AGPL-3.0-only`
The proposal said the nupkg would carry
`AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception`. NuGet's
`PackageLicenseExpression` only accepts SPDX-listed ids and exceptions — a
custom `LicenseRef-*` is rejected at pack time. The Adapter Linking Exception
exists for third parties who *link the AdapterSdk* to write an adapter; the CLI
executable itself is plain AGPL-3.0, so `PackageLicenseExpression=AGPL-3.0-only`
is both valid and correct. The tool README links `LICENSE.EXCEPTIONS` for
completeness. (`reuse` still sees the full expression via the file's SPDX
header and `REUSE.toml`.)

### `dotnet pack` of the existing project, framework-dependent tool
`PackAsTool=true` produces a framework-dependent tool (needs a .NET runtime on
the host). That is the correct trade: someone running `dotnet tool install`
already has .NET; the no-runtime case is what the Docker image is for. The
adapter project references and NuGet dependencies are bundled into the nupkg by
`dotnet pack` automatically — no restructuring.
**ImageSharp advisories:** `dotnet pack` does not fail on NU19xx by default;
leave the warning visible (it is already visible in every build) and let the
separate dependency-hygiene track address ImageSharp. Do not add
`NoWarn`/`TreatWarningsAsErrors` here.
**Playwright:** the UI adapter is compiled in; the tool cannot run browser
journeys until the user runs `playwright install`. Documented, same as the
container's graceful degradation — HTTP and flag-proof cases need nothing extra.

### Floating tags updated by the release workflow, force-pushed
On a successful `v*.*.*` release, `release.yml` computes `v<major>` and
`v<major>.<minor>` from the tag and force-updates them
(`git tag -f` + `git push -f origin <tag>`). This is the standard GitHub Action
distribution pattern (`actions/checkout` etc. do exactly this). The force-push
is scoped to these derived tags only and gated behind the build+test job, so it
can never point `v1` at an unverified commit. Needs `contents: write` on that
job (currently `contents: read` — a narrow bump, tags only).
**Alternative rejected:** a separate "tag-mover" workflow triggered by Release
publish — more moving parts; the release job already has the verified commit
checked out.

### Docs consolidation into `docs/install.md`
`quickstart.md` stays Docker-first (its "no account, no clone, no .NET" framing
is the strongest first-touch). A new `docs/install.md` covers all three paths
side by side and is linked from `quickstart.md`, `ci.md`, README, and
`installation-model.md`. `docs/ci.md`'s literal `OWNER`/`VERSION` placeholders
become the real repo and a real pinned version.

## Risks / Trade-offs

- **First nuget.org push can fail on an unclaimed id** → manual step 1 says to
  push once interactively (or reserve the id) before relying on CI. The CI push
  step tolerates "already exists" for idempotency on re-runs
  (`--skip-duplicate`).
- **Force-pushing `v1` surprises someone who diffed tags** → this is the
  documented, expected behavior for Action major tags; called out in
  `CONTRIBUTING`/release notes.
- **`contents: write` on the release job** → scoped to the job, used only for
  the derived tag push; the checkout is already the verified release commit.
- **Tool users on an older .NET runtime** → `dotnet tool` reports a clear
  runtime-version error; the docs state the required .NET major version.
- **ImageSharp advisory ships in the nupkg** → same exposure as the Docker
  image and source today; not a regression, and tracked separately.

## Migration Plan

1. Land the `.csproj` metadata + `release.yml` steps + docs (this change).
2. User does manual step 1 (nuget.org account + `NUGET_API_KEY` + claim id).
3. Cut the next release tag (`v0.2.0` or similar) — CI publishes the image, the
   tool, and moves `v0`/`v0.2`. (While pre-1.0 the convenience ref is `@v0`;
   the docs note `@v1` becomes available at the 1.0 release.)
4. Verify: `dotnet tool install -g releasetwin` on a clean machine; a scratch
   repo using `uses: …@v0` runs the Action.
5. Post-`go-public-sequence`: image package → public; Action → Marketplace.

Rollback: unlist the nuget.org package version; the Docker path is unaffected.

## Open Questions

- Pre-1.0 major-tag convention: ship `@v0` now and switch guidance to `@v1` at
  the 1.0 release, or hold the floating-tag step until 1.0? (Leaning: ship `@v0`
  — it is still better than an unresolvable `@v1`. Does not change the tasks.)
