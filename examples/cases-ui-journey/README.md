# UI journey example

`cases/example-ui-journey.yaml` drives a real browser (Playwright/Chromium) as one leg of a
journey, then bridges a UI-observed value into API legs — the "log in through the UI, verify
through the API" pattern as one case.

For a SPA-focused walkthrough — waiting on a client-side route, `ui.assertText`, cookie
auth-bypass — see [docs/spa-testing.md](../../docs/spa-testing.md) and
[examples/cases-spa/](../cases-spa/).

## Running it

The UI adapter is opt-in (launching a browser is expensive and needs browser binaries):

```bash
RELEASETWIN_UI_ENABLED=1 dotnet run --project src/ReleaseTwin.Cli -- examples/cases-ui-journey/cases
```

Add `RELEASETWIN_EVIDENCE=on` (with an API token, on a Paid-tier project) to capture a screenshot
after every `ui.*` step — redacted in your CLI, then rendered on the dashboard as visual evidence.

## Testing a gated app: `ui.setCookie`

Most real apps sit behind an auth gate. If the target has a cookie-based bypass for automated
testing (an `NEXT_PUBLIC_E2E_AUTH`-style mode, a role cookie, a locale cookie), seed it before the
first `ui.navigate` — the cookie is set on the run's browser context and visible to every later
step:

```yaml
pipeline:
  - operation: ui.setCookie
    with:
      name: my_app_e2e_role       # the cookie the target's middleware checks
      value: admin
      url: https://staging.example.com   # or: domain + path
      # optional: secure, httpOnly, sameSite (Strict|Lax|None), expires (unix seconds)
  - operation: ui.navigate
    with:
      url: https://staging.example.com/admin
  - operation: ui.assertVisible
    with:
      selector: '[data-testid="admin-home"]'
```

For Clerk / OAuth-style login with no cookie bypass, drive the real sign-in form with
`ui.fill` / `ui.click` instead — a value typed into a `type="password"` field is masked from the
uploaded evidence automatically.
