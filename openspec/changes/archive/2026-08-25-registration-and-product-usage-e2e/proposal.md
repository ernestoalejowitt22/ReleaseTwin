## Why

Everything built this session (`token-onboarding`, `usage-metering`, `plan-tier-gating`) is proven at the unit/HTTP level and was manually verified live once, but two real gaps remain in what's actually exercised as repeatable test coverage:

1. **No test has ever driven real registration.** The existing `dashboard-walkthrough.cy.ts` pre-creates its Clerk user via the backend admin API (`ensureE2ETestUser`) and signs into it — it never touches Clerk's actual sign-up UI. A brand-new visitor's first-time experience (landing page → sign-up form → first-time org/user provisioning) has never been exercised end to end.
2. **No test has ever closed the loop to real product usage.** Every report/usage-count assertion so far is seeded server-side (`ProvisioningService` calls, ingest test helpers) — nothing has ever actually run the CLI against a dashboard-issued token and confirmed the result shows up back in the dashboard, the way a real customer's first real use of the product would.

This is proof-of-correctness work, not new product behavior — it adds test coverage for behavior that already exists and is already specified elsewhere (`account-provisioning`, `cli-runner`, `ingest-api`, `dashboard`). No requirements change.

## What Changes

- Add `web/cypress/e2e/registration.cy.ts`: drives Clerk's real sign-up form (not the backend admin-API shortcut) with a disposable `+clerk_test@` address, confirming first-time provisioning actually happens through the UI a real prospect would use. One run against one disposable address — not a fresh identity generated per run.
- Add `web/cypress/e2e/product-usage-loop.cy.ts`: reuses the existing sign-in helper, creates a project, issues a token, shells out to actually run `dotnet run --project src/ReleaseTwin.Cli -- examples/cases` against that token, then reloads the dashboard and asserts the run history and usage counter reflect the real upload.
- Kept as two separate specs (not merged into `dashboard-walkthrough.cy.ts`): they test different concerns — auth/provisioning vs. the CLI integration loop — and combining them would make one long, more fragile test out of two independently useful ones.

## Capabilities

(none — this adds test coverage for existing, already-specified behavior; no requirements change. `skip_specs: true` set in `.openspec.yaml`.)

## Impact

- New: `web/cypress/e2e/registration.cy.ts`.
- New: `web/cypress/e2e/product-usage-loop.cy.ts`, plus whatever small helper is needed to shell out to the CLI from a Cypress task (Cypress specs run in the browser; invoking a local process needs a `cy.task` in `cypress.config.ts`, similar in spirit to the existing `ensureE2ETestUser` task).
- Possibly: confirm whether `/sign-in`'s Clerk widget already handles first-time sign-up transparently, or whether a dedicated `/sign-up` route needs to exist first — this is investigated during design, not assumed.
- No changes to `ReleaseTwin.Core`, the CLI, or any hosted API behavior — this is test-only.
