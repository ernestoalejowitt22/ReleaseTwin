## Context

See proposal.md - Why. `web/cypress.config.ts` currently has no `screenshotsFolder` override; Cypress's defaults (`cypress/screenshots` for failures, `cypress/videos` for run video, `cypress/downloads` for downloads) apply. Only the first two are gitignored today.

## Goals / Non-Goals

**Goals:**
- Close the `cypress/downloads` gitignore gap.
- Give `dashboard-walkthrough.cy.ts` a deliberate, gitignored evidence trail separate from Cypress's failure-only captures.

**Non-Goals:**
- No network/HAR capture, no custom `cy.task` evidence plumbing, no reusable `cy.evidence()` command — out of scope per the resolved proposal scope.
- No change to CI configuration or artifact retention (this change only affects local/CI-produced files on disk, not what CI does with them).

## Decisions

- **Subfolder under `cypress/screenshots/`, not a separate top-level `cypress/evidence/` folder**: Cypress's `cy.screenshot()` API has no per-call way to redirect output to an arbitrary folder — the output root is always the single global `screenshotsFolder` config value, shared with failure-triggered captures. Getting a genuinely separate `cypress/evidence/` folder would require an `after:screenshot` event handler in `cypress.config.ts` to move files post-capture — real Node plumbing, which this change's non-goals explicitly rule out. Instead, `cy.screenshot("dashboard-walkthrough/<step-name>")` namespaces deliberate captures into a subfolder (`cypress/screenshots/dashboard-walkthrough/`), distinguishing them from Cypress's own failure screenshots (which use auto-generated spec/test names at the folder root) without any new code. Alternative considered and rejected: the `after:screenshot`-based real folder split, deferred as a possible future change if the subfolder convention proves insufficient.
- **Four checkpoints, matching existing test structure**: signed in, project created, token issued, signed out — these are the assertions already present in the spec, so no new waiting/assertion logic is needed, just a screenshot call alongside each existing assertion.
- **No new `.gitignore` entry needed for evidence**: since captures land under the already-ignored `/cypress/screenshots`, only the pre-existing `cypress/downloads` gap needs closing.

## Risks / Trade-offs

- [Evidence screenshots taken at the wrong moment (e.g., before content settles) produce unhelpful blank/loading captures] → Place each `cy.screenshot()` call immediately after the existing `cy.contains(...).should("be.visible")` assertion for that checkpoint, so Cypress's built-in retry-until-visible already guarantees the right state before the capture.
- [Evidence folder grows unbounded across local runs since nothing prunes old screenshots] → Acceptable for now: the folder is gitignored and local-only; pruning is out of scope for this change.
