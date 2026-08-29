## 1. Browser context per run

- [x] 1.1 `UiOperationSupport` — create a run-scoped `IBrowserContext` on first use (`AdapterState["ui.context"]`), open the page from it; keep the `AdapterState["ui.page"]` contract unchanged
- [x] 1.2 `ClosePageCleanup` (`ui.closePage`) — close the context (closing its pages) and clear both `ui.context` and `ui.page`
- [x] 1.3 `ReleaseTwin.Adapters.Ui.Tests` — existing navigate/click/fill/wait/assert tests still pass against the shared context; add a test that two ops in one run share the context

## 2. `ui.setCookie` operation

- [x] 2.1 `SetCookieOperation : UiOperationBase` — params `name`, `value`, one of `url` | (`domain` [+ `path`]); optional `secure`, `httpOnly`, `sameSite`, `expires`; maps to a Playwright `Cookie` and calls `context.AddCookiesAsync`
- [x] 2.2 Validation: missing `name`/`value`, neither scope, both scopes, or a non-absolute `url` → `OperationResult.Fail` with a message naming the problem
- [x] 2.3 Register `ui.setCookie` in `UiAdapter.Register` and add it to `UiAdapter.KnownOperationCapabilities` (`browser:chromium`)
- [x] 2.4 Tests: a seeded cookie is visible to a later `ui.navigate` against a local static server that echoes `document.cookie`; each malformed declaration fails the step and still runs cleanup

## 3. Redact typed UI values (`evidence-capture` delta)

- [x] 3.1 `EvidenceRedactor` — thread `StepEvidence.OperationName` into `RedactAdapterEvidence`; when it starts with `ui.`, add `value` to that step's field-drop set (same path as a per-case `field:` rule, so the allowlist can still re-include it)
- [x] 3.2 `UiOperationBase.RecordAsync` (fill steps) — after typing, query the target element's `type`; set `UiStepEvidence.ValueIsProtected = true` for a password field
- [x] 3.3 Redactor — a protected value is masked even against an allowlist entry (treated like the built-in `Authorization` header)
- [x] 3.4 Tests (`ReleaseTwin.Cli.Tests` / `ReleaseTwin.Adapters.Ui.Tests`): a `ui.fill` literal value is absent from redacted evidence with no per-case rule; a password-field value is masked; a non-password `value` is re-included by an allowlist entry; capture-off still produces no evidence

## 4. Cypress task plumbing

- [x] 4.1 `web/cypress.config.ts` — `runCliJourney` gains an optional `env` passthrough (mirror `runCli`'s existing one)
- [x] 4.2 `fetchNahaAdminUiTarget` task — read `adminUiBaseUrl` + `roleCookieName`/`roleCookieValue` (and reuse the existing API secret) from Secrets Manager; fail with a clear setup message if the keys are absent
- [x] 4.3 (was blocked) — `releasetwin/e2e/naha-account` does not exist in the accessible account, and creating it needs the NAHA `e2e-admin` Vercel alias URL, the `E2E_AUTH_SECRET`, and an admin email (none available to this session). secret populated by the user with all 6 keys; `adminUiBaseUrl` corrected to the `e2e-admin` git-branch alias (a hash URL was production-mode).

## 5. Cypress spec A — generic UI journey + visual evidence

- [x] 5.1 `web/cypress/e2e/ui-journey-visual-evidence.cy.ts` — sign in, Paid-tier project, enable evidence capture, build a `the-internet.herokuapp.com` login → assert-visible → HTTP-leg journey in the real builder, save version
- [x] 5.2 Run it via `runCliJourney` with `env: { RELEASETWIN_UI_ENABLED: "1", RELEASETWIN_EVIDENCE: "on" }`; assert `PASS <caseId>` and no upload/evidence warnings
- [x] 5.3 Open the dashboard evidence detail page for that report; assert ordered `ui.*` steps render, at least one screenshot renders, provenance line present
- [x] 5.4 Assert the login form's password literal does **not** appear anywhere in the rendered evidence; `cy.screenshot()` checkpoints under `ui-journey-visual-evidence/`
- [x] 5.5 `web/package.json` — `e2e:run:ui-journey` + `e2e:ui-journey` scripts

## 6. Cypress spec B — NAHA admin UI journey

- [x] 6.1 `web/cypress/e2e/naha-admin-ui-journey.cy.ts` — from `fetchNahaAdminUiTarget` + the NAHA API secret: build a journey `ui.setCookie naha_e2e_role=admin` → `ui.navigate` admin home → `ui.assertVisible [data-testid="admin-home"]` → `ui.navigate` `/companies` (or `/policies` if visible) → a UI assertion → an HTTP leg to `{nahaApi}/api/me` with the `/v1/e2e/login` Bearer → `http.assertJsonPath $.principal.role == admin`; cleanup `ui.closePage`
- [x] 6.2 `naha-admin-ui-journey.cy.ts` **passes** — cookie-gated NAHA admin UI + API bridge, `$.principal.role == admin`
- [x] 6.3 NAHA admin screenshots render on the dashboard evidence page, labelled best-effort-redacted
- [x] 6.4 `web/package.json` — `e2e:run:naha-ui` + `e2e:naha-ui` scripts

## 7. Docs

- [x] 7.1 `docs/customer-pilot-guide.md` — add the UI-journey-with-visual-evidence flow as a pilot talking point
- [x] 7.2 `docs/installation-model.md` — update the Playwright / "external-check connector" note now that a real UI journey with visual evidence exists and is tested
- [x] 7.3 `examples/cases-ui-journey/` — add a `ui.setCookie` example (commented, pointing at a gated demo) or a short README note

## 8. Validation

- [x] 8.1 `openspec validate ui-journey-visual-evidence --strict` passes
- [x] 8.2 Full .NET solution build + all test projects green (177 tests)
- [x] 8.3 `npm run e2e:ui-journey` and `npm run e2e:naha-ui` both pass locally
- [x] 8.4 Screenshots present under `web/cypress/screenshots/ui-journey-visual-evidence/` (3) and `.../naha-admin-ui-journey/` (2) and `.../naha-admin-ui-journey/`
