## Context

See [proposal.md](proposal.md) — Why. Three pieces of work are in flight on
separate branches, none merged:

| branch | state |
|---|---|
| `changes/ci-report-portability` | done, PR #121 open. `--junit-xml`, GitLab component, `docs/ci.md` snippets. |
| `changes/express-flag-proof-example` | applied + committed (70bbc07). In-repo `examples/express-demo/` + `express-example` GH Actions job. |
| `changes/spa-ui-adapter-ergonomics` | `ui.assertText` + `ui.waitFor` URL mode implemented + 7 tests green (uncommitted). `examples/react-demo/` + `examples/angular-demo/` built (uncommitted). |

The published CLI image today is .NET-only — the in-repo UI tests install
Playwright separately. `docs/ci.md` guidance: always pin a released CLI version
in CI.

User decisions (2026-09-03): one GitHub-canonical examples repo mirrored to
Bitbucket + Azure DevOps; browser cases run in **all** external pipelines;
merge + release `ci-report-portability` before the pipelines pin a version.

## Goals / Non-Goals

**Goals:**

- `docs/ci.md`'s Bitbucket and Azure guidance is a real pipeline file with a
  green run behind it, not a guess.
- The engine repo carries no Node/React/Angular build toolchain.
- One canonical source for the demo apps + pipelines; three CI systems run it.
- A pinned, released CLI runs HTTP **and** browser cases in third-party CI.

**Non-Goals:**

- A published npm/marketplace package for the demo apps.
- Testing every CI system in `docs/ci.md` (CircleCI, Jenkins…) — Bitbucket +
  Azure + GitHub is the matrix; others stay as doc snippets that now point at
  the real files to adapt.
- Keeping the vendored case files in the examples repo automatically in lockstep
  with engine `main` — they pin a tag; a sync check flags drift, a human bumps.
- Hosted-dashboard upload from the example pipelines (they run offline, exit-code
  only).
- Moving `examples/cases-*` YAML out of the engine repo — it stays as the
  canonical reference.

## Decisions

### D1: One repo, GitHub-canonical, mirrored by a push job

`github.com/ernestoalejowitt22/releasetwin-ci-examples` is the source of truth.
A GitHub Actions job on push to `main` mirrors the ref to `bitbucket.org/…` and
an Azure DevOps repo using app-password / PAT credentials stored as GH secrets
(`BITBUCKET_PUSH_TOKEN`, `AZDO_PUSH_TOKEN`). Each platform runs its own pipeline
file from the pushed copy.

- Rejected — three independent repos: 3× drift and maintenance for marginal
  extra fidelity.
- Rejected — Bitbucket/Azure canonical: the user's tooling, auth, and every
  other repo are on GitHub; mirroring outward is the low-friction direction.

### D2: Case files are vendored, pinned, and drift-checked

The examples repo holds a copy of `cases/`, `fixtures/`, `releasetwin.yml` under
a `RELEASETWIN_CASES_TAG` marker (the engine tag they were copied from). A CI
step diffs the vendored copy against that tag's files in the engine repo (raw
`git archive` fetch, no submodule) and fails if they differ — so an engine-side
case change surfaces as a red build in the examples repo, and bumping is a
deliberate PR.

- Rejected — git submodule: Bitbucket Pipelines and Azure both need extra config
  for submodule checkout; friction for a copy that changes rarely.
- Rejected — `curl` the raw files at pipeline time: hides what is actually being
  tested; a reader of the repo can't see the cases.

### D3: Browser cases run on a Playwright base image + the CLI as a .NET tool

Each pipeline uses `mcr.microsoft.com/playwright/dotnet:v<ver>-<distro>` (ships
.NET + browsers + OS deps) as the job image, then
`dotnet tool install -g releasetwin --version <pinned>` and runs with
`RELEASETWIN_UI_ENABLED=1`. This needs **no change to `release.yml`** — the
existing `dotnet tool` artifact is enough once browsers are present in the image.

`cli-packaging` gains a requirement (see the delta spec) that this recipe is
documented and supported. The alternative — a `cli:<ver>-playwright` container
variant built by `release.yml` — is heavier (a second image to build, scan,
publish per release) and deferred; the delta spec is written to allow it later
without another spec change.

### D4: Release sequencing

1. Merge `changes/ci-report-portability` (PR #121) to `main`.
2. Merge `changes/spa-ui-adapter-ergonomics` (code + `docs/spa-testing.md` only,
   after the scope reduction in D6).
3. Cut a CLI release tag from `main` → the `dotnet tool` + container carry
   `--junit-xml` and `ui.assertText` / `ui.waitFor url`.
4. Stand up `releasetwin-ci-examples` pinned to that version.

Steps 1–3 gate step 4; the examples repo can be scaffolded (apps, README) in
parallel but its pipelines stay red until the pinned version exists.

### D5: Per-platform pipeline shape (identical logic, native reporting)

Each of `bitbucket-pipelines.yml`, `azure-pipelines.yml`,
`.github/workflows/ci.yml`:

1. build `apps/express-demo` (npm ci), `apps/react-demo` (vite build),
   `apps/angular-demo` (ng build)
2. start all three: `express-demo` on `:4599`, `react-demo` via `vite preview`
   on `:4173`, `angular-demo` via `serve -s` on `:4174`; poll health
3. `dotnet tool install -g releasetwin --version <pinned>`
4. run the cases with `--junit-xml test-results/releasetwin.xml`,
   `RELEASETWIN_UI_ENABLED=1`, and the three base URLs as env
5. publish `test-results/*.xml` to the native report:
   - Bitbucket: drop it at `test-results/*.xml` (auto-detected)
   - Azure: `PublishTestResults@2`, `testResultsFormat: JUnit`
   - GitHub: `mikepenz/action-junit-report` (or job summary)
6. keep the evidence dir (screenshots / `.webm`) as a pipeline artifact

### D6: Engine-repo reconciliation

- **`spa-ui-adapter-ergonomics`**: drop tasks 4, 5, 7 (React demo, Angular demo,
  CI) and the demo-app files. Keep the operations (done), the delta spec, the
  `examples/cases-spa/` YAML, `docs/spa-testing.md`, and the evidence-doc fix.
  The uncommitted `examples/react-demo/` + `examples/angular-demo/` move to the
  examples repo.
- **`express-flag-proof-example`** (already committed 70bbc07): a follow-up
  commit on that branch removes `examples/express-demo/` and the `express-example`
  job, and points `docs/express.md` at the examples repo for the runnable app.
  `examples/cases-express/` YAML stays.
- **`ci-report-portability`**: unchanged; just needs to merge. `docs/ci.md`
  rewrite lands in *this* change, after the examples repo is green.

### D7: What each case proves in the matrix

| case | type | proves |
|---|---|---|
| `express` flag-proof | HTTP + `flag_proof.control` | known-bad/known-good verdict → JUnit `<testcase>` per leg-outcome, in every platform's test tab |
| `express` contract | HTTP | plain pass → JUnit |
| `react` journey | `ui.*` + HTTP | route-change wait + `assertText` + capture→API, browser running in external CI |
| `angular` journey | `ui.*` + HTTP | same, Angular |
| a deliberately failing case (optional, `continue-on-error`) | any | non-zero exit + a red `<testcase>` actually surfaces in each platform |

## Risks / Trade-offs

- **Playwright in Bitbucket / Azure is unproven here** → D3's base-image choice
  is the mitigation; if the Playwright image doesn't work on a runner, fall back
  to `npx playwright install --with-deps` on a plain image, still no engine
  change. Prove this on one platform before wiring all three.
- **Mirror credentials leak / rot** → scoped push-only tokens, stored as GH
  secrets, rotable; the mirror job is the only consumer.
- **Vendored cases drift silently** → D2's sync check makes drift a red build.
- **Release step blocks the whole change** → apps + pipeline files + README can
  be built and reviewed against a locally-built tool first; only the final
  "pin the released version" flip waits on the release.
- **Three CI systems = 3× flake surface** → each pipeline is independent; a
  Bitbucket outage doesn't block Azure. None gates engine-repo PRs (the engine's
  own unit tests do).
- **"Real Bitbucket user" fidelity is still imperfect** (mirrored, not native)
  → acceptable; the pipeline file is genuine and a reader can copy it verbatim.

## Migration Plan

1. Merge PR #121; merge the reduced `spa-ui-adapter-ergonomics`; follow-up commit
   on `express-flag-proof-example` to remove its demo app + job; merge it.
2. Cut the CLI release.
3. Create `releasetwin-ci-examples`; add apps (moved from the branches), vendored
   cases, three pipeline files, mirror job, README.
4. Create the Bitbucket + Azure DevOps repos; add push credentials as GH secrets;
   run the mirror.
5. Enable pipelines on each platform; iterate to green (start with GitHub, then
   Bitbucket, then Azure).
6. Rewrite `docs/ci.md` Bitbucket/Azure sections to point at the real files +
   green runs; link from `docs/express.md` and `docs/spa-testing.md`.

Rollback: the examples repo is standalone — deleting it or disabling its
pipelines has zero effect on the engine or the hosted platform. The engine-repo
doc changes revert independently.

## Open Questions

- Bitbucket account: personal or a workspace under the ReleaseTwin org identity?
  (Does not change the design; needed before step 4.)
- Which Azure DevOps org — the existing `AZDO_*` adapter-sandbox org, or a fresh
  one for public-facing examples? (Naming/visibility only.)
