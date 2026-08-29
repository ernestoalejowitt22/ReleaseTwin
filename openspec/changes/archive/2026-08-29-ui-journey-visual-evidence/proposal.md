## Why

`dashboard-evidence-viewer` shipped visual evidence — the UI adapter takes a screenshot after every `ui.*` step and the CLI uploads redacted screenshots that render on the dashboard — but nothing exercises that path end to end, and two gaps only surface when you actually try to run a real UI journey with evidence on:

1. **`ui.fill` values leak into the evidence action log in cleartext.** `UiOperationBase.RecordAsync` records every step parameter verbatim; the redactor's built-in denylist matches property *names* (`password|secret|token|…`), and the key here is `"value"`, which is never matched. A literal credential typed by a `ui.fill` step ends up in the uploaded evidence unless the author hand-writes a `redact:` rule.

2. **The UI adapter can't authenticate a browser session.** It opens a fresh, cookieless Playwright context. Any real customer app behind an auth gate is unreachable — including NAHA's own admin app, whose E2E deploy gates purely on a `naha_e2e_role` cookie.

NAHA's `naha-admin` app turns out to be an ideal real-world target: a deployed Next.js app with a dedicated `e2e-admin` Vercel alias, cookie-based E2E auth (no Clerk sign-in), real pages (`/companies`, `/policies`), and LaunchDarkly-gated feature visibility — so a ReleaseTwin journey can drive its UI *and* flag-proof its feature flags in one case.

## What Changes

- **New `ui.setCookie` operation** in `ReleaseTwin.Adapters.Ui`: sets a cookie on the run's Playwright browser context before navigation, so a journey can authenticate a gated app from case-file data alone (`name`, `value`, `domain`/`url`, optional `path`, `secure`, `httpOnly`, `sameSite`). Adds to `ui-adapter`'s operation set and `KnownOperationCapabilities` (capability `browser:chromium`).
- **`ui.fill` (and any `ui.*` step) redacts its typed `value` by default.** The UI evidence recorder SHALL mask the `value` parameter of a fill-style step in the recorded action log, and SHOULD mask a value typed into an input the page reports as `type="password"`. A per-case allowlist entry can opt a specific field back in. This is a modification to `evidence-capture`'s redaction guarantees.
- **New Cypress e2e specs** (test-only, no product code):
  - `ui-journey-visual-evidence.cy.ts` — composes a UI-then-API journey against `the-internet.herokuapp.com` in the real builder, saves it, runs it via the CLI with `RELEASETWIN_UI_ENABLED=1 RELEASETWIN_EVIDENCE=on`, and asserts the redacted screenshots render on the dashboard evidence detail page (and that the login form's password value is **not** present in the evidence).
  - `naha-admin-ui-journey.cy.ts` — same shape against NAHA's `e2e-admin` Vercel alias: `ui.setCookie naha_e2e_role=admin` → `ui.navigate` admin home → navigate `/policies` → a UI assertion → an API leg using the `/v1/e2e/login` Bearer → assert. Optionally declares `flag_proof` against `naha.policy-ui`.
- **NAHA E2E config for the suite**: the `e2e-admin` alias base URL, the cookie name/value, and (reusing the existing `releasetwin/e2e/naha-account` Secrets Manager entry) the API secret — surfaced through a new `fetchNahaAdminUiTarget` Cypress task or an extension of `fetchNahaTestAccount`.
- **Docs**: `customer-pilot-guide.md` gains the UI-journey-with-visual-evidence flow as a pilot talking point; `installation-model.md`'s Playwright/"external-check connector" note is updated now that a real UI journey with evidence exists.

## Capabilities

### New Capabilities

(none — `ui.setCookie` is a new operation within the existing `ui-adapter` capability, and the Cypress specs are test infrastructure)

### Modified Capabilities

- `ui-adapter`: a case's pipeline MAY include a step that seeds a browser cookie before navigation, so a journey can drive an authenticated, gated app; it participates in the same ordered pipeline, failure classification, and cleanup as any other `ui.*` step.
- `evidence-capture`: the CLI-side redaction of UI evidence SHALL mask the typed `value` of a fill-style `ui.*` step in the recorded action log by default (never uploaded verbatim), overridable per case via the existing allowlist; a value typed into a `type="password"` input SHALL be masked regardless.

## Impact

- **`ReleaseTwin.Adapters.Ui`**: new `SetCookieOperation` + registration in `UiAdapter`; `UiAdapter.CreateAsync` / page creation may need a shared `IBrowserContext` so a cookie set on one step is visible to a later `ui.navigate` (today each op calls `browser.NewPageAsync()` — a context-per-run change, small but touches `UiOperationSupport`). `UiOperationBase.RecordAsync` masks `value`.
- **`ReleaseTwin.Cli`**: `EvidenceRedactor` — no change if masking happens in the recorder; a fallback field-name rule (`value` on a `ui.*` step) is the alternative.
- **Core**: none expected (`ui.setCookie` is an ordinary `IOperation`).
- **Tests**: `ReleaseTwin.Adapters.Ui.Tests` (new op, redaction), `web/cypress/e2e/` (2 new specs), `web/cypress.config.ts` (NAHA UI target task), `web/package.json` (e2e scripts).
- **Specs**: 0 new, 2 modified (`ui-adapter`, `evidence-capture`).
- **External**: depends on NAHA's `e2e-admin` Vercel alias being live and its `naha_e2e_role` cookie contract staying stable; the API secret is already provisioned.
