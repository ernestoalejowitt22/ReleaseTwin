## Why

The UI adapter runs on Playwright/Chromium, which auto-waits for selectors, so a
React or Angular single-page app is already drivable today — but "works" is not
"a front-end team would adopt it". There is no SPA example to clone, `ui.assertVisible`
can only check that an element is present (not what it says), and `README.md` still
lists browser/visual evidence as "not wired into the pipeline" while the code
records screenshots and video on the e2e path. Planning for React/Angular is a
documentation + examples job plus one or two small adapter operations — it stays
inside the core/adapter boundary and needs no new adapter.

## What Changes

- Add a `ui.assertText` operation: assert an element's text content equals or
  contains an expected string, with `${VAR}` / `{{capture}}` substitution, failing
  and classifying like any other operation. `ui.assertVisible` stays as-is.
- Add a "wait for SPA navigation" affordance: extend `ui.waitFor` with a `url`
  match mode (glob/substring against `page.url()`) so a case can wait on a
  client-side route change, not only a selector appearing.
- Add `examples/react-demo/` and `examples/angular-demo/` — two minimal bundled
  SPAs (one screen, one client-side route change, one rendered value) that the
  same journey case shape can drive, so the funnel names both frameworks.
- Add `examples/cases-spa/` — a UI-journey case per demo, sharing one case shape,
  showing route change → `ui.assertText` → value capture → API leg (the API leg
  can reuse the `express-demo` from the sibling change, or `httpbin`).
- Reconcile visual-evidence docs: update `README.md` "What's not built yet" and
  `docs/flag-proof.md` / UI docs to state accurately what screenshot + `.webm`
  video capture does today (`RELEASETWIN_EVIDENCE=on`, Paid-tier upload) vs. what
  is still deferred (external-check / Playwright connector).
- `docs/spa-testing.md` — "Testing a React or Angular app": auth cookie bypass,
  waiting for hydration/routes, asserting rendered text, bridging into API legs.

## Capabilities

### New Capabilities
_None._

### Modified Capabilities
- `ui-adapter`: adds a requirement that a case can assert an element's **text
  content** (not just its visibility) as a pipeline step, and that `ui.waitFor`
  can wait on a **URL/route change** in addition to a selector state. Both
  participate in the existing ordered-pipeline, capture, failure-classification,
  and cleanup guarantees.

## Impact

- `src/ReleaseTwin.Adapters.Ui/UiOperations.cs` — new `AssertTextOperation`;
  `WaitForOperation` gains a `url` branch. `UiOperationSupport` unchanged.
- `tests/ReleaseTwin.Adapters.Ui.Tests/` (or the CLI UI tests) — new offline tests
  for text assertion and URL wait.
- New: `examples/cases-spa/`, `docs/spa-testing.md`; edits to `README.md`,
  `docs/flag-proof.md`, `openspec/specs/ui-adapter/spec.md` (via delta).
- New opt-in CI job: builds both demo SPAs (Vite/React, Angular CLI), serves the
  built static output, runs the UI cases with `RELEASETWIN_UI_ENABLED=1` +
  Playwright browsers. First front-end toolchain in the repo — confined to this
  job and `examples/*-demo/`.
- No change to `ReleaseTwin.Core` or the adapter SDK — the invariant holds.
