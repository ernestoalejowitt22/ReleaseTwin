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

Add `RELEASETWIN_EVIDENCE=on` to capture a screenshot after every `ui.*` step, redacted in your
CLI before it goes anywhere. Where it ends up depends on what else you configure — set either or
both:

- `RELEASETWIN_EVIDENCE_DIR=<path>` — writes each case's redacted evidence document and
  screenshots to `<path>/<case-id>/`, fully locally, no account or network access required.
- `RELEASETWIN_API_TOKEN` (on a Paid-tier project) — uploads the same redacted evidence to your
  hosted dashboard instead of (or alongside) the local directory.

## The failure demo case

`cases/example-ui-journey-demo-failure.yaml` is an **intentional, permanent failure** —
not a flaky test, not a bug to fix. It runs the same real login as
`example-ui-journey.yaml` above, then asserts the post-login message equals text the
page never shows. It exists purely to produce a real `FAIL` line and a real attached
screenshot for [docs/ci.md](../../docs/ci.md)'s "What a failure looks like" section — see
that doc for the captured evidence. It is not part of this repo's own CI gate
(`.github/workflows/pr-annotations.yml` only scans `examples/cases-http-only`).

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
