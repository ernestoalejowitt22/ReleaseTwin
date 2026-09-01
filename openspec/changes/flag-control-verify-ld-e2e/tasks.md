## 1. Cypress config task

- [ ] 1.1 Add `writeHttpFlagControlCase({ directory, caseId, flagKey, environmentKey })` to `web/cypress.config.ts` (sibling of `writeLaunchDarklyFlagProofCase`): writes `cases/<caseId>.yaml` + `fixtures/<caseId>.json`, with a pipeline of `http.request` (GET the flag) + `http.assertJsonPath` on `$.environments.<environmentKey>.on`, and a `flag_proof` block with a `control` + `control.verify` targeting LD's REST API per design D2. Bake `environmentKey` literally into the `control.body` JSON-Patch path and into `verify.json_path`; use `${LD_API_TOKEN}` / `${LD_PROJECT_KEY}` for the interpolated fields. Return `{ casesDir }`.
- [ ] 1.2 Reuse `fetchLaunchDarklyTestAccount`, `runCli`, `ensureE2ETestUser` unchanged.

## 2. Cypress spec

- [ ] 2.1 New `web/cypress/e2e/launchdarkly-http-flag-control.cy.ts`, header comment explaining it closes `flag-proof-control-readback` task 6.3 and covers the vendor-neutral `control`/`verify` path (not the `ld.*` adapter).
- [ ] 2.2 `before`: `cy.task("ensureE2ETestUser")`. In the test: `fetchLaunchDarklyTestAccount` → sign in (Clerk testing token) → create a project.
- [ ] 2.3 Through the real "Project secrets" form, add `LD_API_TOKEN` (the LD REST token), `LD_PROJECT_KEY`, and `LD_ENV_KEY` as project secrets. Issue a project token.
- [ ] 2.4 `cy.task("writeHttpFlagControlCase", { flagKey: "e2e.http-flag-control", environmentKey })`, then `cy.task("runCli", { token, apiUrl: "http://localhost:5199", casesDir }, { timeout: 180000 })` — no `LD_*` in `env`, forcing hosted project-secret resolution.
- [ ] 2.5 Assert `stdout` matches `^FLAGPROOF <caseId> \(Passed\)$` (multiline), with `stdout`/`stderr` in the failure message like the existing LD spec.

## 3. Scripts and docs

- [ ] 3.1 `web/package.json`: add `"e2e:run:ld-http": "cypress run --spec cypress/e2e/launchdarkly-http-flag-control.cy.ts"` and an `"e2e:ld-http"` wrapper mirroring `e2e:ld` (`start-server-and-test` for the API + web).
- [ ] 3.2 `docs/flag-proof.md`: one line under the `control.verify` section pointing at the spec as the real-endpoint proof. `demo/README.md`: note the new script alongside `e2e:run:ld`.

## 4. AWS OIDC role (Terraform)

- [ ] 4.1 In `hosted/terraform-bootstrap/main.tf`: add `aws_iam_role.github_actions_e2e` reusing `data.aws_iam_policy_document.github_actions_assume_role` (same OIDC provider + `sub` conditions), with an inline policy granting `secretsmanager:GetSecretValue` on `arn:aws:iam::846136340491:secret:releasetwin/e2e/*` only.
- [ ] 4.2 Add output `github_actions_e2e_role_arn`.
- [ ] 4.3 **Needs the user to run this:** re-run the `oidc-and-role` job in `.github/workflows/bootstrap.yml` (`workflow_dispatch`) to apply, then set repo variable `AWS_E2E_ROLE_ARN` from the new output.

## 5. GitHub Actions workflow

- [ ] 5.1 New `.github/workflows/ld-http-flag-control-e2e.yml`: `on: [workflow_dispatch, schedule (nightly cron)]`, `permissions: { id-token: write, contents: read }`.
- [ ] 5.2 Steps: checkout → `aws-actions/configure-aws-credentials@v4` with `role-to-assume: ${{ vars.AWS_E2E_ROLE_ARN }}` → `setup-dotnet` 8.0.x → `setup-node` + `npm ci` in `web/` → `npm run e2e:ld-http` (starts API + web, runs the spec).
- [ ] 5.3 Confirm the job fails on a non-`Passed` outcome (Cypress non-zero exit propagates). Do **not** add it to branch-protection required checks.

## 6. Close the loop

- [ ] 6.1 In `openspec/changes/archive/2026-09-01-flag-proof-control-readback/tasks.md`, tick task 6.3 and append a note: "covered by `web/cypress/e2e/launchdarkly-http-flag-control.cy.ts` + `.github/workflows/ld-http-flag-control-e2e.yml` (change `flag-control-verify-ld-e2e`)."

## 7. Verification

- [ ] 7.1 `web` build clean: `npm run build` + `npx eslint` in `web/` (the new spec + config task compile).
- [ ] 7.2 `openspec validate flag-control-verify-ld-e2e --strict` passes.
- [ ] 7.3 **Needs the user to run this (needs AWS + LD):** locally, with an AWS session that can read `releasetwin/e2e/launchdarkly-account`, run `npm run e2e:ld-http` and confirm a real `FLAGPROOF … (Passed)`. Then trigger the workflow via `workflow_dispatch` once and confirm it's green.
