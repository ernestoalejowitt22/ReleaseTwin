## 1. Credential / access preflight

- [x] 1.1 Run the credential-preflight skill — done, results in `preflight.md`. gh
  `workflow`+`repo` ✓; AWS secrets ✓ (all NAHA + LD keys present); gaps: no CLI image
  published, NAHA repo missing `RELEASETWIN_NAHA_*` secrets, `read:packages` scope.
- [x] 1.2 Done — `release.yml` was missing the Playwright-browser install step (`ci.yml`
  has it); added it, landed on `main`, re-tagged `v0.1.0`. Run 33358589097 green,
  `ghcr.io/ernestoalejowitt22/releasetwin/cli:{0.1.0,latest}` published.
- [x] 1.3 Done — package made **public**. Verified: anon registry token lists tags
  `["0.1.0","latest"]`, manifest `0.1.0` returns 200 unauthenticated.
- [x] 1.4 Confirmed — `gh` has `workflow` + `repo` scope; NAHA workflow authored.

## 2. Wire the Action into NAHA

- [x] 2.1 Done — NAHA branch `releasetwin-gate` (commit 35d0eca, local, not pushed):
  `releasetwin/cases/naha-admin-auth.yaml` (`NAHA-ADMIN-AUTH-1`: e2e login → `/api/me` →
  assert role admin) + fixture + `releasetwin/README.md`. **Verified green against the
  live NAHA e2e API** (`apiBaseUrl` from `releasetwin/e2e/naha-account`), exit 0.
- [x] 2.2 Done — `.github/workflows/releasetwin.yml` on that branch: `on: pull_request`
  (paths `naha.backend/**`, `releasetwin/**`), Action pinned `@main` / image `:0.1.0`,
  forwards `NAHA_API_URL`/`NAHA_E2E_SECRET`/`NAHA_ADMIN_EMAIL` from repo secrets.
- [x] 2.3 Done — NAHA PR #74 open. `DEMO-GATE-1` fails against the live API; run
  33359474724 rendered the **red** `ReleaseTwin` check + comment (❌ failed, 1 passed ·
  1 failed, DEMO-GATE-1 row). Verified visually in the browser. **Bug found + fixed along
  the way:** the Action's run step (`shell: bash` → `-e`) aborted before the render step
  on a case failure, so nothing was posted — broke ci-pr-integration scenario "A failing
  run still posts the summary". Fixed in `action.yml` (`set +e`, `if: always()`), landed
  on `main` (443a13c).
- [x] 2.4 Done — commit a6117b6 on `releasetwin-gate` fixes the assertion; run 33360603651
  **green**, comment **updated in place** to ✅ passed (2 passed · 0 failed).
- [x] 2.5 **Won't do** — required status checks on NAHA need GitHub Pro or a public repo
  (both the branch-protection and rulesets APIs return 403 "Upgrade to GitHub Pro" on the
  free plan). Decision: leave the check unenforced on NAHA. The landing-page caption and
  `/docs/ci` already frame it as "a failing check you make required," which is accurate —
  the gate runs and fails the PR; whether it *blocks* merge is a repo-settings choice. If
  GitHub Pro is added later: NAHA → Settings → Rules → Rulesets → require `release-proof`
  on `main`.

## 3. Capture script

- [x] 3.1/3.2 Done — `web/scripts/capture-landing-demo.mjs` generates the PR panels as
  committed SVG from the two real run summaries (`web/scripts/demo-summaries/{passed,
  failed}.json`, produced by the CLI against `<naha>/releasetwin/cases`). Outputs
  `web/public/demo/pr-{comment,check}-{failed,passed}.svg`. No browser/deps — SVG is
  crisp + diff-reviewable. (Approach change from raw GitHub screenshots: the real PR page
  carries NAHA's Vercel comment + a colliding `ui-auto-dress / ReleaseTwin` check label,
  and the comment updates in place so only one state is ever live. Spec updated to match.)
- [x] 3.3 Done — `web/cypress/e2e/capture-landing-demo.cy.ts` (sign in → Team project →
  enable evidence → run the bundled cases with `RELEASETWIN_EVIDENCE=on` → screenshot
  **run history** + **evidence viewer** with a credential-shaped header redacted). Ran
  green via `npm run capture:dashboard` (local hosted API, in-memory store, no Docker/AWS).
  Post-billing fix: replaced the removed free-`Upgrade` click with `cy.elevateToTeam()`;
  tightened the screenshots (frame the "Run history" card; hide `next dev` overlay; anchor
  the evidence shot on its title). Trend + rollup panels stay dropped.
- [x] 3.4 Done — `capture-dashboard-demo.mjs` wrote `web/public/demo/dashboard-{runs,evidence}.png`;
  added `DASHBOARD_PANELS` (a 2-panel group after the PR-loop panels) in `page.tsx`.
- [x] 3.5 Done — `docs/landing-demo.md`: asset list, prerequisites, the demo-PR + dashboard
  capture procedure, credential sources (reuses `releasetwin/e2e/*` Secrets Manager path).
- [x] 3.6 Done — review checklist in `docs/landing-demo.md` ("before committing any asset").

## 4. Landing page

- [x] 4.1 Done — `DashboardPreview` (+ its `Table`/`Badge`/`Card` imports) removed from
  `web/src/app/(marketing)/page.tsx`.
- [x] 4.2 Done — CI-loop section: 3 PR panels (check ✗ / comment ✗ / check ✓) + a
  dashboard-pointer line + 2 dashboard panels (run history, evidence viewer), each with a
  claim caption.
- [x] 4.3 Done — `loading="lazy"`, explicit `width`/`height` per panel, descriptive `alt`.
- [x] 4.4 Terminal SVG kept in the hero; section intro copy makes the loop the headline.

## 5. Bitbucket documentation

- [x] 5.1 Add a Bitbucket Pipelines YAML snippet + a "`--summary-json` is CI-agnostic"
  note to `web/src/app/(marketing)/docs/ci/`.
- [x] 5.2 Done — landing CI-loop section links to `/docs/hosted-platform` for the
  evidence story; `/docs/ci` carries the Bitbucket snippet inline.

## 6. Verify

- [x] 6.1 `cd web && npm run build` + `npx eslint` — clean (with the CI-loop section +
  capture script in place).
- [x] 6.2 `openspec validate landing-demo-ci-loop --strict` — passed.
- [x] 6.3 Done — `next dev` preview: all 5 demo images load (correct natural dimensions),
  the CI-loop section renders in order (3 PR panels → dashboard line → 2 dashboard panels)
  with every caption present. `npm run build` + `eslint` clean.
- [x] 6.4 Assets reviewed: `web/public/demo/pr-comment-failed.svg` (GitHub-dark comment
  card, "ReleaseTwin — ❌ failed", 1 passed · 1 failed, table row `DEMO-GATE-1 / failed /
  product / — / releasetwin-gate`), `pr-comment-passed.svg` (✅ passed, 2 passed · 0
  failed, no table), `pr-check-failed.svg` / `pr-check-passed.svg` (red/green
  `ReleaseTwin` check chips). All test data only — NAHA e2e account, no secrets visible.
