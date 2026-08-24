## Why

`docs/installation-model.md` explicitly notes no browser-level (Playwright/Cypress) tests exist of ReleaseTwin's own `web/` frontend, deliberately not added speculatively by `hosted-react-frontend` — the same "one dependency per real need" reasoning applied to every other adapter/integration in this project. That need is now real: this session did the actual first end-to-end walkthrough by hand (real Clerk sign-in, real dashboard, real API calls) — Cypress automates exactly that walkthrough instead of it being a manual, one-off verification every time.

This is pure test tooling — no product behavior changes, so no spec deltas (`skip_specs: true`).

## What Changes

- Add `@clerk/testing` (Clerk's official first-party Cypress integration) and `cypress` as dev dependencies in `web/`.
- `web/cypress.config.ts` wired with `clerkSetup()`; `cypress/support/e2e.ts` wired with `addClerkCommands()`.
- A dedicated, scripted test user (not anyone's personal account) provisioned via Clerk's Backend API, idempotently, signing in via `email_code` strategy with Clerk's `+clerk_test@` test-address convention — a fixed, known verification code, no real email delivery. (Originally planned as `password` strategy; revised during implementation once Clerk's Device Trust feature — a second-factor challenge for password sign-ins from any new device, with no supported bypass — made that impossible for automated runs. See design.md.)
- One initial spec covering the walkthrough already verified by hand this session: sign in → dashboard loads → create a project → issue a token (confirm the "shown once" banner) → sign out. GitHub connections excluded (still no registered OAuth App for that flow — same deferral as everywhere else in this project).
- `start-server-and-test` (or equivalent) to boot both the hosted API (.NET) and the Next.js dev/build server before Cypress runs, and tear them down after — this is genuinely two different runtimes needing to be up together, not something `npm test` alone can express.

**Explicitly out of scope**:
- Any CI pipeline/GitHub Actions wiring — this repo has no CI pipeline at all yet (a separate, real gap named in README's "What's not built yet"); this change makes Cypress runnable locally, wiring it into CI is its own future step.
- GitHub Connections coverage (blocked on a registered GitHub OAuth App).
- Any product code change — this only touches `web/`'s dev tooling.
- Migrating any existing `.NET`-side test to Cypress — `dotnet test` stays the source of truth for backend behavior; Cypress covers what only a real browser + real Clerk session can prove.

## Capabilities

### New Capabilities
(none — pure test tooling, `skip_specs: true`)

### Modified Capabilities
(none)

## Impact

- `web/package.json`: new dev dependencies (`cypress`, `@clerk/testing`, `start-server-and-test`).
- `web/cypress.config.ts`, `web/cypress/support/e2e.ts`, `web/cypress/e2e/*.cy.ts`: new files.
- A new one-time-idempotent test-user-provisioning script/task using `CLERK_SECRET_KEY`.
- README.md: document the new `npm run e2e`-shaped command and its prerequisites (password sign-in enabled on the Clerk instance, `E2E_TEST_USER_*` env vars).
- No change to `ReleaseTwin.Core`, `ReleaseTwin.AdapterSdk`, any adapter, the CLI, or the hosted API's production code.
