## Why

The `demo:naha-video` clip (shipped in `ui-session-video`, PR #12) works, but Act 2 — the live
NAHA admin app — is thin: the `naha-admin-ui-journey` journey only navigates to `/` and asserts one
element, so the adapter recording is ~6s and mostly the initial load. The stitch script papers over
this by freezing the last frame (`--act2-freeze`). Once NAHA's `admin-e2e-route-auth` change lands
(`/companies` and `/policies` render behind the e2e cookie), the journey can actually tour the
customer app, and the clip's presentation can be tightened to match.

## What Changes

- **Journey** — `web/cypress/e2e/naha-admin-ui-journey.cy.ts`: after the existing home assertion, the
  composed journey navigates `/` → `/companies` → `/policies`, each with a `ui.assertVisible` on the
  route's page testid and a short `ui.waitFor` dwell, so Act 2 shows real, changing admin UI. The
  API-bridge legs and evidence assertions are unchanged.
- **Stitch script** — `web/scripts/stitch-demo-video.mjs`:
  - title cards gain a second, smaller sub-line
  - each act gets a low-thirds caption drawn over the video ("Building the journey", "Driving NAHA's
    admin app", "Redacted evidence on the dashboard")
  - a closing card
  - pacing: `--act2-freeze` default drops to 0 (real footage now fills the act); Act 1 / Act 3
    trim defaults re-tuned for the longer Cypress recording the richer journey produces
- **docs/demo-videos.md** — refreshed for the new Act 2 route tour and caption/card options.

## Capabilities

This change modifies no product behaviour — the UI adapter, the CLI, and the ingest contract are
untouched. It changes a Cypress e2e spec, a demo tooling script, and docs. `.openspec.yaml` sets
`skip_specs: true` (same as `ui-session-video`).

## Impact

- `web/cypress/e2e/naha-admin-ui-journey.cy.ts` (composes extra journey steps; the CLI runs the real
  journey against the deployed NAHA e2e app)
- `web/scripts/stitch-demo-video.mjs`, `docs/demo-videos.md`
- **Depends on** NAHA `admin-e2e-route-auth` being merged and live on the `e2e-admin` Preview —
  without it, `/companies` and `/policies` redirect to sign-in and the new `ui.assertVisible` steps
  fail.
- No change to `ReleaseTwin.sln` code or tests; `ReleaseTwin.Adapters.Ui.Tests` unaffected.
