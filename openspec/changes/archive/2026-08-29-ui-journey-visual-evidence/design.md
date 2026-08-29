## Context

See proposal.md — *Why*. Current state that shapes the approach:

- `ReleaseTwin.Adapters.Ui`: five `ui.*` operations, all extending `UiOperationBase` (added by `dashboard-evidence-viewer`). Each op resolves its page via `UiOperationSupport.GetOrCreatePageAsync`, which calls **`browser.NewPageAsync()`** — a fresh page on the browser's *default* context, with no cookie/storage isolation and no seam to inject cookies. `ui.closePage` closes that page.
- `UiOperationBase.RecordAsync` (evidence): after each step, copies **every** parameter into a `UiStepEvidence.Parameters` dict verbatim, then screenshots to a temp PNG. `ui.fill`'s `value` goes in unmodified.
- CLI `EvidenceRedactor.RedactSteps` iterates `StepEvidence` (which carries `OperationName`) and calls `RedactAdapterEvidence(step.AdapterEvidence, rules, …)` — it does **not** currently pass the operation name down. Built-in denylist matches property *names* against `authorization|cookie|credential|password|secret|token|api[_-]?key|bearer`; `"value"` and `"selector"` don't match.
- The `evidence:` case-file block already parses an allowlist (`capture: [...]`) and denylist, shared by local cases and hosted journeys.
- Cypress: `web/cypress.config.ts` has a `fetchNahaTestAccount` task reading `releasetwin/e2e/naha-account` from Secrets Manager (`e2eAuthSecret`, `apiBaseUrl`, `adminEmail`) and a `runCliJourney` task that shells `dotnet run -- --journey <id>@<v>` with `RELEASETWIN_API_TOKEN`/`RELEASETWIN_API_URL`. No task sets `RELEASETWIN_UI_ENABLED` or `RELEASETWIN_EVIDENCE`.
- NAHA `naha-admin`: real Next.js app; dedicated `e2e-admin` git branch → stable Vercel alias; `NEXT_PUBLIC_E2E_AUTH=true` makes its middleware gate purely on a `naha_e2e_role` cookie (`admin` → in, `member` → `/forbidden`, none → `/sign-in`). Real pages: `/` (`[data-testid="admin-home"]`), `/companies`, `/policies` (both LD-visibility-gated, soft — API is the auth source of truth). `naha.client.site` / `naha-internal-admin` are scaffolds.

## Goals / Non-Goals

**Goals**

- One new UI operation (`ui.setCookie`) lets a journey reach a cookie-gated app.
- A `ui.fill` value never reaches uploaded evidence verbatim, with no per-case rule.
- Two real end-to-end Cypress specs proving visual evidence: one hermetic-ish (public login form), one against NAHA's real admin app.
- Screenshots become genuinely demonstrable evidence, not just wired-up-but-untested.

**Non-Goals**

- Flag-proof against NAHA's real LD flags (`naha.policy-ui` etc.) — that needs NAHA's own LaunchDarkly project credentials, out of scope. UI + API legs only.
- A builder affordance for `ui.setCookie` params or screenshot region masks — the builder's free-text operation + generic key/value editor already covers it; a dedicated UI is a later change.
- OCR / selector-aware pixel redaction of screenshots — still region-only, still best-effort.
- Testing `naha.client.site` / `naha-internal-admin` — they don't exist yet.

## Decisions

### D1: One browser context per case run

`UiOperationSupport` switches from `browser.NewPageAsync()` to a run-scoped `IBrowserContext`: on first use, `browser.NewContextAsync()` stashed on `AdapterState["ui.context"]`, then `context.NewPageAsync()` stashed as today on `AdapterState["ui.page"]`. `ui.closePage` closes the context (which closes its pages). `ui.setCookie` calls `context.AddCookiesAsync(...)` on that shared context, so a cookie set in step 1 is present for a `ui.navigate` in step 3.

Alternative: pre-seed cookies at context creation from a journey-level parameter. Rejected — it's not a pipeline step, breaks the "everything is an ordered step" model, and can't express "set a cookie derived from an earlier capture."

### D2: `ui.setCookie` operation shape

Parameters: `name` (required), `value` (required), and exactly one scope — `url` (absolute) **or** `domain` (+ optional `path`, default `/`). Optional: `secure`, `httpOnly`, `sameSite` (`Strict|Lax|None`), `expires` (unix seconds). Maps directly to Playwright's `Cookie`. Missing/both-scope/relative-url → `OperationResult.Fail` with a naming message (spec: "A malformed or unsupported cookie scope is a clear failure"). Capability: `browser:chromium` (same as every `ui.*`), added to `UiAdapter.KnownOperationCapabilities` so `graceful-capability-gating` protects a case that forgets `requires:`.

### D3: Redact a UI step's typed value in the CLI redactor, keyed off the operation name

`EvidenceRedactor.RedactSteps` already has `step.OperationName`. Thread it into `RedactAdapterEvidence`; when it starts with `ui.`, add `value` to the per-step field-drop set (same machinery as a per-case `field:` denylist rule). This keeps:
- **allowlist parity** — a case can still `capture: [$.value]` to opt a specific non-sensitive field back in, via the existing `RestoreAllowlisted` path;
- **the fail-closed / built-in-wins semantics** — `value` is treated exactly like a built-in credential-named field.

Password-field detection (`type="password"` → mask even against an allowlist) is done in the **recorder**: `UiOperationBase` (for `ui.fill`) queries the target element's `type` after typing and sets `UiStepEvidence.ValueIsProtected = true`; the redactor treats a protected value like the built-in `Authorization` header — never re-includable.

Alternatives considered:
- *Mask unconditionally in the recorder.* Simpler, but the raw value is gone before the CLI runs, so the allowlist can never re-include it and the spec's "opt back in" scenario is unmeetable. Also splits redaction logic across the adapter and the CLI, against `evidence-capture`'s "all redaction in the CLI" requirement.
- *A new built-in name pattern for `value`.* Too broad — an HTTP body legitimately named `value` would be masked.

### D4: NAHA E2E target comes from Secrets Manager, same as the existing NAHA task

Extend `releasetwin/e2e/naha-account` (or add `releasetwin/e2e/naha-admin-ui`) with `adminUiBaseUrl` (the `e2e-admin` Vercel alias) and `roleCookieName` / `roleCookieValue` (`naha_e2e_role` / `admin`). A `fetchNahaAdminUiTarget` Cypress task returns them. The API base URL and `x-e2e-secret` for the journey's API leg are already in that secret.

### D5: The Cypress specs build the journey in the real builder, then run it via the CLI

Mirrors `naha-real-journey.cy.ts` exactly — compose steps through `[data-testid="step-N"]` inputs, save a version, then `cy.task("runCliJourney", …)`. The `runCliJourney` (and `runCli`) task gains an optional `env` passthrough (like `runCli` already has) so the spec can set `RELEASETWIN_UI_ENABLED=1` and `RELEASETWIN_EVIDENCE=on`. After the run, the spec visits the dashboard evidence detail page and asserts the screenshots render and the login password is absent.

## Risks / Trade-offs

- **NAHA `e2e-admin` alias must be live and stable.** → The spec fetches the URL from Secrets Manager, not hardcoded; if the secret key is absent the task fails loudly with a setup message. If the alias is down the spec fails like any other real-target spec (`launchdarkly-real-flag-proof`, `naha-real-journey` already carry this risk).
- **Playwright browsers in CI.** The e2e suite has never run a UI case. `dotnet run` of a UI journey needs `playwright install chromium` to have happened on the runner. → Document it; the `UiAdapter.CreateAsync` failure path already prints a clear "browser launch failed" line and the CLI exits 1, so a missing-browser run fails visibly rather than silently.
- **Context-per-run is a behavior change for existing UI cases.** Cookies/storage now isolated per run instead of shared on the browser default context. → This is strictly more correct (test isolation) and no existing case or test relies on cross-run cookie bleed; `ReleaseTwin.Adapters.Ui.Tests` will confirm.
- **Screenshot of a real third-party page (`the-internet.herokuapp.com`, NAHA)** can be large. → Existing ingest caps (256 KB doc, 20 shots, 2 MB each) apply; a 6-step journey is well under. The per-body 32 KB truncation doesn't apply to images.
- **`ValueIsProtected` via a post-type DOM query adds a round-trip per `ui.fill`.** → Negligible (one `evaluate`), and only when evidence capture is on.

## Migration Plan

1. `UiOperationSupport` context-per-run + `ui.closePage` closes context (no observable change for non-cookie cases; UI adapter tests stay green).
2. `SetCookieOperation` + registration + `KnownOperationCapabilities`.
3. Redactor: thread `OperationName`, drop `value` for `ui.*`; recorder sets `ValueIsProtected` for password fields.
4. `runCli`/`runCliJourney` `env` passthrough; `fetchNahaAdminUiTarget` task.
5. Two Cypress specs + `web/package.json` scripts (`e2e:run:ui-journey`, `e2e:run:naha-ui`).
6. Docs.

Rollback: `ui.setCookie` is additive; the redaction change is a stricter default (safe to keep); the context change can revert to `NewPageAsync()` if an unforeseen isolation issue appears. Cypress specs are independent.

## Open Questions

- Exact `e2e-admin` Vercel alias hostname and whether `releasetwin/e2e/naha-account` already carries a UI base URL — confirm with the secret's current contents before writing the NAHA spec; if absent, adding the keys is a one-time ops step, not a code change.
- Whether NAHA's `/policies` is visible on the `e2e-admin` deploy (needs `NEXT_PUBLIC_E2E_POLICY_UI=true` baked in) — if not, the NAHA spec asserts on `/` and `/companies` instead. Does not change any spec or task here.
