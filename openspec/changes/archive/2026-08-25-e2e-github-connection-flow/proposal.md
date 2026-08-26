## Why

`hosted-platform-deployment` task 9.2 ("Create a project; click 'Connect GitHub'; authorize with a
real GitHub account; confirm the connection shows on the dashboard") is still an unchecked, manual
step — the GitHub connection flow (`GitHubConnectionFlowService`, `ConnectionEndpoints`) has no
automated coverage at all, unlike every other customer-facing flow in `web/cypress/e2e`. The
existing e2e suite deliberately excludes it (see `dashboard-walkthrough.cy.ts`'s own comment: "no
registered OAuth App for that flow yet"). A real OAuth App now exists for production
(`hosted-platform-deployment` group 8), which makes automating this finally possible.

## What Changes

- A second, dedicated GitHub OAuth App is registered (manually, by the project owner) with
  callback URL `http://localhost:3000/connect/github/callback`, separate from the production app —
  GitHub OAuth Apps allow exactly one callback URL each, so the production app (pointed at the real
  Vercel URL) can't also serve local Cypress runs.
- The locally-run hosted API is configured with this second app's `GitHubConnection:ClientId` /
  `ClientSecret` / `CallbackUrl` when running the e2e suite, the same way `Clerk__Domain` is already
  passed as an inline env var in `npm run e2e:api`.
- A new Cypress spec drives the real, unmocked GitHub OAuth flow end to end: real sign-in as the
  project owner's real GitHub account (TOTP-based 2FA solved by generating a valid code from a
  stored secret, not by disabling 2FA), the real GitHub consent screen, the real repo-listing API,
  and a real repo selection (`ernestoalejowitt22/NAHA`) confirmed as the project's connection on the
  dashboard.
- The GitHub account's password and TOTP secret are read from AWS Secrets Manager at test-run time
  (a new Cypress task, using whichever AWS credentials are already available in the environment
  running Cypress — no new credential-provisioning mechanism), not committed to any file in the
  repo and not placed in `cypress.env.json` alongside the existing Clerk test credentials, since
  unlike those this is the project owner's actual personal account access.

## Capabilities

No spec-level behavior changes — this adds test coverage for an existing, already-specified flow
(`project-connections`). `.openspec.yaml` sets `skip_specs: true`.

## Impact

- `web/cypress.config.ts`: a new task to fetch the GitHub account's password + TOTP secret from AWS
  Secrets Manager and generate a current TOTP code (new `@aws-sdk/client-secrets-manager` and TOTP
  library dependencies in `web/package.json`).
- `web/cypress/e2e/`: a new spec exercising the real OAuth round trip via `cy.origin('https://github.com')`.
- `web/package.json` / local run instructions: how the local hosted API picks up the second OAuth
  App's `GitHubConnection:*` configuration for e2e runs.
- No changes to `hosted/ReleaseTwin.Hosted.Api` application code — `GitHubConnectionFlowService`
  and `ConnectionEndpoints` already work exactly as this flow needs; only their configuration
  differs between a real e2e run and production.
