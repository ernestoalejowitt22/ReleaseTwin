## Context

See proposal.md — Why. Grounding facts:

- The code under test (`flag_proof.control` + `control.verify`, `ControlUnverified`)
  shipped in `flag-proof-control-readback` (PR #63) with full stub-level coverage.
  This change adds **only** a real-endpoint test + its CI wiring.
- `web/cypress/e2e/launchdarkly-real-flag-proof.cy.ts` is the template: it pulls
  LD creds from `releasetwin/e2e/launchdarkly-account` via the Cypress config task
  `fetchLaunchDarklyTestAccount` (`new SecretsManagerClient({})`, ambient AWS
  creds), drives the real dashboard, issues a real project token, and runs the
  real CLI through `cy.task("runCli", …)`. It exercises the **LD adapter** path.
- `web/cypress/e2e/project-secrets-runtime.cy.ts` is the second template: it
  stores a `${VAR}` through the dashboard's project-secrets form and proves the
  CLI resolves it from hosted infra (not local env) to drive a real
  `http.request`. The `control` block uses that same `${VAR}` resolver.
- `cy.task("runCli")` shells `dotnet run … -- <casesDir>` with
  `RELEASETWIN_API_TOKEN` / `RELEASETWIN_API_URL` plus any extra `env`. With a
  token set, `CliRunner` builds an `IngestClient` and the hosted project-secrets
  fetch is active.
- No GitHub Actions workflow in this repo runs any Cypress e2e today. The deploy
  OIDC role (`hosted/terraform-bootstrap/main.tf`, output `github_actions_role_arn`)
  is scoped to `releasetwin-dev-*` deploy resources and has **no**
  `secretsmanager:GetSecretValue`. An IAM **user** `releasetwin-e2e-secrets-reader`
  can read the e2e secrets (used by `demo/record.sh` locally).

## Goals / Non-Goals

**Goals:**
- One real round trip: toggle a real LD flag over LD's REST API from a `control`
  block, read it back with `control.verify`, and get a deterministic `Passed`.
- Run it in GitHub Actions on demand + nightly, authenticating to AWS via OIDC —
  no static AWS key added anywhere (CLAUDE.md: CI-only, OIDC).

**Non-Goals:**
- A deterministic real-endpoint test of the `ControlUnverified` leg (can't make a
  real LD toggle reliably no-op) — stays unit-covered.
- Testing the LD *adapter* path — already covered by `launchdarkly-real-flag-proof`.
- Wiring the rest of the Cypress e2e suite into CI — out of scope; this workflow
  runs one spec.

## Decisions

### D1: Credentials reach the `control` block via the hosted project-secrets path

The spec stores the LD REST API token (and project/env keys) as **project
secrets** through the real dashboard form, exactly like `project-secrets-runtime.cy.ts`.
The generated case's `control`/`verify` templates reference `${LD_API_TOKEN}` etc.;
`CliRunner` resolves them from hosted infra because no matching local env var is
set.

- **Why over passing them as `runCli` `env`?** It proves the
  customer-realistic path (secret lives in ReleaseTwin, not the CI job's shell)
  and it's the same resolver the `control` block shares with `http.request`.
  Passing via `env` would test a strictly easier path.
- **Cost:** three secrets entered through the form instead of one `cy.task` arg.
  Acceptable — the form interaction is already a solved pattern.

### D2: The flag is toggled with a JSON Patch `PATCH`

```yaml
control:
  method: PATCH
  url: https://app.launchdarkly.com/api/v2/flags/${LD_PROJECT_KEY}/{{featureKey}}
  headers:
    Authorization: ${LD_API_TOKEN}
    Content-Type: application/json
  body: '[{"op":"replace","path":"/environments/E2E_ENV/on","value":{{enabled}}}]'
verify:
  method: GET
  url: https://app.launchdarkly.com/api/v2/flags/${LD_PROJECT_KEY}/{{featureKey}}
  json_path: $.environments.E2E_ENV.on
  expected: "{{enabled}}"
```

- `{{enabled}}` → `true` / `false` substitutes into the raw body as a JSON
  boolean literal, and into `expected` as `"true"` / `"false"` — which
  `JsonPathMatch` matches against LD's JSON boolean (it normalises booleans).
- **`E2E_ENV` is baked in by the generator, not `${...}`-interpolated**, because
  `json_path` is deliberately not env-interpolated (it's a path expression, same
  as `http.assertJsonPath`'s `path`). The `writeHttpFlagControlCase` task has the
  environment key from the secret and writes it literally into both `body`'s path
  and `json_path`.
- **Why JSON Patch over LD semantic patch?** `{{enabled}}` maps cleanly to a
  boolean `value`; semantic patch needs `turnFlagOn`/`turnFlagOff` instruction
  kinds that no substitution token produces.

### D3: A dedicated OIDC role for e2e secret reads

Add `aws_iam_role.github_actions_e2e` to `hosted/terraform-bootstrap/main.tf`
(same OIDC provider, same repo `sub` conditions as the deploy role) with a single
inline policy: `secretsmanager:GetSecretValue` on
`arn:aws:iam::846136340491:secret:releasetwin/e2e/*`. New output
`github_actions_e2e_role_arn`; the workflow reads it from a repo variable
`AWS_E2E_ROLE_ARN`.

- **Why a new role, not `+secretsmanager` on the deploy role?** Keeps deploy
  permissions and read-only test-secret access separate; a leaked e2e-job token
  can't touch `releasetwin-dev-*` infra.
- **Why not reuse the `releasetwin-e2e-secrets-reader` IAM user's key as GitHub
  secrets?** Static long-lived credential — against the OIDC-only convention.
- **Manual step:** applying this needs a re-run of the `oidc-and-role` job in
  `bootstrap.yml` (`workflow_dispatch`), and setting the `AWS_E2E_ROLE_ARN` repo
  variable from its output. Unavoidable — it provisions the very trust the
  workflow then uses. Listed in tasks.md as user-run.

### D4: Workflow shape

`.github/workflows/ld-http-flag-control-e2e.yml`: `workflow_dispatch` + nightly
`schedule`. Steps: checkout → `configure-aws-credentials` (OIDC, `AWS_E2E_ROLE_ARN`)
→ setup dotnet + node → start the API and web dev servers (reuse
`web` scripts `e2e:api` / `e2e:web`, as `e2e:ld` does) → `npm run e2e:run:ld-http`.
Job fails on any non-`Passed`. Not added to branch protection (nightly signal,
not a merge gate — a third-party API being down shouldn't block merges).

## Risks / Trade-offs

- **LD API outage / rate limit → red nightly.** → It's a nightly/dispatch job,
  not a required check. A flake shows up as a nightly failure to investigate, not
  a blocked PR.
- **Test pollution of the shared LD test flag.** → The case toggles the flag to a
  known end state (known-good = on) and its assertion doesn't depend on the prior
  value; concurrent runs are the only real hazard — nightly + manual dispatch
  makes collisions unlikely. Use a dedicated flag key (`e2e.http-flag-control`),
  not the one the adapter spec uses.
- **`E2E_ENV` baked into the case means the generator must know it.** → It already
  receives `environmentKey` from the secret (same shape as
  `writeLaunchDarklyFlagProofCase`).
- **Re-bootstrap friction (D3).** → One-time; documented as a user-run task.

## Open Questions

- Dedicated LD flag key + its creation: create `e2e.http-flag-control` in the LD
  test project by hand once, or have the spec create it via the LD API first?
  Leaning "create once by hand" (a fixture, like the adapter spec's
  `naha.service-catalog-api`), but either works without changing the task
  breakdown — the spec just needs *a* toggleable flag key.
