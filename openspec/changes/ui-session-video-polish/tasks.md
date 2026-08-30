## 0. Precondition (NAHA dependency)

- [x] 0.1 Confirm NAHA `admin-e2e-route-auth` is merged and live: `/companies` and `/policies` on
      the `e2e-admin` Preview return `200` + page testid (not `307 /sign-in`) with
      `Cookie: naha_e2e_role=admin`. Block the rest of this change until this passes.
      _(Merged as ernestoalejowitt22/NAHA#66, commit `ad3768c`.)_
- [ ] 0.2 Confirm the companies/policies **content** renders, not the `*-ui-hidden` panel — the
      `e2e-admin` Vercel Preview needs `NEXT_PUBLIC_E2E_COMPANY_BRANCH_UI=true` and
      `NEXT_PUBLIC_E2E_POLICY_UI=true` (or the `naha.company-branch-ui` / `naha.policy-ui` LD flags
      ON for the e2e LD context). These are Vercel env vars — a manual set by the operator. If they
      can't be enabled, fall back: assert on the `*-ui-hidden` section's testid instead and note in
      docs that Act 2 shows the gated-off state.
      _(Operator toggling; docs note the gated-off fallback.)_

## 1. Journey: tour three admin routes

- [x] 1.1 `web/cypress/e2e/naha-admin-ui-journey.cy.ts` — after the existing `ui.assertVisible`
      `[data-testid="admin-home"]`, add composed steps: `ui.navigate ${adminUiBaseUrl}/companies`
      → `ui.assertVisible [data-testid="companies-page"]`; then the same for `/policies`
      (`[data-testid="policies-page"]`). _(The `ui.waitFor` dwell from the design was dropped —
      `ui.waitFor` waits on a selector state, not a duration, and `ui.assertVisible` already does
      that wait; the real navigations give Act 2 its footage.)_
- [x] 1.2 Shift the API-bridge step indices (`http.request` login, `http.request /api/me`,
      `http.assertJsonPath`) and their capture/header wiring from 3–5 to 7–9 — one contiguous edit
- [x] 1.3 Keep `ui.closePage` cleanup last; keep the evidence-page assertions at the end unchanged
- [ ] 1.4 Run `npm run e2e:naha-ui` — spec passes against the deployed NAHA e2e app; `PASS <caseId>`
      in CLI stdout, evidence renders _(needs 0.2 + the full local stack; blocked on operator)_

## 2. Stitch script presentation

- [x] 2.1 `card(n, title, subtitle)` — title `fontsize=46`, optional subtitle `fontsize=24` below;
      the four `card()` calls carry sub-lines
- [x] 2.2 `clip(..., { caption })` — lower-thirds `drawtext` (`y=h-96`, `fontsize=27`,
      `box=1:boxcolor=black@0.55`); act1/act2/act3 pass a caption
- [x] 2.3 `--act2-freeze` default → `0`; a webm under ~4s auto-applies a 2s freeze and warns
- [x] 2.4 Added a closing-card segment (`06-card4`) after act3; extended the concat order list
- [ ] 2.5 Re-tune `--act1-end` / `--act3-len` defaults from the first real run; record the observed
      Cypress `.mp4` duration here _(current defaults 30 / 16 are estimates for the ~58s three-route
      recording — confirm after the first `demo:naha-video`)_
- [x] 2.6 Header comment + `docs/demo-videos.md` — documented the route tour, captions, closing
      card, `--no-crop-cypress`, new defaults; kept the "review before sharing / test data only"
      caveat and the possible `<ApiError>` state note

### Beyond the original scope

- [x] Crop the Cypress test-runner chrome (command-log sidebar + URL bar) out of Act 1/3 by
      default, so the clip reads as a product recording. `--no-crop-cypress` opts out.
- [ ] Embed the finished clip on the marketing landing page (`web/public/demo-naha-flow.mp4`).

## 3. Validation

- [ ] 3.1 `openspec validate ui-session-video-polish --strict` passes
- [ ] 3.2 `web` `tsc` + `eslint` green; `npm run e2e:naha-ui` still leaves no video files with
      `CYPRESS_VIDEO` unset
- [ ] 3.3 `npm run demo:naha-video` produces a playable `demo/naha-releasetwin-flow.mp4`: Act 2
      visibly shows home, companies, and policies of the live NAHA admin app; captions and the
      closing card render; no `--act2-freeze` hold needed
- [ ] 3.4 Full .NET solution build + test green (unchanged, sanity only)
