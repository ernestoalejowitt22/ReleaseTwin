## 1. `ui.assertText` operation

- [ ] 1.1 Add `AssertTextOperation` to `src/ReleaseTwin.Adapters.Ui/UiOperations.cs` — params `selector`, one of `equals`/`contains`, `expected`; reads visible text (Playwright `InnerTextAsync`, trimmed)
- [ ] 1.2 Route `expected` through the same `${VAR}` + `{{capture}}` substitution the other operations use
- [ ] 1.3 Register the operation in the UI adapter's operation list / composition root
- [ ] 1.4 Failure messages name selector, expected, and actual text; missing element and text mismatch both classified as normal operation failures
- [ ] 1.5 Offline tests (fake page): exact pass, contains pass with a `{{capture}}`, text mismatch, missing element — assert cleanup still runs

## 2. `ui.waitFor` URL mode

- [ ] 2.1 Add an optional `url` param to `WaitForOperation`; when present, wait on `page.WaitForURLAsync` (substring/glob) instead of the selector
- [ ] 2.2 Reject a step that declares both `selector` and `url` with a clear message; cleanup still runs
- [ ] 2.3 Keep existing selector + `state` behavior unchanged; share the timeout handling
- [ ] 2.4 Offline tests: URL match resolves, URL never matches (timeout message names expected + last URL), both-targets-declared rejection

## 3. Delta spec is satisfied

- [ ] 3.1 Re-read `specs/ui-adapter/spec.md`; confirm every scenario has a corresponding test from groups 1 and 2
- [ ] 3.2 `openspec validate spa-ui-adapter-ergonomics --strict`

## 4. React demo (`examples/react-demo/`)

- [ ] 4.1 `npm create vite` (react-ts), strip to one screen; pin React + Vite majors; commit `package-lock.json`; Apache-2.0 header + `README.md` explaining the isolated toolchain
- [ ] 4.2 Client route `/` → `/detail/:id`; `/detail/:id` renders the `id` into a `data-testid` element
- [ ] 4.3 Cookie-gated view: a route that shows a different element when a named cookie is set (for `ui.setCookie`)
- [ ] 4.4 `npm run build` produces static assets served by a plain static server (document the exact serve command)

## 5. Angular demo (`examples/angular-demo/`)

- [ ] 5.1 `ng new` minus extras; pin Angular major; commit lockfile; Apache-2.0 header + `README.md`
- [ ] 5.2 Same shape as the React demo: `/` → `/detail/:id` route, rendered param in a `data-testid` element, cookie-gated view
- [ ] 5.3 `ng build` produces static assets; document the serve command

## 6. SPA journey cases (`examples/cases-spa/`)

- [ ] 6.1 `README.md` — how to build + serve each demo, then run with `RELEASETWIN_UI_ENABLED=1`
- [ ] 6.2 `react-journey.yaml` — `ui.navigate` → `ui.click` a link → `ui.waitFor` url `**/detail/*` → `ui.assertText` on the rendered id (contains) → `capture` the id → `http.request` to `${API_BASE_URL}` with the captured value → `http.assertJsonPath`; `cleanup: ui.closePage`
- [ ] 6.3 `angular-journey.yaml` — same pipeline against the Angular demo
- [ ] 6.4 One case demonstrates `ui.setCookie` before navigation against the gated view
- [ ] 6.5 `fixtures/` + recomputed `sha256` per case
- [ ] 6.6 Run both locally end-to-end; confirm pass and that captured value reaches the API leg

## 7. CI

- [ ] 7.1 Add a job to `.github/workflows/ci.yml`: path filter on `examples/react-demo/**`, `examples/angular-demo/**`, `examples/cases-spa/**`, `src/ReleaseTwin.Adapters.Ui/**`, plus nightly
- [ ] 7.2 Steps: setup-node (pinned), build both demos, cache + install Playwright browsers, serve each built demo, build the CLI, run it against `examples/cases-spa/` with `RELEASETWIN_UI_ENABLED=1`
- [ ] 7.3 Confirm `gh auth` has `workflow` scope before pushing the workflow edit
- [ ] 7.4 Green run on a branch (link in the PR)

## 8. Docs

- [ ] 8.1 `docs/spa-testing.md` — "Testing a React or Angular app": opens by scoping this as journey evidence not component testing; cookie bypass; waiting for a route (exact glob form + example); asserting rendered text; bridging a captured value into an API leg
- [ ] 8.2 `README.md` — add the SPA examples to the examples table; reconcile "What's not built yet" to describe current screenshot + `.webm` capture vs. the still-deferred external-check/Playwright connector
- [ ] 8.3 `docs/flag-proof.md` — fix any UI-evidence mention that contradicts 8.2
- [ ] 8.4 Cross-link `docs/spa-testing.md` from `docs/quickstart.md` and the UI journey example README

## 9. Verification

- [ ] 9.1 `dotnet build ReleaseTwin.sln` + `dotnet test ReleaseTwin.sln` green; report the new test count
- [ ] 9.2 `node --test integrations/github-action/` green
- [ ] 9.3 `openspec validate spa-ui-adapter-ergonomics --strict`
- [ ] 9.4 Run the SPA cases with `RELEASETWIN_EVIDENCE=on`; open the evidence folder, confirm screenshots + `.webm` land where `docs/spa-testing.md` says and show the real rendered screens (no blank frames / spinner-only clips)
