## Why

The `naha-admin-ui-journey` e2e proves the whole "ReleaseTwin → a real customer app" flow works, but you can't *watch* it as one thing. The run spans two browsers: Cypress drives the ReleaseTwin dashboard, then `cy.task("runCliJourney")` shells out to the CLI, which launches its **own** headless Playwright browser to drive NAHA's admin app — Cypress records none of that. There's no artifact that shows a customer building a journey, it running against their live app, and the redacted evidence landing back on the dashboard, in sequence.

This adds the two missing pieces to produce that artifact on demand: the UI adapter can record its browser session to a directory, and a script stitches the Cypress recording + the adapter recording into one narrated demo video.

## What Changes

- **UI adapter can record the browser session.** `UiAdapter.CreateAsync` gains an optional `recordVideoDir`; when set, the run's browser context is created with Playwright video recording (headless-compatible, sized to match Cypress's 1280×720). On `ui.closePage` the finalized `.webm` is renamed to a stable `<caseId>.webm` in that directory so a consumer can find it by name. Off by default — no video, no behavior change, byte-for-byte as today.
- **CLI passes it through.** `CliRunner` reads `RELEASETWIN_UI_VIDEO_DIR` and forwards it to `UiAdapter.CreateAsync`, the same opt-in shape as `RELEASETWIN_UI_ENABLED`.
- **Cypress `runCliJourney` task forwards `RELEASETWIN_UI_VIDEO_DIR`** from its own environment automatically (no spec change needed) so a `--journey` run under the demo script records.
- **`video` becomes opt-in in `cypress.config.ts`** — `video: process.env.CYPRESS_VIDEO === "true"` (Cypress 15 default is already off; this just makes it toggleable without a `--config` flag). Default stays off — CI is unaffected.
- **New `scripts/stitch-demo-video.mjs`** — takes the Cypress `.mp4` + the adapter `.webm`, generates three title cards, normalizes codecs (Cypress H.264 / Playwright VP8 → one H.264 1280×720 stream), optionally speeds up the dashboard act, concatenates, and writes `demo/naha-releasetwin-flow.mp4`. Resolves ffmpeg from `@ffmpeg-installer/ffmpeg` (new dev dep) or Playwright's bundled binary or system `ffmpeg`.
- **New `npm run demo:naha-video`** — `start-server-and-test` + `cypress run` the naha-ui spec with video on + the stitch step. Output under a gitignored `demo/`.
- **Docs** — a short note on generating the demo video and the "review before sharing" caveat.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none — the adapter's video-recording hook is a new opt-in operational switch, not a spec-level behavior change, same category as `RELEASETWIN_UI_ENABLED`. The rest is demo tooling. `.openspec.yaml` sets `skip_specs: true`.)

## Impact

- **`ReleaseTwin.Adapters.Ui`**: `UiAdapter.CreateAsync` signature (optional param, additive); `UiOperationSupport.GetOrCreateContextAsync` takes an optional `recordVideoDir`; the `ui.*` operation constructors thread it from `UiAdapter.Register`; `ClosePageCleanup` resolves + renames the video on context close.
- **`ReleaseTwin.Cli`**: `CliRunner` reads one new env var, passes it to `CreateAsync`.
- **`web/cypress.config.ts`**: `video` line; `runCliJourney` forwards one env var.
- **`web/package.json`**: `@ffmpeg-installer/ffmpeg` dev dep, `demo:naha-video` script.
- **New**: `scripts/stitch-demo-video.mjs`, `demo/` in `.gitignore`, a docs note.
- **Tests**: `ReleaseTwin.Adapters.Ui.Tests` — a video-dir run produces a `.webm`; a no-dir run is unchanged. No Cypress test change (the demo script isn't a test).
- **Not in scope**: video as *customer-facing evidence* (uploaded, stored, redacted, on the dashboard). That's a separate, larger change (`evidence-capture` + `evidence-store`) — this reuses the recording primitive it would need, but stops at a local demo artifact with no redaction beyond "review before sharing."
- **Reuses**: the per-run browser context seam added by `ui-journey-visual-evidence`.
