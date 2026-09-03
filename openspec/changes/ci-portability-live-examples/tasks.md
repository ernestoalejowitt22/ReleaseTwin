## 1. Engine-repo reconciliation (unblocks the release)

- [x] 1.1 `ci-report-portability` merged — PR #121 → `main` (cd373c5)
- [x] 1.2 `spa-ui-adapter-ergonomics` merged — PR #122 → `main` (9f2db0f): `ui.assertText` + `ui.waitFor` URL mode + 7 tests, `examples/cases-spa/` reference YAML, `docs/spa-testing.md`, README `Adapters.Ui` row + evidence-bullet fix. Demo apps + CI dropped.
- [x] 1.3 `express-flag-proof-example` merged — PR #123 → `main` (29a9865): `examples/cases-express/` + `docs/express.md` kept, pointing at the examples repo; `examples/express-demo/` + the `express-example` job removed.
- [x] 1.4 `examples/react-demo/` + `examples/angular-demo/` parked in the scratchpad (`demo-apps/`) for group 3; never landed in `main`.

_Main verified green after all three merges: `dotnet test` 294 passed / 0 failed (7 assemblies), Action tests 6/6._

## 2. Release the CLI with the new flags

- [ ] 2.1 Confirm `main` has `--junit-xml`, `ui.assertText`, `ui.waitFor url` after the merges
- [ ] 2.2 `credential-preflight` for the release: `NUGET_API_KEY` secret, GHCR publish perms, tag-push rights
- [ ] 2.3 Cut the release tag; verify `release.yml` publishes the container + `dotnet tool` green
- [ ] 2.4 Record the pinned version string for the pipelines

## 3. Scaffold `releasetwin-ci-examples` (GitHub, canonical)

- [ ] 3.1 Create the repo (Apache-2.0, README stub, `.gitignore`)
- [ ] 3.2 `apps/express-demo/`, `apps/react-demo/`, `apps/angular-demo/` — moved from the engine branches; each keeps its lockfile + SPDX headers + per-app README
- [ ] 3.3 `cases/`, `fixtures/`, `releasetwin.yml` — vendored from the engine repo; add a `CASES_TAG` marker file naming the engine tag
- [ ] 3.4 `scripts/check-cases-in-sync.sh` — `git archive` the tag's `examples/cases-*` + `examples/fixtures/*` from the engine repo, diff against the vendored copy, non-zero on drift
- [ ] 3.5 Top-level `README.md` — what this repo is, the three-platform matrix, links to each pipeline's latest run
- [ ] 3.6 `cli-packaging` delta spec: write `docs/…` (in the engine repo) documenting the "Playwright base image + `dotnet tool`" recipe from design D3

## 4. Pipeline files (identical logic, native reporting)

- [ ] 4.1 A shared script (`scripts/run-examples.sh`) — build all 3 apps, start + health-poll each, `dotnet tool install -g releasetwin --version <pinned>`, run cases with `--junit-xml test-results/releasetwin.xml`, `RELEASETWIN_UI_ENABLED=1`, the 3 base URLs
- [ ] 4.2 `.github/workflows/ci.yml` — Playwright/.NET image, run the shared script, publish JUnit to the run summary, evidence dir as artifact
- [ ] 4.3 `bitbucket-pipelines.yml` — same; leave `test-results/*.xml` for Bitbucket's auto-detection; artifacts for the evidence dir
- [ ] 4.4 `azure-pipelines.yml` — same; `PublishTestResults@2` (`JUnit`); `PublishBuildArtifacts` for evidence
- [ ] 4.5 Add the optional deliberately-failing case behind a pipeline flag to confirm a red `<testcase>` shows in each platform's test tab, then leave it disabled
- [ ] 4.6 `sync-cases` step wired into all three pipelines (from 3.4)

## 5. Mirror automation

- [ ] 5.1 Decide the Bitbucket account/workspace (open question 1) and Azure DevOps org (open question 2)
- [ ] 5.2 Create the Bitbucket repo + a scoped app password; store as GH secret `BITBUCKET_PUSH_TOKEN`
- [ ] 5.3 Create the Azure DevOps repo + a scoped PAT; store as GH secret `AZDO_PUSH_TOKEN`
- [ ] 5.4 `.github/workflows/mirror.yml` in the examples repo — on push to `main`, force-push the ref to both remotes; document what triggers it and how to rotate the tokens
- [ ] 5.5 First mirror run green; both remotes show the ref

## 6. Bring each platform to green (in order)

- [ ] 6.1 GitHub Actions green — all cases pass, JUnit visible in the run, evidence artifact present and inspected (open the screenshots/`.webm`, confirm real rendered screens)
- [ ] 6.2 Bitbucket Pipelines green — cases pass, **Test** tab populated from the JUnit file, evidence artifact downloadable
- [ ] 6.3 Azure Pipelines green — cases pass, **Tests** tab populated via `PublishTestResults@2`, evidence artifact present
- [ ] 6.4 Capture a run URL / screenshot from each platform for the docs

## 7. Engine-repo docs rewrite

- [ ] 7.1 `docs/ci.md` — replace the speculative Bitbucket + Azure snippets with the real pipeline files (or trimmed excerpts) + a link to `releasetwin-ci-examples` and its green runs; note the Playwright-image recipe for `ui.*` cases
- [ ] 7.2 `docs/express.md` + `docs/spa-testing.md` — "run it" points at the examples repo; keep the case walk-through
- [ ] 7.3 Engine `README.md` — a line pointing to the live examples repo as the multi-CI reference
- [ ] 7.4 `docs/ci.md` CircleCI/Jenkins snippets — reword to "adapt the pipeline files in releasetwin-ci-examples"

## 8. Verification

- [ ] 8.1 `openspec validate ci-portability-live-examples --strict`
- [ ] 8.2 Engine: `dotnet build` + `dotnet test ReleaseTwin.sln` green after all merges; report the count
- [ ] 8.3 `node --test "integrations/github-action/**/*.test.mjs"` green
- [ ] 8.4 All three example pipelines green on the same commit; the three test tabs agree on case count and outcomes
- [ ] 8.5 `check-cases-in-sync.sh` passes against the pinned tag
- [ ] 8.6 Evidence review: for each platform, the browser journey's screenshots/`.webm` show the real React/Angular screens (no blank frames, no spinner-only clips), landing where the docs say
