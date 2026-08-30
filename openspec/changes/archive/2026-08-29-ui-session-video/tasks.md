## 1. UI adapter: record the browser session

- [x] 1.1 `UiOperationSupport.GetOrCreateContextAsync` — optional `string? recordVideoDir`; when set, `NewContextAsync(new BrowserNewContextOptions { RecordVideoDir = recordVideoDir, RecordVideoSize = new() { Width = 1280, Height = 720 } })`
- [x] 1.2 `UiAdapter.CreateAsync` — optional `string? recordVideoDir` param, stored on the instance; `Register` threads it into every `ui.*` operation constructor
- [x] 1.3 `ui.*` operation classes + `UiOperationBase` — accept `recordVideoDir`, pass to `GetOrCreateContextAsync`
- [x] 1.4 `ClosePageCleanup` — capture `page.Video` refs before close; after close, `await PathAsync()` then `video.SaveAsAsync(<recordVideoDir>/<caseId>.webm)` + `DeleteAsync()` the GUID original (SaveAsAsync waits for the recording to flush; a bare File.Move raced the flush); tolerate a missing/failed video without failing cleanup
- [x] 1.5 `UiAdapter.Dispose` — fallback finalize for a run with no `ui.closePage` (browser close flushes videos; leave GUID names, the consumer handles it)
- [x] 1.6 Tests (`ReleaseTwin.Adapters.Ui.Tests`): a run with `recordVideoDir` set produces a non-empty `<caseId>.webm`; a run without it is byte-for-byte unchanged (no `RecordVideo*` on the context, existing tests green)

## 2. CLI passthrough

- [x] 2.1 `CliRunner` — read `RELEASETWIN_UI_VIDEO_DIR`; when the UI adapter is enabled, pass it to `UiAdapter.CreateAsync`
- [x] 2.2 SKIPPED — the CLI test suite is HTTP-fake-handler based and never launches Playwright (deliberately). The passthrough is one line (`Get("RELEASETWIN_UI_VIDEO_DIR")` → `CreateAsync(recordVideoDir:)`); the recording behavior is covered by `UiSessionVideoTests`, and the end-to-end path by `demo:naha-video` (task 6.3).

## 3. Cypress wiring

- [x] 3.1 `web/cypress.config.ts` — `video: process.env.CYPRESS_VIDEO === "true"` (default off, CI unaffected)
- [x] 3.2 `web/cypress.config.ts` — `runCliJourney` task forwards `process.env.RELEASETWIN_UI_VIDEO_DIR` in the child `env` (no spec change)
- [x] 3.3 Confirm `e2e`, `e2e:naha-ui`, and other `e2e:*` runs are unchanged (no video, no new files) with `CYPRESS_VIDEO` unset

## 4. Stitch script

- [x] 4.1 `web/package.json` — add `@ffmpeg-installer/ffmpeg` (devDependencies)
- [x] 4.2 `scripts/stitch-demo-video.mjs` — ffmpeg resolution (installer → Playwright bundle → system, clear error if none); inputs: newest `web/cypress/videos/*naha-admin-ui-journey*.mp4` + newest `<RELEASETWIN_UI_VIDEO_DIR>/*.webm`
- [x] 4.3 Script — generate 3 title cards (`lavfi color` + `drawtext`, ~2.5s): "A customer builds a release-proof journey in ReleaseTwin" / "It runs against NAHA's live admin app — a real customer target" / "Redacted evidence lands back on the dashboard"
- [x] 4.4 Script — Act 1 = full Cypress mp4 at `setpts=PTS/3`, trimmed a few s before end; Act 2 = the `.webm` real-time; Act 3 = last ~15s of the Cypress mp4 (`-sseof`); all normalized to H.264 1280×720 30fps, then `concat`
- [x] 4.5 Script — optional `--blur-secret-input` flag: draw a black box over the project-secret input region during Act 1
- [x] 4.6 Script — output `demo/naha-releasetwin-flow.mp4`; print the path
- [x] 4.7 `web/package.json` — `demo:naha-video` = `start-server-and-test e2e:api <url> e2e:web <url> "..."` where the inner command sets `CYPRESS_VIDEO=true RELEASETWIN_UI_VIDEO_DIR=$(mktemp -d)`, runs `cypress run --spec cypress/e2e/naha-admin-ui-journey.cy.ts`, then `node ../scripts/stitch-demo-video.mjs`
- [x] 4.8 `.gitignore` — add `/demo/`

## 5. Docs

- [x] 5.1 A note (in `docs/` or the naha spec's neighborhood) — how to run `demo:naha-video`, what it produces, the "review before sharing / test data only" caveat, the `--blur-secret-input` option

## 6. Validation

- [x] 6.1 `openspec validate ui-session-video --strict` passes
- [x] 6.2 Full .NET solution build + all test projects green (`dotnet test ReleaseTwin.sln` — 162 passed; `ReleaseTwin.Adapters.Ui.Tests` green across repeated runs)
- [x] 6.3 `npm run demo:naha-video` produces a playable `demo/naha-releasetwin-flow.mp4` (H.264, ~58s, 3 acts + title cards); Act 2 shows the real NAHA Nails admin app (held on the final rendered frame via `--act2-freeze`), Act 3 shows the redacted NAHA screenshot on the dashboard's Run evidence page
- [x] 6.4 `web build` + `tsc` + `eslint` green; a plain `npm run e2e:naha-ui` passed and created no `cypress/videos` and no adapter video dir
