<!--
SPDX-FileCopyrightText: 2026 Ernesto Alejo and the ReleaseTwin contributors
SPDX-License-Identifier: AGPL-3.0-only WITH LicenseRef-ReleaseTwin-Adapter-Exception
-->

# Testing a React or Angular app

ReleaseTwin's `ui.*` adapter drives a real browser (Playwright/Chromium) as one
leg of a journey. This is **journey evidence, not component testing** — it does
not replace Vitest / Jest / Testing Library / Karma. Its job is the seam those
tools don't cover: *"a user did X in the UI, and the effect is visible through
the API"*, as one case, under the same fixture-integrity, capture, failure-
classification, and cleanup guarantees every other case has.

The framework doesn't matter — the adapter drives a rendered DOM, so React,
Angular, Vue, Svelte, and server-rendered pages are all the same to it.

## The operations

| step | what |
|---|---|
| `ui.navigate` | load a URL |
| `ui.click` / `ui.fill` | interact |
| `ui.waitFor` | wait for a `selector` state **or** a `url` match (below) |
| `ui.assertVisible` | assert an element is present |
| `ui.assertText` | assert an element's rendered text (`equals` or `contains`) |
| `ui.setCookie` | seed a cookie before navigation (auth bypass, locale, a flag) |
| `ui.closePage` | cleanup — closes the context, finalizes the video |

The adapter is opt-in — set `RELEASETWIN_UI_ENABLED=1`.

## Waiting for a client-side route

A SPA route change (`history.pushState`, a router `<Link>` / `routerLink`) fires
no page-load event, so waiting on a selector alone is racy. `ui.waitFor` takes a
`url` instead:

```yaml
- operation: ui.click
  with: { selector: '[data-testid="open-42"]' }
- operation: ui.waitFor
  with:
    url: '**/detail/*'        # substring, or a glob when it contains '*'
    timeoutMs: 5000
```

`url` matches as a **substring** unless it contains `*`, in which case `*` means
"any run of characters" (`**/detail/*` matches `https://app.example/detail/42`).
A `ui.waitFor` step takes a `selector` **or** a `url`, never both. On timeout the
failure names the pattern and the last URL seen.

## Asserting what rendered

`ui.assertVisible` only checks presence. `ui.assertText` checks the text, and its
expected value takes `${VAR}` (load time) and `{{capture}}` (per run)
substitution:

```yaml
- operation: ui.assertText
  with:
    selector: '[data-testid="detail-id"]'
    contains: "42"
  capture:
    - name: orderId
      from: text:[data-testid="detail-id"]
```

Add stable `data-testid` (or `data-test`) attributes in your components and
target those — not CSS classes or text that a redesign will move.

## Getting past an auth gate

Most real apps sit behind login. If the app has a cookie-based bypass for
automated testing (an `E2E`-mode role cookie, a locale cookie), seed it before
the first navigation:

```yaml
- operation: ui.setCookie
  with:
    name: demo_role          # the cookie your middleware checks
    value: admin
    url: ${SPA_BASE_URL}
- operation: ui.navigate
  with: { url: ${SPA_BASE_URL}/admin }
- operation: ui.assertText
  with: { selector: '[data-testid="admin"]', contains: "Admin area" }
```

All `ui.*` steps in a case share one browser context, so a cookie set in one
step is visible to every later step. For Clerk / OAuth logins with no cookie
bypass, drive the real form with `ui.fill` / `ui.click` — a value typed into a
`type="password"` field is masked from the uploaded evidence automatically.

## Bridging a UI value into an API leg

The point of the adapter — a value observed in the browser flows into a later
HTTP step by the same `capture` mechanism every adapter uses:

```yaml
  - operation: ui.assertText
    with: { selector: '[data-testid="detail-id"]', contains: "42" }
    capture:
      - name: orderId
        from: text:[data-testid="detail-id"]
  - operation: http.request
    with:
      method: GET
      url: ${API_BASE_URL}/get?order={{orderId}}
  - operation: http.assertJsonPath
    with: { path: $.args.order, expected: "{{orderId}}" }
```

## Evidence

Under `RELEASETWIN_EVIDENCE=on` (with an API token, on a Paid-tier project) the
adapter captures a screenshot after every `ui.*` step and records the whole
session to `<caseId>.webm`, finalized by `ui.closePage`. Screenshots are redacted
CLI-side before upload, then rendered on the dashboard. What is *not* built is an
**external-check connector** — folding a separately-run Playwright/Cypress suite's
results into a case.

## Running it

Reference cases:
[`examples/cases-spa/`](../examples/cases-spa/) — `react-journey.yaml`,
`angular-journey.yaml`, `admin-cookie.yaml`. They resolve `SPA_BASE_URL` and
`API_BASE_URL` at load time:

```bash
RELEASETWIN_UI_ENABLED=1 SPA_BASE_URL=http://localhost:4173 API_BASE_URL=https://postman-echo.com \
  dotnet run --project src/ReleaseTwin.Cli -- run examples/cases-spa
```

Runnable React and Angular demo apps, plus pipelines that run these cases on
Bitbucket Pipelines, Azure Pipelines, and GitHub Actions, are in the
[`releasetwin-ci-examples`](https://github.com/ernestoalejowitt22/releasetwin-ci-examples)
repo.
