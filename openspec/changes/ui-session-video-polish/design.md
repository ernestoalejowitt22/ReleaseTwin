## Context

See proposal.md — Why. Builds directly on `ui-session-video` (archived): the UI adapter records its
Playwright session to `<caseId>.webm`, `web/scripts/stitch-demo-video.mjs` joins the Cypress `.mp4`
(dashboard) and that `.webm` (NAHA) into a 3-act clip with `lavfi`+`drawtext` title cards, and
`npm run demo:naha-video` (in `web/`) drives the whole thing.

The current stitch pipeline: 6 segments (`card1`, `act1` = cypress sped `--act1-speed`, `card2`,
`act2` = webm + `tpad` freeze, `card3`, `act3` = cypress tail), each normalised to H.264 1280×720
30fps `-an`, then `concat -c copy`. ffmpeg is `@ffmpeg-installer/ffmpeg` (v4.4) — no ffprobe;
duration is parsed from `ffmpeg -i` stderr.

## Goals / Non-Goals

**Goals**
- Act 2 is real NAHA footage (home → companies → policies), not a held still.
- The clip reads as a narrated walkthrough: sub-titled cards, a persistent caption per act, a
  closing card.
- `demo:naha-video` still runs end to end with the same one command.

**Non-Goals**
- No frame-accurate cutting — same tolerance as `ui-session-video` (speed + trim, not scene
  detection).
- No audio / voiceover.
- No change to the adapter, the CLI, or what evidence is captured.
- Not landing this before NAHA `admin-e2e-route-auth` is live (the journey would fail).

## Decisions

### D1: Journey tours three routes via the existing step-composer helpers
The Cypress spec already builds the journey by driving the dashboard builder (`addStep`,
`stepParam`). Add three more `ui.navigate` + `ui.assertVisible` pairs (home re-assert is already
there; add companies, policies) plus a `ui.waitFor` on a stable element per route for dwell. Step
indices shift, so the API-bridge steps (currently 3–5) and their captures move down by the number of
inserted steps — update those indices in one place.

Route → assertion:
- `/` → `[data-testid="admin-home"]` (already present)
- `${adminUiBaseUrl}/companies` → `[data-testid="companies-page"]`
- `${adminUiBaseUrl}/policies` → `[data-testid="policies-page"]`

### D2: Captions via a second `drawtext` on each act segment, not burned into concat
Add a `caption` option to the `clip()` helper: a lower-thirds `drawtext` (`y=h-120`, `fontsize=28`,
semi-transparent `box=1:boxcolor=black@0.5`). Keeps each segment self-contained; no filter_complex
across the concat.

### D3: Title cards get a sub-line
`card(n, title, subtitle)` — render `title` at `fontsize=46` centred, `subtitle` at `fontsize=26`
~60px below. Reuse the existing `drawtext` escaping.

### D4: `--act2-freeze` defaults to 0, kept as an escape hatch
Real footage now fills Act 2. Keep the flag (default `0`) so a short/failed webm can still be
padded. If the webm is under ~4s, auto-apply a 2s freeze (warn on stderr) so the act is never a
single flash.

### D5: Re-tune Act 1 / Act 3 trim defaults from the actual recording
The richer journey makes the Cypress `.mp4` longer. `--act1-end` (drop-from-end) and `--act3-len`
stay flags; update their defaults after the first real `demo:naha-video` run and record the
observed Cypress duration in tasks.md.

### D6: Closing card
A 7th segment after `act3`: "ReleaseTwin — release-proof journeys against real customer targets"
(sub-line: the repo / a URL, left generic). Same `card()` helper.

## Risks / Trade-offs

- **NAHA Preview not yet redeployed when this applies** → the apply workflow's first step is to
  verify `/companies` + `/policies` return 200 behind the cookie; block if not.
- **`companies-page` / `policies-page` render an `<ApiError>` state instead of content** (mint
  succeeded but the list call failed) → `ui.assertVisible` on the page testid still passes (the
  `<main data-testid>` wraps the error too); the video just shows an error card. Acceptable — it is
  still the real app. Note it in docs' "review before sharing".
- **drawtext font path** already resolved from a list in the script; captions reuse it.

## Migration Plan

Not applicable (demo tooling + e2e spec). Land after NAHA `admin-e2e-route-auth`; verify the clip
with `npm run demo:naha-video`; PR to `main`. No deploy (touches no `hosted/**`).

## Open Questions

_None._
