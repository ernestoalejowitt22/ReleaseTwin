## 1. Gitignore gap-fill

- [x] 1.1 Add `/cypress/downloads` to `web/.gitignore`, alongside the existing `/cypress/videos` and `/cypress/screenshots` entries.

## 2. Evidence screenshots

- [x] 2.1 Add four `cy.screenshot("dashboard-walkthrough/<step-name>")` calls to `web/cypress/e2e/dashboard-walkthrough.cy.ts`, immediately after the existing assertions for: signed in, project created, token issued, signed out. (Namespaced under `cypress/screenshots/dashboard-walkthrough/` — no separate top-level folder; see design.md.)
- [x] 2.2 Run `npm run e2e` locally and confirm `web/cypress/screenshots/dashboard-walkthrough/` contains the four expected screenshots, and that `git status` shows no untracked files under `cypress/screenshots` or `cypress/downloads`.
