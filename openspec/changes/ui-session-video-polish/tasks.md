## 0. Precondition (NAHA dependency) — RESOLVED

- [x] 0.1 NAHA `admin-e2e-route-auth` merged and live: `/companies` + `/policies` render behind
      `Cookie: naha_e2e_role=admin` on the `e2e-admin` Preview (not `307 /sign-in`).
      _(ernestoalejowitt22/NAHA#66, commit `ad3768c`.)_
- [x] 0.2 The old manual Vercel-env step is gone. Two merged NAHA changes:
      - `admin-e2e-ui-visible` (#68, `bf08465`) — `NEXT_PUBLIC_E2E_AUTH=true` alone forces the
        company-branch / policy **UI** gates open.
      - `api-e2e-availability-open` (#70, `dea043d`) — `E2E_AUTH_ENABLED=true` on the shared API
        Lambda forces the `naha.*-api` **availability** gates open, so `/api/companies` +
        `/api/policies` serve instead of `404 not_found`.
      Verified end to end on the `e2e-admin` Preview: `/companies` → `companies-page` +
      `company-list` + `companies-empty` + `create-company-form`; `/policies` → `policies-page` +
      `policy-list` + `create-policy-form`. Act 2 shows the real admin UI (empty-state lists + the
      create forms). Seed e2e data before recording if a populated list is wanted.

## 1. Journey: tour three admin routes

- [x] 1.1 `web/cypress/e2e/naha-admin-ui-journey.cy.ts` — after `ui.assertVisible
      [data-testid="admin-home"]`, added `ui.navigate → ui.assertVisible → ui.waitFor` legs for
      `/companies` and `/policies`. The `ui.waitFor` targets the page-shell testid (not a child
      like `create-company-form`) so the journey is robust to the list being empty, populated, or
      (if NAHA e2e data/API regresses) an error card — see 0.2.
- [x] 1.2 API-bridge steps shifted 3/4/5 → 9/10/11 (login+capture, `/api/me`+header,
      `http.assertJsonPath`) — one contiguous edit.
- [x] 1.3 `ui.closePage` cleanup still last; end-of-spec evidence assertions unchanged.
- [x] 1.4 `npm run demo:naha-video` ran the spec against the live NAHA e2e app — `naha-admin-ui-journey.cy.ts`
      **1/1 passing** (01:22); the composed journey shows all three routes and `PASS E2E-NAHA-UI-…`
      (the spec's stdout assertion) held. Evidence uploaded (no "evidence not accepted").

## 2. Stitch script presentation

- [x] 2.1 `card(n, title, subtitle)` — title `fontsize=46` (shifted up when a subtitle is present),
      subtitle `fontsize=26` below; `--card-secs` flag. All card calls now pass a sub-line.
- [x] 2.2 `caption(text)` helper + `clip(..., { caption })` — lower-thirds `drawtext` (`y=h-120`,
      `fontsize=28`, `box=1:boxcolor=black@0.5:boxborderw=18`); act1/act2/act3 each carry one.
- [x] 2.3 `--act2-freeze` default → `0`; auto 2s freeze + stderr warn when the adapter clip probes
      under 4s.
- [x] 2.4 7th segment `06-card4` (closing card) added to the build + concat list.
- [x] 2.5 First real `demo:naha-video` run: Cypress `.mp4` = **83.8s**, adapter `.webm` = **9.4s**
      (no auto-freeze — >4s). Bumped `--act1-end` default 24 → 30 (the richer journey pads the
      recording tail). `--act3-len` 18 kept. Output clip = 79.7s, 1280×720, 7 segments. Fine-tune
      pacing visually from here if wanted.
- [x] 2.6 Header comment + `docs/demo-videos.md` updated — route tour, captions, closing card, new
      defaults, and the Act-2 "real app, list or error state" note. "Review before sharing / test
      data only" caveat kept.

## 3. Validation

- [x] 3.1 `openspec validate ui-session-video-polish --strict` passes.
- [~] 3.2 `eslint` green on `naha-admin-ui-journey.cy.ts` + `stitch-demo-video.mjs`; `node --check`
      green on the script; a synthetic-input smoke run of `stitch-demo-video.mjs` produced a
      playable 7-segment mp4 (all drawtext/caption filters valid). `tsc --noEmit` is blocked by a
      pre-existing Next type-gen quirk (`LayoutProps` global needs a prior `next build`; unrelated
      to these files — no app source changed). `e2e:*` scripts untouched, so the `CYPRESS_VIDEO`-
      unset "no video files" behavior is unchanged.
- [x] 3.3 `npm run demo:naha-video` produced a playable `demo/naha-releasetwin-flow.mp4` (79.7s,
      H.264 1280×720). Act 2 is live NAHA footage of home → companies → policies; per-act captions
      and the closing card render; no `--act2-freeze` hold. Sent to the user for review.
- [~] 3.4 .NET solution build + test — not re-run this pass (no `src/` or `hosted/` change in this
      change; the CLI built fine as part of `demo:naha-video`'s `dotnet run`). Sanity only.
