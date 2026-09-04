## 1. New repo scaffold

- [x] 1.1 Created `releasetwin-ci-examples` (public, GitHub, Apache-2.0
      LICENSE). REUSE.toml deferred — single-license repo, LICENSE file at
      root is sufficient; revisit only if per-file license mix is ever needed.
- [x] 1.2 Top-level `README.md` stub added.
- [x] 1.3 `.gitignore` added.

## 2. Express demo app

- [x] 2.1 `apps/express-demo/` built — `server.js`, `package.json`,
      `README.md`. `GET /orders/:id` omits tax unless `orders-v2` is enabled;
      `GET`/`PUT /admin/flags/:key` toggle it over REST. `npm audit` flagged a
      moderate `qs` DoS advisory in Express 4.22.2's transitive deps (no fix
      available on the 4.x line) — accepted for a non-production, local-only
      demo rather than risking an Express 5 migration for it.
- [x] 2.2 Case-wiring: the CLI resolves `examples/cases-express/` directly
      from `ReleaseTwin` (no clone/vendor needed) — verified by running
      `dotnet run --project src/ReleaseTwin.Cli -- run examples/cases-express`
      from that repo. Simpler than either option `design.md` posed, since the
      case files already live there and don't need to travel with this repo.
      (Along the way, found and fixed a false alarm: I initially thought the
      cases' fixtures were missing — they're not, they resolve from
      `examples/fixtures/`, a directory outside `cases-express/`/`cases-spa/`
      that my first search missed. Reverted that incorrect edit.)
- [x] 2.3 Verified: booted the app, ran the real cases via `dotnet run`
      (not yet the packaged CLI image) —
      `PASS EXPRESS-CONTRACT-1` / `FLAGPROOF EXPRESS-FLAGPROOF-1 (Passed)`,
      2 passed, 0 failed. Matches `docs/express.md`'s documented output
      exactly.

## 3. React + Angular demo apps

- [x] 3.1 `apps/react-demo/` (Vite + React Router 7) and `apps/angular-demo/`
      (Angular CLI 18, `--minimal`) built — home → route change to
      `/detail/42` → rendered id, plus a cookie-gated `/admin` route for
      `admin-cookie.yaml`. `react-router-dom` and Vite's `esbuild` had
      moderate/high advisories at the versions `npm install` picked; both
      resolved via `npm audit fix --force` (react-router-dom 6→7, vite 5→8) —
      verified the app still builds and the real cases still pass after the
      bump. Angular's own `npm audit` shows many findings, all in
      `@angular/cli`'s build toolchain (webpack/esbuild/rollup, dev-only,
      never in the shipped bundle) — accepted, same reasoning as 2.1.
- [x] 3.2 Case-wiring: same answer as 2.2 — the CLI resolves
      `examples/cases-spa/` directly from `ReleaseTwin`, no clone/vendor step.
- [x] 3.3 Verified against real running instances: `SPA-REACT-JOURNEY-1` +
      `SPA-ADMIN-COOKIE-1` passed against `react-demo`;
      `SPA-ANGULAR-JOURNEY-1` + `SPA-ADMIN-COOKIE-1` passed against
      `angular-demo` — all via `RELEASETWIN_UI_ENABLED=1` with a real
      Playwright browser, `API_BASE_URL=https://postman-echo.com` for the API
      leg.

## 4. GitHub Actions (buildable and verifiable now)

- [x] 4.1 `.github/workflows/express.yml` — boots `apps/express-demo/`, runs
      its cases via the published `ghcr.io/…/releasetwin/cli:0.2.0` image.
      First run failed: mounted only `cases-express/`, but fixtures resolve
      from a sibling `fixtures/` dir next to the cases root — fixed by
      mounting the whole `examples/` dir instead.
- [x] 4.2 `.github/workflows/react.yml` + `.github/workflows/angular.yml` —
      the published CLI image has the UI adapter compiled in but no Chromium
      (see the Dockerfile's own comment), so these two build the engine from
      source and install Playwright's Chromium instead, same as
      `ReleaseTwin`'s own `ci.yml` does for `ReleaseTwin.Adapters.Ui.Tests`.
- [x] 4.3 Pushed, all three green after one fix round: Express's mount (4.1)
      and a react-demo `npm ci` failure — `vite@8` + `@vitejs/plugin-react@4`
      (only supports vite up to ^7) peer conflict, bumped plugin-react to
      `^6.1.1`. Angular was green on the first push. Confirmed via
      `gh run list --repo ernestoalejowitt22/releasetwin-ci-examples`.

## 5. Bitbucket mirror + pipeline (blocked on a Bitbucket account)

- [x] 5.1 User confirmed the Bitbucket workspace already exists:
      https://bitbucket.org/releasetwin/workspace/overview/ (2026-09-03).
- [x] 5.2 `.github/workflows/mirror-to-bitbucket.yml` added — force-pushes
      `main` to `bitbucket.org/releasetwin/releasetwin-ci-examples.git`.
      Bitbucket app passwords turned out to be deprecated; switched to a
      **repository access token** (`BITBUCKET_REPO_TOKEN` secret), which
      authenticates over git+https as username `x-token-auth`. Verified
      working: mirror job pushes successfully on every push to `main`.
- [x] 5.3 `bitbucket-pipelines.yml` at repo root — **not using the CLI
      image** as originally scoped: Bitbucket's Docker-in-Docker doesn't
      cleanly reach a service the same step just booted on localhost, so
      (like react.yml/angular.yml) it builds the engine from source and
      installs Playwright directly (PowerShell + the `playwright.ps1` script
      the `Microsoft.Playwright` package generates at build time —
      `Microsoft.Playwright.CLI` turned out to be deprecated, stopped
      publishing after 1.2.3).
- [x] 5.4 Bitbucket Pipelines enabled (user did this manually — the
      repository-access-token auth doesn't carry a full user session, and
      the enable-pipelines API endpoint requires one). First mirror repo
      (`releasetwin-ci-examples`) turned out to live in a private Bitbucket
      project with no API path to make it public (needs a workspace token,
      which needs Premium) — moved to a new public repo,
      `releasetwin-ci-example-projects`. **Real green run confirmed**:
      build #1
      (https://bitbucket.org/releasetwin/releasetwin-ci-example-projects/pipelines/results/1)
      — all three steps (Express/React/Angular demo) passed, after fixing a
      `bash -qq` typo and the deprecated `Microsoft.Playwright.CLI` mistake
      on the original repo's builds #1–#2.

## 6. Azure Pipelines (blocked on an Azure DevOps account)

- [x] 6.1 User confirmed the org/project already exist:
      https://ernestotesting.visualstudio.com/My%20First%20Project
      (2026-09-03).
- [x] 6.2 `azure-pipelines.yml` at repo root — platform-agnostic (no
      hardcoded org/project name), three jobs (one per demo), same
      build-from-source + Playwright approach as react.yml/angular.yml/
      bitbucket-pipelines.yml, for the same reason (published CLI image has
      no Chromium).
- [ ] 6.3 **Blocked on credentials, not the account**: an Azure DevOps PAT
      (Build: Read & execute; Service Connections: Read, query & manage;
      Project and Team: Read) to create the pipeline + a GitHub service
      connection via REST API, and a GitHub PAT (repo/contents:read on
      `releasetwin-ci-examples`) for that service connection to authenticate
      with. Requested from the user via credential-preflight (2026-09-03) —
      not yet provided. (No mirror needed here — Azure Pipelines builds the
      GitHub repo directly.)

## 7. Close the loop in the engine repo

- [x] 7.1 Pointer added in `docs/ci.md` linking the real, verified Bitbucket
      build (updated once more when the mirror moved repos, 7efaf77); notes
      Azure is still pending its own real run. Also updated the private
      `releasetwin-platform` landing page + `/docs/ci` docs page with the
      same real-proof link (out of this repo's scope, but the user asked
      for it alongside this task).
- [ ] 7.2 Confirm with the user before archiving.
