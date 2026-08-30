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
   `demo/.adapter-video/<caseId>.webm`. The journey tours three admin routes — home → `/companies`
   → `/policies` — so Act 2 is real, changing admin UI rather than one held frame;
3. `node scripts/stitch-demo-video.mjs` stitches the two recordings — four narrated cards (three
   title + one closing), a persistent lower-thirds caption per act — into
   `demo/naha-releasetwin-flow.mp4`.

Cypress and the CLI drive **two separate browsers** — Cypress cannot see the CLI's Playwright
window, which is why the adapter records itself and the script joins the halves afterward.

## Output

`demo/naha-releasetwin-flow.mp4` — H.264, 1280×720, three acts:

| Act | Source | Notes |
|---|---|---|
| 1 | Cypress `.mp4`, sped up (`--act1-speed`, default 2×) | The customer building/launching the journey in the dashboard |
| 2 | adapter `.webm`, real time | The headless browser touring NAHA's real admin app: home → companies → policies |
| 3 | tail of the Cypress `.mp4` (`--act3-len`, default 18s) | The redacted evidence rendered back on the dashboard |

Each act carries a persistent lower-thirds caption; the clip ends on a closing card.
`demo/` is gitignored — the clip is a build artifact, not a checked-in asset.

**Act 2 and the live NAHA data.** The companies/policies routes render behind the e2e cookie:
NAHA forces both the admin **UI** gates (`NEXT_PUBLIC_E2E_AUTH`) and the API **availability** gates
(`E2E_AUTH_ENABLED`) open for the e2e surface, so `/companies` and `/policies` show the real
list + create form. With no seeded data they show the empty state ("No companies yet"). Seed a
company and a policy against the e2e API before recording if you want populated lists in the clip.
The journey asserts on the page-shell testid, so it stays green regardless.

## Tuning

```bash
node scripts/stitch-demo-video.mjs --act1-speed 3 --act1-end 60 --act3-len 15 --blur-secret-input
```

- `--act1-end` / `--act3-len` — the 3-route journey lengthens the Cypress recording; re-tune these
  from the observed duration the run prints at the end.
- `--act2-freeze <sec>` — default `0` (Act 2 is real footage now); auto-applies a 2s tail freeze
  if the adapter clip comes in under 4s. `--card-secs` sets each card's duration (default 2.6).
- `--blur-secret-input` draws a black box over the project-secret input region during Act 1.
- `--cypress-video <file>` / `--video-dir <dir>` override the auto-discovered inputs.
- ffmpeg is resolved from `@ffmpeg-installer/ffmpeg`, then a Playwright `ffmpeg-*` bundle, then
  `ffmpeg` on `PATH`.

## Before sharing

**Review the clip end to end first.** It is driven against the real NAHA e2e deployment with a real
login. Everything shown must be **test data only** — the NAHA e2e admin account, seeded fixtures,
nothing from a real customer or a real operator inbox. The adapter redacts password-field values
before they can reach evidence, but the video itself is raw screen capture: if a step surfaces
something that should not be on a shared clip, re-record with different data or trim that segment.

## Not affected

Plain `npm run e2e`, `npm run e2e:naha-ui`, and every other `e2e:*` script leave `CYPRESS_VIDEO`
unset — no video is recorded and no files are written. CI is unchanged.
