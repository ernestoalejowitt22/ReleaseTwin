## 1. New repo scaffold

- [ ] 1.1 Create `releasetwin-ci-examples` (public, GitHub, Apache-2.0 LICENSE,
      same author/supplier metadata pattern as `ReleaseTwin`'s `REUSE.toml`).
- [ ] 1.2 Top-level `README.md` stub: what the repo proves, one row per demo
      app / pipeline (fill in badges once each pipeline has a real run).
- [ ] 1.3 `.gitignore` for Node (`node_modules/`, build output) + CI artifacts.

## 2. Express demo app

- [ ] 2.1 `apps/express-demo/` — ~40-line Express app, one real behaviour bug
      gated behind one feature flag, plus a local flag-toggle endpoint the
      `flag_proof.control` block can drive (per `express-flag-proof-example`'s
      original scope).
- [ ] 2.2 Decide and implement the case-wiring mechanism left open in
      `design.md` (shallow-clone `ReleaseTwin`'s `examples/cases-express/` at a
      pinned ref vs. vendoring a copy here) — pick the simplest that keeps the
      case in sync with the engine repo without manual duplication drift.
- [ ] 2.3 Verify locally: boot the app, run the CLI (`ghcr.io/…/cli:<pinned>`)
      against the case, confirm known-bad fails and known-good passes.

## 3. React + Angular demo apps

- [ ] 3.1 `apps/react-demo/` and `apps/angular-demo/` — one screen, one
      client-side route change, one rendered value each (per
      `spa-ui-adapter-ergonomics`'s original scope).
- [ ] 3.2 Wire `examples/cases-spa/` the same way as 2.2 (shallow-clone or
      vendored copy — reuse whichever choice 2.2 made for consistency).
- [ ] 3.3 Verify locally: each demo's route-change → `ui.assertText` → capture
      → API-leg case passes via `RELEASETWIN_UI_ENABLED=1`.

## 4. GitHub Actions (buildable and verifiable now)

- [ ] 4.1 `.github/workflows/express.yml` — boot `apps/express-demo/`, run its
      case, fail the job on a case failure or adverse flag-proof verdict.
- [ ] 4.2 `.github/workflows/react.yml` + `.github/workflows/angular.yml` (or
      one matrixed workflow) — same shape for the SPA demos.
- [ ] 4.3 Push, confirm all GitHub Actions jobs green. This is the first real
      proof point and needs no new account.

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
