## 1. Dependencies and config

- [x] 1.1 Add `cypress`, `@clerk/testing`, `@clerk/backend`, and `start-server-and-test` as dev dependencies in `web/`.
- [x] 1.2 Add `web/cypress.config.ts` with `clerkSetup()` wired into `setupNodeEvents`, `baseUrl: "http://localhost:3000"`, and the `ensureE2ETestUser` task (design.md).
- [x] 1.3 Add `web/cypress/support/e2e.ts` with `addClerkCommands({ Cypress, cy })`.
- [x] 1.4 Resolve the `CLERK_PUBLISHABLE_KEY` vs `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` naming bridge (design.md) — verify against `clerkSetup()`'s actual typed options.

## 2. Test user provisioning

- [x] 2.1 Implement the `ensureE2ETestUser` Cypress task using `@clerk/backend`'s `createClerkClient`, looking up by email before creating (idempotent).
- [x] 2.2 Document `E2E_TEST_USER_EMAIL`/`E2E_TEST_USER_PASSWORD` as required env vars (a `cypress.env.json.example` or README section, not committed with real values).
- [x] 2.3 Confirm the manual prerequisite (password sign-in enabled on the Clerk instance) is documented, not silently assumed.

## 3. First spec

- [x] 3.1 `web/cypress/e2e/dashboard-walkthrough.cy.ts`: `before()` calls `ensureE2ETestUser`; `setupClerkTestingToken()`; `cy.clerkSignIn({ strategy: "email_code", identifier })` (revised from the originally-planned password strategy — see design.md, Device Trust has no bypass); visit `/dashboard`.
- [x] 3.2 Assert the dashboard renders (projects section visible).
- [x] 3.3 Create a project via the UI form; assert it appears in the projects list and gets selected.
- [x] 3.4 Issue a token via the UI button; assert the "shown once" banner renders with a token-shaped value.
- [x] 3.5 `cy.clerkSignOut()`; assert redirected away from `/dashboard`.

## 4. Runner wiring

- [x] 4.1 Add an `e2e` script to `web/package.json` using `start-server-and-test` to boot the hosted API (in-memory DB, real `Clerk__Domain`, no `Database__SqlitePath`) and `next dev`, then run `cypress run`.
- [x] 4.2 Confirm `npm run e2e` passes locally end to end, against the real Clerk instance already configured this session.

## 5. Docs

- [x] 5.1 README.md: document `npm run e2e`, its prerequisites (password sign-in enabled, `E2E_TEST_USER_*` env vars, real Clerk credentials already required for `web/` generally), and that it's local-only for now (no CI wiring yet — a separate, real gap, not silently implied as solved).
