## 1. Registration spec

- [x] 1.1 Add a disposable `+clerk_test@` address (e.g. `releasetwin-e2e-registration+clerk_test@gmail.com`) to `cypress.env.json`/`.example`, distinct from the existing `E2E_TEST_USER_EMAIL` (that one stays pre-seeded for the other specs; this one must start unregistered).
- [x] 1.2 Add `web/cypress/e2e/registration.cy.ts`: `setupClerkTestingToken()`, visit `/sign-in`, click the "Sign up" link, follow the cross-origin navigation to Clerk's hosted Account Portal.
- [x] 1.3 Drive the real hosted sign-up form: enter the disposable email, submit, enter the verification code (confirm empirically whether Clerk's documented `424242` test-OTP convention holds for this instance).
- [x] 1.4 Assert landing back on `/dashboard` with a real, freshly-provisioned organization (empty project list, Free plan tier visible).
- [x] 1.5 Handle the "address already registered" branch gracefully (this address is reused across runs, not regenerated) — detect Clerk's own "already have an account" state and treat it as an acceptable outcome, not a test failure, per design.md.

## 2. `runCli` Cypress task

- [x] 2.1 Add a `runCli` task to `cypress.config.ts` (alongside `ensureE2ETestUser`), shelling out via `child_process` to `dotnet run --project <path-to-src>/ReleaseTwin.Cli -- examples/cases` with `RELEASETWIN_API_TOKEN`/`RELEASETWIN_API_URL` set in the child environment, returning exit code + stdout.
- [x] 2.2 Resolve the CLI project path correctly relative to `web/`'s own working directory (`../src/ReleaseTwin.Cli` or equivalent) — verify it resolves the same regardless of where `npm run e2e`/`cypress run` is invoked from.
- [x] 2.3 Set an explicit, generous timeout on this task (a first invocation includes an implicit `dotnet build`).

## 3. Product-usage-loop spec

- [x] 3.1 Add `web/cypress/e2e/product-usage-loop.cy.ts`: sign in via the existing `clerkSignIn` helper (reuses `E2E_TEST_USER_EMAIL`, matching the other specs), create a project, issue a token.
- [x] 3.2 Capture the org's usage-counter values from the dashboard before running anything.
- [x] 3.3 Call `cy.task('runCli', { token, apiUrl, casesDir: 'examples/cases' })`; assert a successful exit code.
- [x] 3.4 Reload `/dashboard`; assert the run history table shows the new case (`HTTP-DEMO-1`, matching the bundled zero-credential example) and the case-report usage counter incremented by exactly 1 from the captured baseline.

## 4. Verification

- [x] 4.1 Run `registration.cy.ts` alone against a real Clerk instance; confirm it passes on a genuinely fresh address (first run) and confirm the design.md-anticipated "already registered" branch is actually reachable/correct on a second run.
- [x] 4.2 Run `product-usage-loop.cy.ts` alone; confirm the real CLI invocation succeeds and the dashboard reflects it.
- [x] 4.3 Run the full `web/` e2e suite (`npm run e2e`) with all specs together; confirm nothing in the new specs interferes with `dashboard-walkthrough.cy.ts`.
