> **Scope reduced 2026-09-03.** The bundled React/Angular demo apps and the
> GitHub Actions job moved to the separate `ci-portability-live-examples` change
> (a standalone `releasetwin-ci-examples` repo running the demos through
> Bitbucket / Azure / GitHub pipelines). This change keeps the engine code, the
> `ui-adapter` delta spec, the `examples/cases-spa/` reference YAML, and the
> docs. Original groups 4, 5, 7 are struck through below.

## 1. `ui.assertText` operation

- [x] 1.1 Added `AssertTextOperation` to `src/ReleaseTwin.Adapters.Ui/UiOperations.cs` — `selector` + one of `equals`/`contains`
- [x] 1.2 `equals`/`contains` values are resolved by the core (`CaptureReferenceResolver` for `{{capture}}`, load-time interpolation for `${VAR}`) before the operation runs — no per-operation work
- [x] 1.3 Registered in `UiAdapter.Register` + `KnownOperationCapabilities`
- [x] 1.4 Failure names selector + expected + actual; missing element / mismatch classified like any operation failure
- [x] 1.5 Offline tests: exact pass, contains with `{{capture}}`, mismatch (asserts cleanup ran), missing element, both-of-equals/contains rejected

## 2. `ui.waitFor` URL mode

- [x] 2.1 `WaitForOperation` takes an optional `url`; waits on `page.WaitForURLAsync` with a predicate — substring match, or glob when the pattern contains `*`
- [x] 2.2 Both `selector` and `url` → fail "exactly one wait target"; neither → fail; cleanup still runs
- [x] 2.3 Selector + `state` path unchanged; shared `TimeoutMs`
- [x] 2.4 Offline tests: route-change resolves, timeout names pattern + last URL, both-targets rejected

## 3. Delta spec is satisfied

- [x] 3.1 Every scenario in `specs/ui-adapter/spec.md` has a test (7 new, 20 in the UI suite)
- [x] 3.2 `openspec validate spa-ui-adapter-ergonomics --strict` — valid

## ~~4. React demo~~ — moved to `ci-portability-live-examples`

## ~~5. Angular demo~~ — moved to `ci-portability-live-examples`

## 6. SPA journey cases (`examples/cases-spa/`) — reference YAML only

- [x] 6.1 `examples/cases-spa/README.md` — env vars, `RELEASETWIN_UI_ENABLED=1`, points at `releasetwin-ci-examples` for runnable apps
- [x] 6.2 `react-journey.yaml` — navigate → click → `waitFor url **/detail/*` → `assertText contains "42"` + capture → `http.request ${API_BASE_URL}/get?order={{orderId}}` → `assertJsonPath $.args.order`
- [x] 6.3 `angular-journey.yaml` — identical pipeline
- [x] 6.4 `admin-cookie.yaml` — `ui.setCookie demo_role=admin` → navigate `/admin` → `assertText`
- [x] 6.5 `examples/fixtures/spa-journey.json`, sha256 `e371f715…4892` in all three
- [x] 6.6 `dotnet run -- run examples/cases-spa` (no UI enabled) → all 3 load and report `missing-capability:browser:chromium` (parse OK); end-to-end run is in `ci-portability-live-examples`

## ~~7. CI~~ — moved to `ci-portability-live-examples`

## 8. Docs

- [x] 8.1 `docs/spa-testing.md` written — journey-evidence framing, route wait (glob form), `ui.assertText`, `ui.setCookie` bypass, UI→API bridge, evidence, `releasetwin-ci-examples` link
- [x] 8.2 `README.md` — reworded the "External-check connector" bullet (the `ui.*` adapter already captures screenshots + `.webm`; what's deferred is folding an *external* suite in); added a `ReleaseTwin.Adapters.Ui` row to "What's here"
- [x] 8.3 `docs/flag-proof.md` — grep confirms no UI-evidence mention to fix
- [x] 8.4 Cross-linked from `docs/quickstart.md` ("More") and `examples/cases-ui-journey/README.md`

## 9. Verification

- [x] 9.1 `dotnet build` + `dotnet test ReleaseTwin.sln` — 294 passed, 0 failed (rebased on `main` after #121; UI suite 20, CLI suite 169)
- [x] 9.2 `node --test "integrations/github-action/**/*.test.mjs"` — 6/6 pass
- [x] 9.3 `openspec validate spa-ui-adapter-ergonomics --strict`
- [ ] 9.4 Evidence review deferred to `ci-portability-live-examples` (that's where the demos actually run under `RELEASETWIN_EVIDENCE=on`)
