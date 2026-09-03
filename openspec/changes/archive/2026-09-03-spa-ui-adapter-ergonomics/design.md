## Context

See [proposal.md](proposal.md) — Why, and [specs/ui-adapter/spec.md](specs/ui-adapter/spec.md)
for the two new requirements. Current state:

- `UiOperations.cs` holds `NavigateOperation`, `ClickOperation`, `FillOperation`,
  `WaitForOperation` (selector + `state` ∈ visible/hidden/attached/detached),
  `AssertVisibleOperation`, `SetCookieOperation`, `ClosePageOperation`. All run on
  Playwright/Chromium via `UiOperationSupport.GetOrCreatePageAsync`.
- The UI adapter is opt-in behind `RELEASETWIN_UI_ENABLED=1`; browser launch and
  Playwright browser binaries are required only when it is enabled.
- `README.md` "What's not built yet" says browser/visual evidence "isn't wired
  into the pipeline"; the code already records per-step screenshots
  (`RELEASETWIN_EVIDENCE=on`) and a `.webm` per case on `ui.closePage`.
- No front-end toolchain in the repo.

User decision (2026-09-03): ship **both** a bundled React demo and a bundled
Angular demo — the funnel should name both frameworks with something clonable.

## Goals / Non-Goals

**Goals:**

- `ui.assertText` and a URL-wait mode for `ui.waitFor`, both offline-testable.
- Two minimal bundled SPAs that the same case shape drives.
- `docs/spa-testing.md` naming React and Angular, covering auth-cookie bypass,
  waiting for a route, asserting rendered text, bridging into API legs.
- README / flag-proof docs state accurately what visual evidence does today.

**Non-Goals:**

- Component/unit testing of the SPAs (that is Vitest/Jest/Karma territory —
  `docs/spa-testing.md` says so explicitly).
- Shadow-DOM piercing config, `networkidle` waits, or ret--polling assertions —
  Playwright's defaults cover the demo; revisit only if a real case needs it.
- A framework version matrix. One pinned React major, one pinned Angular major.
- Visual-regression / screenshot diffing.
- Driving the SPAs in the same case as a flag-proof run (the SPA cases are
  journeys, not flag proofs).

## Decisions

### D1: `ui.assertText` is a new operation, not a mode of `ui.assertVisible`

`assertVisible` stays a presence-only check (some cases only care that something
rendered). A separate `AssertTextOperation` takes `selector`, one of
`equals` / `contains`, and `expected` (with `${VAR}` + `{{capture}}` applied by
the same substitution path `http` operations use). Reads text via Playwright
`Locator.InnerTextAsync` (trimmed). Alternative — overload `assertVisible` with an
optional `text` param — rejected: muddies a stable operation and its failure
messages.

### D2: URL wait is a branch inside `WaitForOperation`, mutually exclusive with `selector`

`ui.waitFor` gains an optional `url` param (substring or glob, matched against
`page.Url`). If both `selector` and `url` are present the step fails with a clear
message (spec scenario). Implemented with Playwright `Page.WaitForURLAsync`
(glob-aware), same `Timeout` handling as the selector branch. Alternative — a new
`ui.waitForUrl` operation — rejected: it is the same "wait for a condition"
concept and `waitFor` already dispatches on its params.

### D3: Two bundled demos — `examples/react-demo/` (Vite) and `examples/angular-demo/` (Angular CLI)

Both build to static assets; the CI job and local runs serve the built output
with any static file server (e.g. `npx serve`), so there is no dev-server
lifecycle to manage and no SSR. Each demo is one screen with:

- a client-side route (`/` → `/detail/:id`) so the URL-wait requirement has
  something real to synchronize on,
- one value rendered from a captured route param, asserted with `ui.assertText`,
- an optional cookie-gated view so `ui.setCookie` is exercised.

Alternatives considered and rejected: a public demo SPA (outside our control,
can change or vanish); a single hand-written vanilla SPA (doesn't read as
"React/Angular" for the funnel); Next.js/Nuxt (drags in SSR + a Node server at
serve time). Cost accepted: two front-end toolchains, confined to their dirs and
one CI job.

### D4: API leg reuses `express-demo` when present, falls back to `httpbin`

The SPA journey's API leg (proving a UI-captured value reaches a backend) points
at `${API_BASE_URL}` defaulting to a public echo service, and `docs/spa-testing.md`
notes it can point at the `express-demo` from the sibling `express-flag-proof-example`
change. No hard dependency between the two changes.

### D5: CI — one new opt-in job, path-filtered + nightly

Job in `ci.yml` (not a new workflow), triggered by changes under
`examples/react-demo/**`, `examples/angular-demo/**`, `examples/cases-spa/**`,
`src/ReleaseTwin.Adapters.Ui/**`, plus the nightly. Steps: setup-node, build both
demos, install Playwright browsers, serve each built demo, run the CLI against
`examples/cases-spa/` with `RELEASETWIN_UI_ENABLED=1`. The `ui.assertText` /
URL-wait unit tests run in the normal `dotnet test` job — they are offline (fake
page) and gate every PR; the browser job proves the examples.

### D6: Evidence-doc reconciliation is in scope, narrowly

Update `README.md` "What's not built yet" and `docs/flag-proof.md` UI mentions to
describe the current screenshot + `.webm` behavior and what remains deferred (the
external-check / Playwright *connector* — a distinct thing from the UI adapter).
No code change; this is a factual correction the SPA docs would otherwise
contradict.

## Risks / Trade-offs

- **Two front-end toolchains in a .NET repo** → confined to `examples/*-demo/`
  and one CI job; contributor `dotnet build`/`test` loop untouched; each demo
  README says why the toolchain is isolated. Angular CLI is heavy — pin it, commit
  the lockfile, keep the app to `ng new` minus extras.
- **Playwright browser download flakiness in CI** → cache the browser bundle;
  the job is opt-in so a transient failure doesn't block unrelated PRs.
- **SPA demos rot (framework major bump)** → pinned majors + committed lockfiles
  + the CI job catches breakage.
- **URL-wait glob semantics differ from contributor expectation** → spec pins
  "substring or glob"; document the exact form in `docs/spa-testing.md` with an
  example; test both a matching and non-matching case.
- **`InnerText` vs `TextContent` (hidden nodes, whitespace)** → use `InnerText`
  (visible text, normalized) to match what `assertVisible` users expect; note it
  in the operation's failure message so a mismatch is debuggable.
- **Scope creep toward "SPA testing framework"** → Non-Goals list is explicit;
  `docs/spa-testing.md` opens by saying this is journey evidence, not component
  testing.
