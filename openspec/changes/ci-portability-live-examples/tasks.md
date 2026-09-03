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

- [ ] 5.1 **Needs the user to create a Bitbucket Cloud account/workspace** —
      not performable by an agent.
- [ ] 5.2 Add a GitHub Actions job that push-mirrors `main` to the Bitbucket
      Cloud repo on every push to `main`, using a Bitbucket app password or
      access token stored as a GitHub secret. Fail visibly (not silently) if
      the mirror push fails, per `design.md`'s risk mitigation.
- [ ] 5.3 `bitbucket-pipelines.yml` at repo root — boot each demo app, run its
      case via the CLI image, same behavior as the GitHub Actions jobs.
- [ ] 5.4 **Needs the user to connect the mirrored Bitbucket repo to Bitbucket
      Pipelines** (enable Pipelines in repo settings) and confirm one real
      green run.

## 6. Azure Pipelines (blocked on an Azure DevOps account)

- [ ] 6.1 **Needs the user to create an Azure DevOps organization/project** —
      not performable by an agent.
- [ ] 6.2 `azure-pipelines.yml` at repo root, platform-agnostic (no hardcoded
      org/project name) — same boot-and-run shape as the other two.
- [ ] 6.3 **Needs the user to create an Azure Pipeline pointed at the
      `releasetwin-ci-examples` GitHub repo** (external Git source — no mirror
      needed, per `design.md`) and confirm one real green run.

## 7. Close the loop in the engine repo

- [ ] 7.1 Once at least one of Bitbucket/Azure has a real green run, add a
      one-line pointer in `ReleaseTwin`'s `docs/ci.md` ("these are proven, not
      just typed — see `releasetwin-ci-examples`") near the relevant snippet.
- [ ] 7.2 Confirm with the user before archiving.
