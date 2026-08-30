## Context

See proposal.md — *Why*. Relevant current state:

- `UiAdapter.CreateAsync(bool headless = true, CancellationToken)` launches Chromium and holds `IBrowser`. `Register` news up each `ui.*` operation with `_browser` and `new SetCookieOperation(_browser)` etc. `Dispose()` does `_browser.CloseAsync().GetAwaiter().GetResult()` — sync-over-async is already the pattern here.
- `UiOperationSupport.GetOrCreateContextAsync(context, browser)` — `browser.NewContextAsync()` on first use, stashed on `AdapterState["ui.context"]` (added by `ui-journey-visual-evidence`). `ClosePageCleanup` closes the context (which closes its pages) and clears both `ui.context` / `ui.page` keys.
- `CliRunner`: `uiEnabled` from `RELEASETWIN_UI_ENABLED`; `uiAdapter = await UiAdapter.CreateAsync(cancellationToken: …)` inside `if (uiEnabled)`.
- `web/cypress.config.ts` `runCliJourney` task: `execFileAsync("dotnet", ["run", …, "--journey", journeyRef], { env: { …process.env, RELEASETWIN_API_TOKEN, RELEASETWIN_API_URL, RELEASETWIN_FIXTURES_ROOT, ...env } })` — already spreads `process.env` and an optional `env` param (added by `ui-journey-visual-evidence`).
- Cypress 15; `video` defaults to off. Playwright records `.webm` (VP8); Cypress records `.mp4` (H.264). Playwright ships an ffmpeg binary at `<ms-playwright cache>/ffmpeg-*/ffmpeg-<os>`.
- The `naha-admin-ui-journey.cy.ts` spec composes a `ui.closePage` cleanup step.

## Goals / Non-Goals

**Goals**

- One command produces one watchable file showing: customer builds a journey in ReleaseTwin → it runs against NAHA's live admin → redacted evidence on the dashboard.
- The adapter's video hook is a clean, reusable primitive (not a demo-only hack).
- Zero change to the default test/CI path — video off unless asked.

**Non-Goals**

- Video as customer-facing evidence (uploaded / stored / redacted / rendered on the dashboard). Separate change; this only produces a local demo artifact.
- Automated redaction of the demo video. It's generated from NAHA's e2e environment with test data — human review before sharing, plus an optional region-blur on the project-secret input in Act 1.
- Recording anything other than the UI adapter's own browser (the CLI's HTTP legs, the API, etc.).
- Splitting the Cypress recording frame-accurately at the `cy.task` boundary — see D3.
- A general `cy.video()` / evidence-video command.

## Decisions

### D1: Thread `recordVideoDir` through operation constructors, not env-in-adapter or a wrapper

`UiAdapter` stores an optional `_recordVideoDir`. Each `ui.*` operation constructor takes it (`new NavigateOperation(_browser, _recordVideoDir)`, …) and passes it to `UiOperationSupport.GetOrCreateContextAsync(context, browser, recordVideoDir)`, which sets `RecordVideoDir` + `RecordVideoSize { Width = 1280, Height = 720 }` on the `BrowserNewContextOptions` when non-null.

Alternatives:
- *Adapter reads `RELEASETWIN_UI_VIDEO_DIR` directly.* Rejected — adapters never read env in this codebase; the CLI owns config resolution.
- *Eagerly create one context in `CreateAsync`.* Rejected — breaks the lazy per-run context isolation `ui.setCookie` depends on when a directory of UI cases runs.
- *Wrap `IBrowser` in a session object.* Cleaner long-term but a bigger refactor than this warrants; the constructor thread is 7 one-line changes.

### D2: Finalize the video in `ClosePageCleanup`, rename to `<caseId>.webm`

Playwright only resolves `page.Video.PathAsync()` **after** the context closes, and names files by GUID. `ClosePageCleanup`:
1. captures `page.Video` references for the context's pages (before close),
2. closes the context (as today),
3. `await video.PathAsync()` for each, `File.Move` to `<recordVideoDir>/<context.Case.CaseId>.webm` (last one wins if a journey somehow has multiple pages — journeys are single-page in practice).

The consumer globs `<dir>/*.webm` (the script passes the dir, so it knows where to look) — no stdout parsing.

`UiAdapter.Dispose()` is the fallback for a journey with no `ui.closePage`: on browser close Playwright still flushes videos to `recordVideoDir` with GUID names. The script tolerates GUID-named files too (newest `.webm` in the dir). But the spec having `ui.closePage` is the documented happy path.

### D3: No frame-accurate split of the Cypress recording — speed + trim Act 1 instead

Interleaving Acts perfectly would need the spec to emit timestamps and the script to `-ss`/`-to` slice the single Cypress `.mp4`. Instead:
- Act 1 = the whole Cypress recording, `setpts=PTS/3` (3× speed — the dashboard clicking isn't the payoff) and `-to` trimmed a few seconds before its end to cut the frozen `cy.task` gap.
- Act 2 = the adapter `.webm`, real-time.
- Act 3 = a short tail of the Cypress recording (the evidence page), taken as the last ~15s via `-sseof -15`.

Title cards between each make the cut legible even though Act 1 and Act 3 come from the same source file. If frame-accurate interleave is wanted later, add `cy.task("mark", …)` timestamps — deferred.

### D4: ffmpeg resolution order — installer dep → Playwright bundle → system

`@ffmpeg-installer/ffmpeg` (new dev dep) is the portable default. If it's absent, fall back to Playwright's bundled binary (`require("playwright-core/lib/utils/registry")` or the known cache path), then a system `ffmpeg` on PATH. The script fails with a clear "install ffmpeg or @ffmpeg-installer/ffmpeg" message if none resolve.

### D5: `video` gated on `CYPRESS_VIDEO=true`, not always-on

`video: process.env.CYPRESS_VIDEO === "true"` in `cypress.config.ts`. Default off — CI, `e2e`, and every other `e2e:*` script are unaffected (no video files, no encode time). `demo:naha-video` sets `CYPRESS_VIDEO=true` and `RELEASETWIN_UI_VIDEO_DIR=<tmp>` for its one run.

### D6: Output to a gitignored `demo/`

`demo/naha-releasetwin-flow.mp4` — regenerated on demand (before a sales call / release), never committed. `/demo/` added to `.gitignore` alongside the cypress entries.

## Risks / Trade-offs

- **Video only flushes on context close** → a journey without `ui.closePage` gets a GUID-named file (or, worst case, a 0-byte file if the process is killed). Mitigation: D2's fallback + the spec already has `ui.closePage`; the script warns if it finds no usable `.webm`.
- **Headless video quality / cursor** — Playwright headless video has no visible mouse cursor and can look "jumpy" on fast DOM changes. Acceptable for a demo; if it matters, run that leg headed (`RELEASETWIN_UI_HEADLESS=false` would be a further adapter knob, out of scope here).
- **Codec normalize is lossy + slow** — re-encoding both sources + concat is ~30–60s of ffmpeg on a ~2min combined video. Fine for an on-demand script.
- **NAHA e2e-admin alias must be up** (same dependency as the spec itself). If it's down the Cypress run fails and there's no video to stitch — the script exits early.
- **Secret on screen in Act 1** — the e2e secret is typed into a `type="password"` field (dots), and `{ log: false }` keeps it out of the Cypress command log, but a determined viewer of a slowed-down video sees keystroke timing only, not characters. Optional `--blur-secret-input` flag draws a box over that input's region in Act 1. Documented either way.
- **`@ffmpeg-installer/ffmpeg` is a ~30 MB platform binary dev dep** — acceptable; it's dev-only and the fallbacks mean it's not strictly required.

## Migration Plan

1. Adapter: `recordVideoDir` param + `GetOrCreateContextAsync` option + operation constructor thread + `ClosePageCleanup` finalize. Tests: `.webm` produced when dir set, unchanged when not.
2. CLI: `RELEASETWIN_UI_VIDEO_DIR` → `CreateAsync`.
3. `cypress.config.ts`: `video` gate + `runCliJourney` env forward.
4. `scripts/stitch-demo-video.mjs` + `@ffmpeg-installer/ffmpeg` + `demo:naha-video` + `.gitignore` + docs.
5. Run `npm run demo:naha-video`, eyeball the output.

Rollback: every piece is additive and off by default; delete the script + npm entry, revert the `video` line, drop the adapter param (nothing else references it).

## Open Questions

- Act 1 speed factor (3× is a guess) and whether to add the `mark` timestamps for a real interleave — decide after seeing the first cut.
- Whether `demo:naha-video` should also work against the deployed hosted API (`baseUrl=https://releasetwin.vercel.app`, like `e2e:secrets:prod`) rather than local — a "real production flow" video. Additive; the local version proves the pipeline first.
