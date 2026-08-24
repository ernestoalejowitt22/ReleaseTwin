## Why

The new Cypress e2e suite (`web-cypress-e2e`) produces no durable trace of what a passing run actually saw beyond Cypress's own failure-only screenshots and run video. There's also a gap in `.gitignore`: `cypress/downloads` isn't excluded, so any test exercising a download would leave untracked files showing up in `git status`. Both are small gaps worth closing before the suite grows more specs.

## What Changes

- Add `/cypress/downloads` to `web/.gitignore`, alongside the existing `/cypress/videos` and `/cypress/screenshots` entries.
- Add `cy.screenshot()` calls to `dashboard-walkthrough.cy.ts` at the four natural checkpoints already present in the walkthrough (signed in, project created, token issued, signed out), namespaced under a `dashboard-walkthrough/` subfolder so they're distinguishable from Cypress's own failure-only captures within the already-gitignored `cypress/screenshots/` folder.
- No new capture mechanism beyond `cy.screenshot()` — no network/HAR logging, no custom `cy.task`/`after:screenshot` plumbing, no new top-level gitignore entry for this (Cypress has no per-call way to route screenshots to an arbitrary folder, so a separate `cypress/evidence/` top-level folder was dropped in favor of a subfolder — see design.md).

## Capabilities

### New Capabilities

(none — this is test-infrastructure/tooling only, no product-observable behavior changes)

### Modified Capabilities

(none)

## Impact

- `web/.gitignore`: two new ignore entries.
- `web/cypress/e2e/dashboard-walkthrough.cy.ts`: four `cy.screenshot()` calls added at existing checkpoints, writing into `cypress/screenshots/dashboard-walkthrough/`.
- No production code, API, or spec-level behavior affected.
