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
- [ ] 1.4 Run `npm run e2e:naha-ui` — spec passes against the deployed NAHA e2e app; `PASS <caseId>`
      in CLI stdout, evidence renders. _(operator: needs the AWS Secrets Manager target + Clerk
      test user + Playwright chromium.)_

## 2. Stitch script presentation

- [x] 2.1 `card(n, title, subtitle)` — title `fontsize=46` (shifted up when a subtitle is present),
      subtitle `fontsize=26` below; `--card-secs` flag. All card calls now pass a sub-line.
- [x] 2.2 `caption(text)` helper + `clip(..., { caption })` — lower-thirds `drawtext` (`y=h-120`,
      `fontsize=28`, `box=1:boxcolor=black@0.5:boxborderw=18`); act1/act2/act3 each carry one.
- [x] 2.3 `--act2-freeze` default → `0`; auto 2s freeze + stderr warn when the adapter clip probes
      under 4s.
- [x] 2.4 7th segment `06-card4` (closing card) added to the build + concat list.
- [ ] 2.5 Re-tune `--act1-end` / `--act3-len` defaults from the first real run; record the observed
      Cypress `.mp4` duration here. _(operator: after 3.3. The 3-route journey lengthens the
      Cypress recording; current defaults 24 / 18 are unretuned. Script prints the observed
      duration at the end.)_
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
- [ ] 3.3 `npm run demo:naha-video` produces a playable `demo/naha-releasetwin-flow.mp4`: Act 2
      shows the live NAHA admin app touring home/companies/policies; captions + closing card
      render; no `--act2-freeze` hold needed. _(operator run.)_
- [ ] 3.4 Full .NET solution build + test green (unchanged, sanity only). _(operator run.)_
