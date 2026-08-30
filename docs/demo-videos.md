# Demo videos — the ReleaseTwin → NAHA end-to-end flow

Internal reference. Produces a single watchable clip of the whole story: a customer builds a
release-proof journey in the ReleaseTwin dashboard → it runs against NAHA's live admin app → the
redacted evidence lands back on the dashboard.

## Run it

```bash
cd web
npm run demo:naha-video
```

`start-server-and-test` brings up the hosted API (`:5199`) and the Next.js dev server (`:3000`),
then:

1. runs `cypress run --spec cypress/e2e/naha-admin-ui-journey.cy.ts` with `CYPRESS_VIDEO=true`, so
   Cypress records its own browser (the ReleaseTwin dashboard half) to
   `web/cypress/videos/naha-admin-ui-journey.cy.ts.mp4`;
2. that spec's `cy.task("runCliJourney")` shells out to `ReleaseTwin.Cli`, which — because
   `RELEASETWIN_UI_ENABLED=1` and `RELEASETWIN_UI_VIDEO_DIR=../demo/.adapter-video` are set — runs
   its own headless Playwright Chromium against NAHA's admin app and records that session to
   `demo/.adapter-video/<caseId>.webm`;
3. `node scripts/stitch-demo-video.mjs` stitches the two recordings, with three narrated title
   cards, into `demo/naha-releasetwin-flow.mp4`.

Cypress and the CLI drive **two separate browsers** — Cypress cannot see the CLI's Playwright
window, which is why the adapter records itself and the script joins the halves afterward.

## Output

`demo/naha-releasetwin-flow.mp4` — H.264, 1280×720. Four title cards + three acts, each act with a
persistent lower-thirds caption:

| Segment | Source | Notes |
|---|---|---|
| Card 1 | generated | "Build a release-proof journey" |
| Act 1 | Cypress `.mp4`, sped up (`--act1-speed`, default 2.4×), Cypress chrome cropped | Building the journey in the dashboard |
| Card 2 | generated | "Run it against NAHA's live admin app" |
| Act 2 | adapter `.webm`, real time | The headless browser touring NAHA's admin app — home → companies → policies (`ui-session-video-polish`) |
| Card 3 | generated | "Redacted evidence on the dashboard" |
| Act 3 | tail of the Cypress `.mp4` (`--act3-len`, default 16s), chrome cropped | The redacted evidence rendered back on the dashboard |
| Card 4 | generated | Closing card |

`ui-session-video-polish` widened Act 2: the `naha-admin-ui-journey` journey now navigates `/` →
`/companies` → `/policies` with a `ui.assertVisible` per route, so the adapter recording is real
changing admin UI, not one page load. **This needs NAHA's `admin-e2e-route-auth` live on the
`e2e-admin` Preview** (`/companies` + `/policies` behind `naha_e2e_role=admin`), plus the Preview's
`NEXT_PUBLIC_E2E_COMPANY_BRANCH_UI` / `NEXT_PUBLIC_E2E_POLICY_UI` env set so those routes render
content, not the `*-ui-hidden` panel. Without the env, the pages still resolve (the
`ui.assertVisible` on the page testid passes) but Act 2 shows the gated-off state.

`demo/*.mp4` is gitignored — the raw clip is a build artifact. The copy embedded on the marketing
site lives at `web/public/demo-naha-flow.mp4` and is committed.

## Tuning

```bash
node scripts/stitch-demo-video.mjs --act1-speed 3 --act1-end 40 --act3-len 14 --no-crop-cypress
```

- `--no-crop-cypress` keeps the Cypress test-runner chrome (command log + URL bar) in Act 1/3.
  Default is to crop it out so the clip reads as a product recording.
- `--act2-freeze <sec>` holds Act 2's final frame; default `0` now that the route tour fills the
  act. A webm under ~4s auto-gets a 2s hold.
- `--blur-secret-input` draws a black box over the project-secret input region during Act 1.
- `--cypress-video <file>` / `--video-dir <dir>` override the auto-discovered inputs.
- ffmpeg is resolved from `@ffmpeg-installer/ffmpeg`, then a Playwright `ffmpeg-*` bundle, then
  `ffmpeg` on `PATH`.

## Before sharing

**Review the clip end to end first.** It is driven against the real NAHA e2e deployment with a real
login. Everything shown must be **test data only** — the NAHA e2e admin account, seeded fixtures,
nothing from a real customer or a real operator inbox. If the admin token mint succeeds but a
list call fails, `/companies` or `/policies` can render an `<ApiError>` card instead of content —
`ui.assertVisible` on the page testid still passes, but re-run before shipping a clip that shows an
error state. The adapter redacts password-field values
before they can reach evidence, but the video itself is raw screen capture: if a step surfaces
something that should not be on a shared clip, re-record with different data or trim that segment.

## Not affected

Plain `npm run e2e`, `npm run e2e:naha-ui`, and every other `e2e:*` script leave `CYPRESS_VIDEO`
unset — no video is recorded and no files are written. CI is unchanged.
